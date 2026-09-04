#!/usr/bin/env bash
# refresh-secrets.sh
#
# Copy docker/appsettings.secrets.json (the canonical ABP-license holder in
# this repo) into the four per-service locations of every worktree under
# /w/patient-portal/. Run after the license rotates, or after creating a new
# worktree by hand without the add-worktree helper.
set -euo pipefail

ROOT="/w/patient-portal"
MAIN="$ROOT/main"
SRC="$MAIN/docker/appsettings.secrets.json"

[ -f "$SRC" ] || { echo "error: $SRC missing; cannot refresh" >&2; exit 1; }

shopt -s nullglob
for wt in "$ROOT"/*/; do
  # Only process git worktrees (the .git file or dir exists).
  [ -e "$wt/.git" ] || continue
  for svc in AuthServer HttpApi.Host DbMigrator; do
    dest="$wt/src/HealthcareSupport.CaseEvaluation.$svc/appsettings.secrets.json"
    cp "$SRC" "$dest"
  done
  cp "$SRC" "$wt/test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json"

  # Also copy into docker/ so docker-compose.yml's bind mount finds a real
  # file. If Docker pre-created an empty directory there (happens when a
  # prior `docker compose up -d` hit a missing source file), remove it first.
  # Skip the self-copy when the loop visits main (src and dest resolve equal).
  wt_docker="$wt/docker/appsettings.secrets.json"
  src_real="$(cd "$(dirname "$SRC")" 2>/dev/null && pwd)/$(basename "$SRC")"
  wt_docker_parent="$(cd "$(dirname "$wt_docker")" 2>/dev/null && pwd || echo "")"
  if [ -n "$wt_docker_parent" ] && [ "$src_real" != "$wt_docker_parent/$(basename "$wt_docker")" ]; then
    [ -d "$wt_docker" ] && rm -rf "$wt_docker"
    cp "$SRC" "$wt_docker"
  fi

  echo "refreshed secrets in $wt"
done
