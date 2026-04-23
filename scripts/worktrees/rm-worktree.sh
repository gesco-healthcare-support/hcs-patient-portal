#!/usr/bin/env bash
# rm-worktree.sh <slug> [--force]
#
# Remove a worktree. For persistent worktrees (development/staging/production),
# prompt before dropping the associated LocalDB. Never drops the shared
# CaseEvaluation DB (used by main + feature worktrees).
#
# Usage: ./rm-worktree.sh feat-287-contact-method
#        ./rm-worktree.sh staging --force
set -euo pipefail

SLUG="${1:?usage: rm-worktree.sh <slug> [--force]}"
FORCE="${2:-}"
ROOT="/w/patient-portal"
MAIN="$ROOT/main"
TARGET="$ROOT/$SLUG"

[ -d "$TARGET" ] || { echo "error: no worktree at $TARGET" >&2; exit 1; }

# Never remove main via this helper.
if [ "$SLUG" = "main" ]; then
  echo "error: refusing to remove main worktree" >&2
  exit 1
fi

cd "$TARGET"
if [ -n "$(git status --porcelain)" ] && [ "$FORCE" != "--force" ]; then
  echo "error: $TARGET has uncommitted changes; commit/push or rerun with --force" >&2
  exit 1
fi

# DB drop prompt for persistent worktrees only. Feature worktrees share
# CaseEvaluation with main and must never drop it.
case "$SLUG" in
  development|staging|production)
    DB="CaseEvaluation_$SLUG"
    read -p "Drop LocalDB database [$DB]? [y/N] " yn
    if [ "${yn:-N}" = "y" ] || [ "$yn" = "Y" ]; then
      sqlcmd -S "(LocalDb)\\MSSQLLocalDB" \
        -Q "IF DB_ID('$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END"
      echo "dropped $DB"
    else
      echo "leaving $DB in place"
    fi
    ;;
esac

cd "$MAIN"
if [ "$FORCE" = "--force" ]; then
  git worktree remove --force "$TARGET"
else
  git worktree remove "$TARGET"
fi
echo "removed $TARGET"
