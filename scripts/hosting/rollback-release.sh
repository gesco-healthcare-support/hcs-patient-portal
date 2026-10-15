#!/usr/bin/env bash
# 09-30 ROLLBACK of the failed release deploy (f5496ec9: db-migrator dies in DI -- IAdminPasswordStoreLock is not
# registered) to the last good release. Runs ON the box from its own file, detached; asserts on artefacts.
# Checks out the target DETACHED so the box's development ref is untouched. No backup step: two fresh off-box sets
# were taken at 11:29 and 11:32 PDT (journal DONE 20260930-112926 / -113221) and the migrator died before migrating.
# Usage (on the box): nohup bash /tmp/rollback-release.sh <good_sha> > /tmp/rollback-release.log 2>&1 < /dev/null &
set -u
TARGET=${1:?good sha}
SERVICES="db-migrator api authserver angular"
cd ~/hcs-patient-portal || { echo "STOP: no checkout"; exit 1; }
C="docker compose --env-file secrets/env.prod -f docker-compose.prod.yml"
ts() { date -u +%H:%M:%SZ; }
stop() { echo "$(ts) ROLLBACK STOPPED: $*"; exit 1; }
# The internal base domain is not committed (public repository): read it from the compose env file,
# parsed rather than sourced, exactly as build-docs-site.sh does. The last BASE_DOMAIN line wins.
BASE=$(grep -E '^[[:space:]]*BASE_DOMAIN=' secrets/env.prod 2>/dev/null | tail -n 1 | cut -d= -f2- \
  | tr -d '\r' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/")
[ -n "$BASE" ] || stop "BASE_DOMAIN missing or empty in secrets/env.prod"
code() { curl -sk -o /dev/null -w '%{http_code}' --max-time 20 --resolve "$1:443:127.0.0.1" "https://$1$2"; }

echo "$(ts) start: HEAD $(git rev-parse HEAD) on $(git rev-parse --abbrev-ref HEAD); target $TARGET"
dirty=$(git status --porcelain --untracked-files=no)
[ -z "$dirty" ] || stop "tracked changes on the box: $dirty"
git cat-file -e "$TARGET^{commit}" || stop "target $TARGET not in the box's clone"
git checkout -q --detach "$TARGET" || stop "checkout refused"
[ "$(git rev-parse HEAD)" = "$TARGET" ] || stop "HEAD is $(git rev-parse HEAD) after checkout"
echo "$(ts) checkout: detached at $TARGET (development ref still $(git rev-parse development))"

$C build $SERVICES < /dev/null || stop "build failed"
echo "$(ts) images built"
$C up -d < /dev/null || stop "up -d failed"
$C up -d --force-recreate reverse-proxy < /dev/null || stop "reverse-proxy recreate failed"
echo "$(ts) stack up"
mig=$(docker inspect hcs-patient-portal-db-migrator-1 --format '{{.State.Status}} exit={{.State.ExitCode}}')
echo "$(ts) db-migrator: $mig"
[ "$mig" = "exited exit=0" ] || { $C logs --tail 40 db-migrator < /dev/null; stop "db-migrator did not exit 0"; }

sleep 25   # let api/authserver finish starting before probing
echo "admin.auth discovery: $(code admin.auth.$BASE /.well-known/openid-configuration)  (want 200)"
echo "admin.api app-config: $(code admin.api.$BASE /api/abp/application-configuration)  (want 200)"
echo "bogus.api app-config: $(code bogus-office.api.$BASE /api/abp/application-configuration)  (want 404)"
echo "admin SPA:            $(code admin.$BASE /)  (want 200)"
echo "falkinstein SPA:      $(code falkinstein.$BASE /)  (want 200)"
echo "apex page:            $(code $BASE /)  (want 200)"
echo "api /health-status:   $(code admin.api.$BASE /health-status)  (want 200)"
echo "$(ts) [ERR]/[FTL] lines since start, api + authserver: $($C logs --since 15m api authserver < /dev/null 2>&1 | grep -cE '\[(ERR|FTL)\]')"
$C ps --format '{{.Service}} {{.Status}}' < /dev/null
echo "$(ts) ROLLBACK DONE $(git rev-parse HEAD)"
