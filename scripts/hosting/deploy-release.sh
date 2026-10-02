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
set -u
EXPECTED=${1:?expected development sha (full 40 characters)}
SERVICES=${2:-db-migrator api authserver angular}
PREV=${3:?rollback sha (full 40 characters): the release running now}
cd ~/hcs-patient-portal || { echo "STOP: no checkout"; exit 1; }
C="docker compose --env-file secrets/env.prod -f docker-compose.prod.yml"
ts() { date -u +%H:%M:%SZ; }
stop() { echo "$(ts) DEPLOY STOPPED: $*"; exit 1; }
# The internal base domain is not committed (public repository): read it from the compose env file,
# parsed rather than sourced, exactly as build-docs-site.sh does. The last BASE_DOMAIN line wins.
BASE=$(grep -E '^[[:space:]]*BASE_DOMAIN=' secrets/env.prod 2>/dev/null | tail -n 1 | cut -d= -f2- \
  | tr -d '\r' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/")
[ -n "$BASE" ] || stop "BASE_DOMAIN missing or empty in secrets/env.prod"
code() { curl -sk -o /dev/null -w '%{http_code}' --max-time 20 --resolve "$1:443:127.0.0.1" "https://$1$2"; }
# 2026-10-02: a short sha passed the backup and the fast-forward, then stopped at the HEAD comparison below, with HEAD
# already moved. A relaunch that let the rollback target default to HEAD would then have rolled back to the release
# being deployed. So both shas are full ids, checked before anything runs, and the rollback target is never inferred.
[[ "$EXPECTED" =~ ^[0-9a-f]{40}$ ]] || stop "expected sha must be the full 40-character id, got [$EXPECTED]"
[[ "$PREV" =~ ^[0-9a-f]{40}$ ]] || stop "rollback sha must be the full 40-character id, got [$PREV]"
git cat-file -e "$PREV^{commit}" 2>/dev/null || stop "rollback target $PREV is not in this clone"
[ "$PREV" = "$(git rev-parse HEAD)" ] || echo "$(ts) NOTE: rollback target $PREV is not the current HEAD $(git rev-parse HEAD)"

echo "$(ts) start: HEAD $(git rev-parse HEAD) on $(git rev-parse --abbrev-ref HEAD); build [$SERVICES]"
dirty=$(git status --porcelain --untracked-files=no)
[ -z "$dirty" ] || stop "tracked changes on the box: $dirty"
# After the 09-30 rollback the box sits DETACHED at the rollback target (rollback-release.sh); return to the branch.
if [ "$(git rev-parse --abbrev-ref HEAD)" = HEAD ]; then
  git checkout -q development || stop "could not return from detached HEAD to development"
  echo "$(ts) was detached; now on development at $(git rev-parse HEAD)"
fi
[ "$(git rev-parse --abbrev-ref HEAD)" = development ] || stop "not on development"

# Fresh OFF-BOX backup through the installed unit (supervisor runbook step 1). Judged by its artefacts: the success
# marker it touches only after the off-box sizes are verified, its DONE journal line, and new local dumps.
T0=$(date +%s); J0=$(date '+%Y-%m-%d %H:%M:%S')
sudo systemctl start hcs-portal-backup.service < /dev/null
ls_mt=$(sudo stat -c %Y /var/backups/hcs-portal/last-success 2>/dev/null || echo 0)
[ "$ls_mt" -ge "$T0" ] || stop "backup: last-success marker not refreshed (mtime $ls_mt < start $T0)"
done_line=$(sudo journalctl -u hcs-portal-backup.service --since "$J0" --no-pager < /dev/null | grep -E "DONE [0-9]{8}-[0-9]{6}" | tail -1)
[ -n "$done_line" ] || stop "backup: no DONE line in the journal since $J0"
newbak=$(find backups -maxdepth 1 -name '*.bak' -newermt "@$T0" 2>/dev/null | wc -l)
[ "$newbak" -ge 2 ] || stop "backup: only $newbak new .bak files since start"
echo "$(ts) backup OK: $newbak new dumps; ${done_line##*]: }"

git fetch -q origin development || stop "fetch"
# The expected sha must be ON development, but need not be its tip: development keeps moving while a deploy is
# prepared, and the release is the commit that was chosen and checked, not whatever landed since (2026-10-02).
git merge-base --is-ancestor "$EXPECTED" origin/development \
  || stop "$EXPECTED is not on origin/development ($(git rev-parse origin/development))"
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
free=$(df --output=avail -BG / | tail -1 | tr -dc 0-9)
echo "$(ts) disk free ${free}G"
if [ "$free" -lt 15 ]; then
  docker builder prune -f --filter until=48h < /dev/null | tail -1   # build cache only: no images, no volumes
  echo "$(ts) after cache prune: $(df --output=avail -BG / | tail -1 | tr -dc 0-9)G free"
fi
bash scripts/hosting/build-docs-site.sh < /dev/null || stop "build-docs-site.sh failed (served site unchanged)"
[ -f docker/nginx-proxy/docs-site/index.html ] || stop "docs-site has no index.html after the build"
[ -f docker/nginx-proxy/docs-site/search/search_index.json ] || stop "docs-site has no search index after the build"
echo "$(ts) docs site built: $(find docker/nginx-proxy/docs-site -type f | wc -l) files, owner $(stat -c '%U' docker/nginx-proxy/docs-site)"

# --- app ---
# DOTNET_ENVIRONMENT=Development would override ASPNETCORE_ENVIRONMENT and reopen the Development-only consoles.
# db-migrator legitimately sets it to Production (compose line from #340; a generic-host console reads DOTNET_), so
# the check is "no value other than Production", not "absent" (the first 09-30 run stopped on that Production line).
envs=$($C config < /dev/null 2>/dev/null | grep -o 'DOTNET_ENVIRONMENT: *"\?[A-Za-z]*' | sed 's/.*: *"\?//' | sort | uniq -c | tr '\n' ' ')
bad=$($C config < /dev/null 2>/dev/null | grep -o 'DOTNET_ENVIRONMENT: *"\?[A-Za-z]*' | sed 's/.*: *"\?//' | grep -vcx Production)
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
# The 09-30 run 2 failed here (migrator DI crash) with no diagnosis in the log; print the migrator's end state first.
$C up -d < /dev/null || { docker inspect hcs-patient-portal-db-migrator-1 --format 'db-migrator: {{.State.Status}} exit={{.State.ExitCode}}'; $C logs --tail 15 db-migrator < /dev/null 2>&1 | cut -c1-300; stop "up -d failed -- the stack is NOT serving; rollback: rollback-release.sh $PREV"; }
$C up -d --force-recreate reverse-proxy < /dev/null || stop "reverse-proxy recreate failed"
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
# Adrian 2026-09-30 12:5x ("Fix it in today's release"): identity-model PII logging must be OFF in both web hosts.
echo "api DisablePII:       $(docker exec hcs-patient-portal-api-1 printenv App__DisablePII 2>&1)  (want true)"
echo "auth DisablePII:      $(docker exec hcs-patient-portal-authserver-1 printenv App__DisablePII 2>&1)  (want true)"

echo "$(ts) [ERR]/[FTL] lines since start, api + authserver: $($C logs --since 20m api authserver < /dev/null 2>&1 | grep -cE '\[(ERR|FTL)\]')"
$C ps --format '{{.Service}} {{.Status}}' < /dev/null
echo "$(ts) DEPLOY DONE $(git rev-parse HEAD)"
