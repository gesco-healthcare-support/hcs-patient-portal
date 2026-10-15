#!/bin/bash
# Prove, on a BUILT IMAGE, that the ABP key is gone and the scrub broke nothing (#899).
# Nothing in CI builds these images, so this is the check to run by hand after
# touching docker/scrub-nuget-key.sh or a backend Dockerfile's restore step.
#
#   ABP_NUGET_API_KEY=<key> docker/verify-nuget-scrub.sh IMAGE [PROJECT]
#
# IMAGE   an SDK-stage image (target `build` or `dev`) built from the repo root, e.g.
#         docker build --target build --secret id=abp_nuget_key,env=ABP_NUGET_API_KEY \
#           -f src/<Project>/Dockerfile -t probe:build .
# PROJECT csproj to publish (default: the DbMigrator).
#
# Checks: (1) zero files under /root /src /app /artifacts /tmp contain the key; (2) a fresh
# restore with NO feed config still succeeds from the scrubbed package cache and
# with --locked-mode --force (a full re-evaluation, not the up-to-date no-op); (3) `dotnet publish --no-restore` succeeds; (4) a restore
# WITH the real feed config (the dev-restore-run.sh path on a warm volume) succeeds.
# Never prints the key.
set -u
IMAGE="${1:?image name}"
PROJECT="${2:-src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj}"
: "${ABP_NUGET_API_KEY:?set ABP_NUGET_API_KEY}"
SLN=HealthcareSupport.CaseEvaluation.slnx
fail=0
run() { MSYS_NO_PATHCONV=1 docker run --rm -e K="$ABP_NUGET_API_KEY" -e P="$PROJECT" -e SLN="$SLN" \
          -e DOTNET_CLI_TELEMETRY_OPTOUT=1 --entrypoint sh "$IMAGE" -c "$1"; }

n=$(run 'grep -rlF "$K" /root /src /app /artifacts /tmp 2>/dev/null | wc -l')
echo "1. files still holding the key: $n"; [ "$n" = 0 ] || fail=1

run 'dotnet restore "$SLN" --locked-mode --force >/tmp/r.log 2>&1; echo "exit=$?"; tail -3 /tmp/r.log' | sed 's/^/2. keyless restore: /'
run 'dotnet restore "$SLN" --locked-mode --force >/dev/null 2>&1' || { echo "2. FAILED"; fail=1; }

run 'dotnet publish "$P" -c Release -o /tmp/pub --no-restore >/tmp/p.log 2>&1; rc=$?; tail -2 /tmp/p.log; exit $rc' \
  | sed 's/^/3. publish --no-restore: /'
[ "${PIPESTATUS[0]}" = 0 ] || { echo "3. FAILED"; fail=1; }

# Same-container so the config the key is rendered into never reaches a layer.
run 'printf "%s" "$K" > /tmp/k && sh /usr/local/bin/write-nuget-config.sh /tmp/k >/dev/null \
  && dotnet restore "$SLN" --locked-mode --force >/tmp/r2.log 2>&1; rc=$?; rm -f NuGet.Config /tmp/k; tail -2 /tmp/r2.log; exit $rc' \
  | sed 's/^/4. keyed restore on scrubbed cache: /'
[ "${PIPESTATUS[0]}" = 0 ] || { echo "4. FAILED"; fail=1; }

[ "$fail" = 0 ] && echo "PASS" || { echo "FAIL"; exit 1; }
