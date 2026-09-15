#!/bin/sh
# Entrypoint for the `dev` stage of the three backend Dockerfiles.
#
# The dev containers bind-mount ./src, ./test and the solution file and keep the
# NuGet package cache in a named volume, so the restore baked into the image is
# only a cache warm-up -- a real restore has to run on every start to pick up
# csproj changes, and on a fresh volume it has to reach the private ABP feed.
#
# That is why the ABP key is needed at RUN time here and not only at build time,
# and why it arrives as a Compose service secret (a tmpfs at
# /run/secrets/abp_nuget_key) rather than an environment variable: #703. The
# rendered NuGet.Config is deleted before the app is exec'd, so it exists only
# for the seconds the restore takes and never in an image layer.
#
# Usage: dev-restore-run.sh <project.csproj> [extra dotnet run args...]
set -eu

PROJECT="$1"
shift

SOLUTION="${SOLUTION:-HealthcareSupport.CaseEvaluation.slnx}"

if [ -r /run/secrets/abp_nuget_key ]; then
  sh /usr/local/bin/write-nuget-config.sh
else
  # Not fatal: a warm nuget_packages volume restores entirely from the global
  # packages folder without contacting any feed. It IS fatal on a fresh volume,
  # and the restore log below is where that shows up, so say so now.
  echo "dev-restore-run: /run/secrets/abp_nuget_key absent -- restoring without the" >&2
  echo "  ABP feed. Fine if the nuget_packages volume is warm; NU1101 on Volo.*" >&2
  echo "  packages means it is not. Add the service's secrets: block." >&2
fi

rc=0
dotnet restore "$SOLUTION" >/tmp/restore.log 2>&1 || rc=$?
rm -f NuGet.Config
if [ "$rc" -ne 0 ]; then
  cat /tmp/restore.log
  exit "$rc"
fi

exec dotnet run --no-launch-profile --no-restore --project "$PROJECT" "$@"
