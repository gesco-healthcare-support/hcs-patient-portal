#!/usr/bin/env bash
# 09-30 release deploy (Adrian 2026-09-30: "deploy to server both the App and the docs site") of `development` @ <sha>.
# Derived from deploy_dev.sh (09-25). Runs ON the box from its own file -- never piped over ssh, because
# backup-databases.sh hides an `exec -T` that would eat a piped script. Stops at the first failure and asserts on
# artefacts (HEAD, stat, container state, HTTP codes), never on a launcher's exit code.
# Adds the three release prerequisites, each BEFORE compose starts anything:
#   1. #1151: the AuthServer image now runs as uid/gid 1654, so the token-signing pfx needs group 1654 + mode 640.
#      NEVER regenerate it (signing + encryption cert: replacing it signs everyone out).
#   2. #1127 (Levon's review on #1120): ./secrets/admin-passwords must exist owned 1654:1654 mode 700, or Docker creates
#      it root-owned, the migrator's first password write fails and api never starts.
#   3. #1152: build-docs-site.sh fills docker/nginx-proxy/docs-site as the deploy user; if compose ran first, Docker
#      would create that mount source as root and the script could no longer write into it.
# Usage (on the box): nohup bash /tmp/deploy-release.sh <expected_sha> "<services to build>" <rollback_sha> > /tmp/deploy-release.log 2>&1 < /dev/null &
# Both shas are FULL 40-character ids, and the rollback sha is the release running NOW. Copy the scripts to /tmp
# first and run that copy: this checkout moves under a script run from inside it.
#
# Two settings come from the environment. Their defaults ARE the office server, which therefore needs nothing new:
#   DEPLOY_BRANCH  the branch this host deploys from                 (default development)
#   BACKUP_UNIT    the systemd unit that takes the pre-deploy backup  (default hcs-portal-backup.service)
# The Azure VM backs up with hcs-portal-backup-azure.service instead, so it launches with both set, e.g.
#   DEPLOY_BRANCH=<branch> BACKUP_UNIT=hcs-portal-backup-azure.service nohup bash /tmp/deploy-release.sh ...
# Both backup units touch the same last-success marker and log the same "DONE <stamp>" line, so the backup check
# below judges either one by the same artefacts.
set -u
DEPLOY_BRANCH=${DEPLOY_BRANCH:-development}
BACKUP_UNIT=${BACKUP_UNIT:-hcs-portal-backup.service}
EXPECTED=${1:?expected sha on the deploy branch (full 40 characters)}
SERVICES=${2:-db-migrator api authserver angular}
PREV=${3:?rollback sha (full 40 characters): the release running now}
cd ~/hcs-patient-portal || { echo "STOP: no checkout"; exit 1; }
C="docker compose --env-file secrets/env.prod -f docker-compose.prod.yml"
ts() { date -u +%H:%M:%SZ; }
stop() { echo "$(ts) DEPLOY STOPPED: $*"; exit 1; }
# Checked before anything else runs. A misspelled unit would otherwise fail at `systemctl start` with a message
# about a unit rather than about this setting, and the branch name goes into git commands below.
case "$BACKUP_UNIT" in
  hcs-portal-backup.service|hcs-portal-backup-azure.service) ;;
  *) stop "BACKUP_UNIT must be hcs-portal-backup.service or hcs-portal-backup-azure.service, got [$BACKUP_UNIT]" ;;
