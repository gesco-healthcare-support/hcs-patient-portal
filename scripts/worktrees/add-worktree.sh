#!/usr/bin/env bash
# add-worktree.sh <branch>
#
# Creates a feature worktree under /w/patient-portal/ with auto-allocated ports
# (+10 per existing non-persistent worktree beyond main+dev+staging), copies
# secrets, renders appsettings.Local.json + environment.local.ts, runs restore
# + yarn install.
#
# Feature worktrees share main's CaseEvaluation DB, so no DbMigrator run is
# necessary. If the feature needs schema migrations applied, run DbMigrator
# manually in the new worktree after this completes.
#
# Usage: ./add-worktree.sh feat/287-contact-method
set -euo pipefail

BRANCH="${1:?usage: add-worktree.sh <branch-name>}"
SLUG="$(echo "$BRANCH" | tr '/' '-')"
ROOT="/w/patient-portal"
MAIN="$ROOT/main"
TARGET="$ROOT/$SLUG"

[ -d "$TARGET" ] && { echo "error: $TARGET already exists" >&2; exit 1; }

# Pick the next +10 port offset. main/development/staging hold offsets 0/1/2;
# feature worktrees take offset 3 onward.
offset=3
cd "$MAIN"
while IFS= read -r wt; do
  name="$(basename "$wt")"
  case "$name" in
    main|development|staging) ;;
    *) offset=$((offset + 1)) ;;
  esac
done < <(git worktree list --porcelain | awk '/^worktree/ {print $2}')

AUTH=$((44368 + offset * 10))
API=$((44327 + offset * 10))
NG=$((4200 + offset * 10))
DB="CaseEvaluation"

echo "Creating $SLUG at $TARGET (AUTH=$AUTH API=$API NG=$NG DB=$DB)"

cd "$MAIN"
git fetch origin
if git show-ref --verify --quiet "refs/heads/$BRANCH" \
   || git show-ref --verify --quiet "refs/remotes/origin/$BRANCH"; then
  git worktree add "$TARGET" "$BRANCH"
else
  git worktree add -b "$BRANCH" "$TARGET" main
fi

# Copy secrets into all worktrees (this includes the new one).
"$MAIN/scripts/worktrees/refresh-secrets.sh"

# Render per-worktree config.
"$MAIN/scripts/worktrees/render-config.sh" "$TARGET" "$AUTH" "$API" "$NG" "$DB"

# Install deps.
cd "$TARGET"
dotnet restore
(cd angular && yarn install --mutex network)

cat <<NOTE

Worktree $SLUG ready at $TARGET.
  AuthServer:   https://localhost:$AUTH
  HttpApi.Host: https://localhost:$API
  Angular:      http://localhost:$NG
  Database:     $DB (shared with main)

See scripts/worktrees/run.sh for launch order.
NOTE
