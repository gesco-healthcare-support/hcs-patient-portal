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
  echo "refreshed secrets in $wt"
done