esac
[[ "$DEPLOY_BRANCH" =~ ^[A-Za-z0-9][A-Za-z0-9._/-]*$ ]] || stop "DEPLOY_BRANCH is not a plain branch name: [$DEPLOY_BRANCH]"
# The internal base domain is not committed (public repository): read it from the compose env file,
# parsed rather than sourced, exactly as build-docs-site.sh does. The last BASE_DOMAIN line wins.
BASE=$(grep -E '^[[:space:]]*BASE_DOMAIN=' secrets/env.prod 2>/dev/null | tail -n 1 | cut -d= -f2- \
  | tr -d '\r' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/")
[ -n "$BASE" ] || stop "BASE_DOMAIN missing or empty in secrets/env.prod"
code() { curl -sk -o /dev/null -w '%{http_code}' --max-time 20 --resolve "$1:443:127.0.0.1" "https://$1$2"; }
# Both web hosts must RUN as Production: outside Development, identity-model PII logging stays off and the API
# documentation is not served (IdentityModelPiiLogging, ApiDocumentationPipeline: both decided by the environment, not
# by a setting). Read from the running containers, after `up`, so it checks what is serving rather than what compose
# meant. ASPNETCORE_ENVIRONMENT must be exactly Production, and EMPTY FAILS: `printenv` prints nothing for an unset
# variable, compose sets the value explicitly, so empty means the read saw nothing, and a check that passes on seeing
# nothing is the failure mode the compose-config check below already refuses. DOTNET_ENVIRONMENT may be unset or
# Production, the same rule as that compose-config check. Returns non-zero when either host is wrong.
check_web_env() {
  local svc a d bad=0
  for svc in api authserver; do
    a=$(docker exec "hcs-patient-portal-$svc-1" printenv ASPNETCORE_ENVIRONMENT 2>&1 < /dev/null)
    d=$(docker exec "hcs-patient-portal-$svc-1" printenv DOTNET_ENVIRONMENT 2>&1 < /dev/null)
    if [ "$a" = Production ] && { [ -z "$d" ] || [ "$d" = Production ]; }; then
      printf '%-22sASPNETCORE_ENVIRONMENT=[%s] DOTNET_ENVIRONMENT=[%s]  OK\n' "${svc%server} environment:" "$a" "$d"
    else
      printf '%-22sASPNETCORE_ENVIRONMENT=[%s] DOTNET_ENVIRONMENT=[%s]  WRONG (want Production, and unset or Production)\n' \
        "${svc%server} environment:" "${a:0:160}" "${d:0:160}"
      bad=$((bad + 1))
    fi
  done
  [ "$bad" = 0 ]
}
# 2026-10-02: a short sha passed the backup and the fast-forward, then stopped at the HEAD comparison below, with HEAD
# already moved. A relaunch that let the rollback target default to HEAD would then have rolled back to the release
# being deployed. So both shas are full ids, checked before anything runs, and the rollback target is never inferred.
[[ "$EXPECTED" =~ ^[0-9a-f]{40}$ ]] || stop "expected sha must be the full 40-character id, got [$EXPECTED]"
[[ "$PREV" =~ ^[0-9a-f]{40}$ ]] || stop "rollback sha must be the full 40-character id, got [$PREV]"
git cat-file -e "$PREV^{commit}" 2>/dev/null || stop "rollback target $PREV is not in this clone"
[ "$PREV" = "$(git rev-parse HEAD)" ] || echo "$(ts) NOTE: rollback target $PREV is not the current HEAD $(git rev-parse HEAD)"

echo "$(ts) start: HEAD $(git rev-parse HEAD) on $(git rev-parse --abbrev-ref HEAD); deploy branch $DEPLOY_BRANCH; backup unit $BACKUP_UNIT; build [$SERVICES]"
dirty=$(git status --porcelain --untracked-files=no)
[ -z "$dirty" ] || stop "tracked changes on the box: $dirty"
# After the 09-30 rollback the box sits DETACHED at the rollback target (rollback-release.sh); return to the branch.
if [ "$(git rev-parse --abbrev-ref HEAD)" = HEAD ]; then
  git checkout -q "$DEPLOY_BRANCH" || stop "could not return from detached HEAD to $DEPLOY_BRANCH"
  echo "$(ts) was detached; now on $DEPLOY_BRANCH at $(git rev-parse HEAD)"
fi
[ "$(git rev-parse --abbrev-ref HEAD)" = "$DEPLOY_BRANCH" ] || stop "not on $DEPLOY_BRANCH"

# Fresh OFF-BOX backup through the installed unit (supervisor runbook step 1). Judged by its artefacts: the success
# marker it touches only after the off-box sizes are verified, its DONE journal line, and new local dumps.
T0=$(date +%s); J0=$(date '+%Y-%m-%d %H:%M:%S')
sudo systemctl start "$BACKUP_UNIT" < /dev/null
ls_mt=$(sudo stat -c %Y /var/backups/hcs-portal/last-success 2>/dev/null || echo 0)
[ "$ls_mt" -ge "$T0" ] || stop "backup: last-success marker not refreshed (mtime $ls_mt < start $T0)"
done_line=$(sudo journalctl -u "$BACKUP_UNIT" --since "$J0" --no-pager < /dev/null | grep -E "DONE [0-9]{8}-[0-9]{6}" | tail -1)
[ -n "$done_line" ] || stop "backup: no DONE line in the journal since $J0"
newbak=$(find backups -maxdepth 1 -name '*.bak' -newermt "@$T0" 2>/dev/null | wc -l)
[ "$newbak" -ge 2 ] || stop "backup: only $newbak new .bak files since start"
echo "$(ts) backup OK: $newbak new dumps; ${done_line##*]: }"

git fetch -q origin "$DEPLOY_BRANCH" || stop "fetch"
# The expected sha must be ON the deploy branch, but need not be its tip: the branch keeps moving while a deploy is
# prepared, and the release is the commit that was chosen and checked, not whatever landed since (2026-10-02).
git merge-base --is-ancestor "$EXPECTED" "origin/$DEPLOY_BRANCH" \
  || stop "$EXPECTED is not on origin/$DEPLOY_BRANCH ($(git rev-parse "origin/$DEPLOY_BRANCH"))"
git merge --ff-only -q "$EXPECTED" || stop "ff-only merge to $EXPECTED refused (not a fast-forward, or an untracked file collision?)"
[ "$(git rev-parse HEAD)" = "$EXPECTED" ] || stop "HEAD is $(git rev-parse HEAD) after the merge"
echo "$(ts) checkout $PREV -> $EXPECTED (rollback target: $PREV)"

# --- prerequisite 1: pfx readable by gid 1654 (#1151) ---
PFX=secrets/openiddict.pfx
[ -f "$PFX" ] || stop "$PFX missing -- do NOT regenerate; find out why"
g=$(getent group 1654 || true)
[ -z "$g" ] || stop "gid 1654 already used on the host by [$g]; its members would gain read access to the pfx"
sudo chgrp 1654 "$PFX" < /dev/null && sudo chmod 640 "$PFX" < /dev/null
[ "$(stat -c '%g %a' "$PFX")" = "1654 640" ] || stop "pfx is $(stat -c '%U:%g %a' "$PFX"), want :1654 640"
echo "$(ts) pfx: $(stat -c '%U:%g %a' "$PFX")"

# --- prerequisite 2: admin-password store (#1127) ---
AP=secrets/admin-passwords
mkdir -p "$AP"
sudo chown 1654:1654 "$AP" < /dev/null && sudo chmod 700 "$AP" < /dev/null
[ "$(stat -c '%u:%g %a' "$AP")" = "1654:1654 700" ] || stop "$AP is $(stat -c '%u:%g %a' "$AP"), want 1654:1654 700"
echo "$(ts) admin-passwords: $(stat -c '%u:%g %a' "$AP")"

# --- prerequisite 3: docs site, before compose (#1152) ---
# Measure the disk Docker actually writes to. On the office server that is /, but the Azure VM keeps Docker's root on
# its data disk (/srv/docker), so measuring / there would watch the wrong disk.
droot=$(docker info --format '{{.DockerRootDir}}' 2> /dev/null < /dev/null)
[ -d "$droot" ] || droot=/
free=$(df --output=avail -BG "$droot" | tail -1 | tr -dc 0-9)
echo "$(ts) disk free ${free}G on the filesystem holding $droot"
if [ "$free" -lt 15 ]; then
  docker builder prune -f --filter until=48h < /dev/null | tail -1   # build cache only: no images, no volumes
  echo "$(ts) after cache prune: $(df --output=avail -BG "$droot" | tail -1 | tr -dc 0-9)G free"
fi
bash scripts/hosting/build-docs-site.sh < /dev/null || stop "build-docs-site.sh failed (served site unchanged)"
[ -f docker/nginx-proxy/docs-site/index.html ] || stop "docs-site has no index.html after the build"
[ -f docker/nginx-proxy/docs-site/search/search_index.json ] || stop "docs-site has no search index after the build"
echo "$(ts) docs site built: $(find docker/nginx-proxy/docs-site -type f | wc -l) files, owner $(stat -c '%U' docker/nginx-proxy/docs-site)"

# --- prerequisite 4: DataProtection certificate (#1179) ---
# The key ring is encrypted at rest, and BOTH api and authserver mount the same pfx and refuse to
# start outside Development when the path or passphrase is blank (HostingConfigValidator).
#
# WITHOUT THIS BLOCK THE DEPLOY FAILED LATE AND CONFUSINGLY. An unset DATAPROTECTION_PFX_PATH is not
# a soft blank to compose, it is a hard parse error -- `invalid spec: :/app/dataprotection.pfx:ro:
# empty section between colons` -- so `config` produced NOTHING and the build stopped several steps
# later with "build failed". Reproduced against this compose file before adding this.
#
# It STOPS rather than generating. Same rule as the openiddict pfx above: a new certificate is a new
# key, and generating one on a whim signs out everyone who is signed in.
for v in DATAPROTECTION_PFX_PATH DATAPROTECTION_CERT_PASSPHRASE; do
  # env.prod is a compose env-file, NOT shell-sourceable, so grep it rather than sourcing it.
  grep -qE "^${v}=.+" secrets/env.prod     || stop "$v is missing or empty in secrets/env.prod. Generate once with DATAPROTECTION_CERT_PASSPHRASE=<new> bash scripts/hosting/gen-dataprotection-cert.sh secrets/dataprotection.pfx, then set BOTH keys. Expect every signed-in user to be signed out once when this first deploys."
done
DP=$(grep -E '^DATAPROTECTION_PFX_PATH=' secrets/env.prod | head -1 | cut -d= -f2- | tr -d '"')
[ -f "$DP" ] || stop "DATAPROTECTION_PFX_PATH points at $DP, which does not exist -- do NOT regenerate blindly; a new certificate signs everyone out"
# gen-dataprotection-cert.sh leaves the file 600 when it cannot chgrp, and the api runs as uid 1654,
# so without this the containers start and then crash unable to read it -- which the migrator
# pre-flight and the rehearsal both MISS, because neither loads this certificate.
sudo chgrp 1654 "$DP" < /dev/null && sudo chmod 640 "$DP" < /dev/null
[ "$(stat -c '%g %a' "$DP")" = "1654 640" ] || stop "$DP is $(stat -c '%U:%g %a' "$DP"), want :1654 640"
echo "$(ts) dataprotection pfx: $(stat -c '%U:%g %a' "$DP")"


# --- app ---
# DOTNET_ENVIRONMENT=Development would override ASPNETCORE_ENVIRONMENT and reopen the Development-only consoles.
# db-migrator legitimately sets it to Production (compose line from #340; a generic-host console reads DOTNET_), so
# the check is "no value other than Production", not "absent" (the first 09-30 run stopped on that Production line).
# Run config ONCE and keep it. It used to run twice with stderr discarded, and that made a FAILING
# config invisible: no output means no DOTNET_ENVIRONMENT lines, `grep -vcx Production` counts 0, and
# the check passes. A gate that reports success because it saw nothing is the failure mode this repo
# keeps meeting, so the emptiness is now itself a stop.
CFGERR=$(mktemp)
cfg=$($C config < /dev/null 2>"$CFGERR")   || stop "compose config FAILED, so the stack would not come up: $(tail -3 "$CFGERR" | tr '
' ' ')"
[ -n "$cfg" ] || stop "compose config succeeded but printed nothing -- treat as a failure, not a pass: $(tail -3 "$CFGERR" | tr '
' ' ')"
rm -f "$CFGERR"
envs=$(printf '%s
' "$cfg" | grep -o 'DOTNET_ENVIRONMENT: *"\?[A-Za-z]*' | sed 's/.*: *"\?//' | sort | uniq -c | tr '
' ' ')
bad=$(printf '%s
' "$cfg" | grep -o 'DOTNET_ENVIRONMENT: *"\?[A-Za-z]*' | sed 's/.*: *"\?//' | grep -vcx Production)
[ "$bad" = 0 ] || stop "compose config sets DOTNET_ENVIRONMENT to a non-Production value: [$envs]"
echo "$(ts) compose config: DOTNET_ENVIRONMENT values [${envs:-none}] -- none other than Production"
$C build $SERVICES < /dev/null || stop "build failed"
echo "$(ts) images built"

# PRE-FLIGHT (09-30: two dependency-injection crashes in the new migrator -- IAdminPasswordStoreLock, then
# IHostEnvironment -- each passed CI and only failed when the real image started, and the first one took the running
# stack down). Start the NEW migrator image with NO network and a throwaway admin-password folder: it must get past
# dependency resolution (logs "Started database migrations") and may then fail only at the SQL connection. Nothing
# touches the database (no network) and the running stack is untouched until this passes.
PF=$(mktemp -d)
sudo chown 1654:1654 "$PF" < /dev/null && sudo chmod 700 "$PF" < /dev/null
docker rm -f hcs-preflight-migrator > /dev/null 2>&1 < /dev/null
# The migrator's OWN compose environment (licence key, connection string, ...), fed through a pipe so no value is
# printed or written to disk. Without it the image exits 214 with no output (09-30 test). No network: the connection
# string names a host that cannot be reached, so nothing touches the database.
mig_env() { $C config --format json < /dev/null 2>/dev/null | python3 -c 'import json,sys; e=json.load(sys.stdin)["services"]["db-migrator"].get("environment") or {}; [print(f"{k}={v}") for k, v in e.items() if v is not None and "\n" not in str(v)]'; }
docker run -d --name hcs-preflight-migrator --network none --env-file <(mig_env) \
  -e AdminPasswords__Directory=/run/admin-passwords -e AdminPasswords__VaultUri= \
  -v "$PF:/run/admin-passwords" hcs-patient-portal-db-migrator < /dev/null > /dev/null \
  || { sudo rm -rf "$PF" < /dev/null; stop "pre-flight: could not start the new migrator image"; }
for _ in $(seq 1 45); do
  pflog=$(docker logs hcs-preflight-migrator 2>&1 < /dev/null)
  printf '%s\n' "$pflog" | grep -qE 'DependencyResolutionException|ComponentNotRegistered|None of the constructors|Started database migrations|Migrating schema for' && break
  [ "$(docker inspect -f '{{.State.Running}}' hcs-preflight-migrator < /dev/null)" = false ] && break
  sleep 2
done
pflog=$(docker logs hcs-preflight-migrator 2>&1 < /dev/null)
docker rm -f hcs-preflight-migrator > /dev/null 2>&1 < /dev/null
sudo rm -rf "$PF" < /dev/null
di=$(printf '%s\n' "$pflog" | grep -cE 'DependencyResolutionException|ComponentNotRegistered|None of the constructors')
started=$(printf '%s\n' "$pflog" | grep -cE 'Started database migrations|Migrating schema for')   # #1181 drops the first line
if [ "$di" != 0 ] || [ "$started" = 0 ]; then
  printf '%s\n' "$pflog" | grep -vE '^\s+at ' | tail -12 | cut -c1-300
  stop "pre-flight: new migrator image did not get past DI (DI error lines $di, 'Started database migrations' $started) -- running stack untouched"
fi
echo "$(ts) pre-flight: new migrator image got past DI ('Started database migrations' seen, 0 DI error lines)"

# REHEARSAL (09-30, third failure: a RUNTIME crash in the admin-password rotation that only a real database reaches).
# /tmp/rehearse-migrator.sh runs the new migrator image against a throwaway restore of the newest backups -- the one
# this run just took -- on an internal network. It must exit 0 before the live stack is touched.
rh=$(bash /tmp/rehearse-migrator.sh < /dev/null 2>&1)
printf '%s\n' "$rh" | tail -14 | cut -c1-240
printf '%s\n' "$rh" | grep -q "REHEARSAL PASS" || stop "rehearsal failed -- running stack untouched"
# AZURE VM ONLY; a no-op on a host without the unit (the office server). The Relay listener (relay-listener.sh) runs
# inside the reverse proxy's network namespace. Recreating the proxy takes that namespace's network away, so the
# listener's connection cannot close, and Azure Relay keeps counting the dead listener for about four minutes
# (measured 2026-10-20), sending some Case Tracker reads to it. Stopping the listener first closes its connection
# cleanly; it is started again once the new proxy runs, and attaches to it. If it does not come back, the "no
# listener" alert in Azure fires after 15 minutes.
RELAY_UNIT=hcs-portal-relay.service
relay_paused=false
relay_pause() {
  systemctl is-active --quiet "$RELAY_UNIT" < /dev/null || return 0
  sudo systemctl stop "$RELAY_UNIT" < /dev/null || return 1
  relay_paused=true
  echo "$(ts) relay listener stopped for the proxy recreate"
}
relay_resume() {
  $relay_paused || return 0
  sudo systemctl start "$RELAY_UNIT" < /dev/null || return 1
  relay_paused=false
  echo "$(ts) relay listener started again"
}
relay_pause || stop "could not stop the relay listener (sudo systemctl stop $RELAY_UNIT) -- running stack untouched"
# The 09-30 run 2 failed here (migrator DI crash) with no diagnosis in the log; print the migrator's end state first.
$C up -d < /dev/null || { docker inspect hcs-patient-portal-db-migrator-1 --format 'db-migrator: {{.State.Status}} exit={{.State.ExitCode}}'; $C logs --tail 15 db-migrator < /dev/null 2>&1 | cut -c1-300; relay_resume; stop "up -d failed -- the stack is NOT serving; rollback: rollback-release.sh $PREV"; }
$C up -d --force-recreate reverse-proxy < /dev/null || { relay_resume; stop "reverse-proxy recreate failed"; }
relay_resume || echo "$(ts) WARNING: the relay listener did not start again; the Case Tracker cannot read documents until: sudo systemctl start $RELAY_UNIT"
echo "$(ts) stack up"

mig=$(docker inspect hcs-patient-portal-db-migrator-1 --format '{{.State.Status}} exit={{.State.ExitCode}}')
echo "$(ts) db-migrator: $mig"
[ "$mig" = "exited exit=0" ] || { $C logs --tail 40 db-migrator < /dev/null; stop "db-migrator did not exit 0"; }
# Levon's point 2: ONLY these databases have a valid password file. The line names the database, never the value.
echo "$(ts) migrator 'Rotated the admin password of' lines (the ONLY valid password files):"
$C logs db-migrator < /dev/null 2>&1 | grep -o 'Rotated the admin password of "\?[A-Za-z0-9_]*' | tr -d '"' | sort | uniq -c   # Serilog quotes strings
echo "$(ts) admin-password file COUNT (contents never read): $(sudo ls "$AP" < /dev/null | wc -l)"

sleep 25   # let api/authserver finish starting before probing
echo "admin.auth discovery: $(code admin.auth.$BASE /.well-known/openid-configuration)  (want 200)"
echo "admin.api app-config: $(code admin.api.$BASE /api/abp/application-configuration)  (want 200)"
echo "bogus.api app-config: $(code bogus-office.api.$BASE /api/abp/application-configuration)  (want 404)"
echo "admin SPA:            $(code admin.$BASE /)  (want 200)"
echo "falkinstein SPA:      $(code falkinstein.$BASE /)  (want 200)"
echo "docs site:            $(code $BASE /docs/)  (want 200)"
echo "apex page:            $(code $BASE /)  (want 200)"
echo "api /health-status:   $(code admin.api.$BASE /health-status)  (want 200)"
echo "api /hangfire:        $(code admin.api.$BASE /hangfire)  (want 401, #1131)"
echo "api /health-ui:       $(code admin.api.$BASE /health-ui)  (want 401, #1131)"
echo "api /health-api:      $(code admin.api.$BASE /health-api)  (want 401, #1131)"
echo "auth /health-ui:      $(code admin.auth.$BASE /health-ui)  (want 302 to sign-in, #1131)"
echo "docs CSP header:      $(curl -sk -D - -o /dev/null --max-time 20 --resolve "$BASE:443:127.0.0.1" "https://$BASE/docs/" | grep -i '^content-security-policy' | cut -c1-120)"
echo "authserver user:      $(docker exec hcs-patient-portal-authserver-1 id 2>&1)"
# Replaces the App__DisablePII probe (2026-09-30): that setting no longer decides anything outside Development, so the
# variable is empty on a correct server and the probe could only ever raise a false alarm. The environment decides.
# It reports like the probes above rather than stopping, because the stack is already serving; a wrong value is
# repeated just above the last line, so it cannot scroll away.
webenv=OK; check_web_env || webenv=WRONG

echo "$(ts) [ERR]/[FTL] lines since start, api + authserver: $($C logs --since 20m api authserver < /dev/null 2>&1 | grep -cE '\[(ERR|FTL)\]')"
$C ps --format '{{.Service}} {{.Status}}' < /dev/null
[ "$webenv" = OK ] || echo "$(ts) WARNING: A WEB HOST IS NOT RUNNING AS Production (the 'environment:' lines above). The stack IS serving; decide now whether to roll back: rollback-release.sh $PREV"
echo "$(ts) DEPLOY DONE $(git rev-parse HEAD)"
