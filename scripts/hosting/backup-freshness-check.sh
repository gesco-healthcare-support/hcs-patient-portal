#!/usr/bin/env bash
# Backup freshness check (#945, 2026-09-24). Daily at 09:00 (hcs-portal-backup-freshness.timer). Emails through
# backup-alert.sh when the off-box backup has not SUCCEEDED recently, whatever the reason.
#
# WHY IT EXISTS ON TOP OF OnFailure=
#   OnFailure= on the backup units reports a run that fails. It cannot report a run that never happens: a timer
#   not enabled after a rebuild, units not reinstalled after a move, or the machine off at 01:30. From the outside
#   those look exactly like a working backup that has nothing to say, which is how a silent gap starts.
#
# HOW
#   backup-offbox.sh touches two markers only after it fully succeeds: last-success on every run, and
#   last-restore-proof on the weekly --verify-restore run. This compares their ages with the limits below. A
#   missing marker counts as stale: no success has been recorded on this machine.
#
# Env (all optional): MARKER_DIR (default /var/backups/hcs-portal), BACKUP_MAX_AGE_HOURS (default 26: one nightly
#   run plus slack), PROOF_MAX_AGE_HOURS (default 192: eight days, one weekly proof plus a day), REPO_DIR,
#   BACKUP_ALERT_DRY_RUN (passed through to backup-alert.sh).
# Exit status: 0 when both are fresh or every alert was sent; 1 when an alert could not be sent.
set -euo pipefail

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"
MARKER_DIR="${MARKER_DIR:-/var/backups/hcs-portal}"
BACKUP_MAX_AGE_HOURS="${BACKUP_MAX_AGE_HOURS:-26}"
PROOF_MAX_AGE_HOURS="${PROOF_MAX_AGE_HOURS:-192}"

now="$(date +%s)"
unsent=0

# Alerts when <marker> is missing or older than <max_hours>. <what> names it in the email.
check() {
  local what="$1" marker="$2" max_hours="$3" detail mtime age_seconds
  if [[ ! -f "$marker" ]]; then
    detail="No successful ${what} has been recorded on this machine: ${marker} does not exist."
  else
    mtime="$(stat -c %Y "$marker")"
    age_seconds=$((now - mtime))
    if ((age_seconds <= max_hours * 3600)); then
      echo "backup-freshness-check: ${what} OK ($((age_seconds / 3600))h old, limit ${max_hours}h)"
      return 0
    fi
    detail="The last successful ${what} was $((age_seconds / 3600)) hours ago ($(date -u -d "@${mtime}" +%FT%TZ)); the limit is ${max_hours} hours."
  fi

  echo "backup-freshness-check: ${what} STALE: ${detail}"
  bash ./scripts/hosting/backup-alert.sh stale "$what" "$detail" || unsent=$((unsent + 1))
}

check "nightly off-box backup" "${MARKER_DIR}/last-success" "$BACKUP_MAX_AGE_HOURS"
check "weekly restore proof" "${MARKER_DIR}/last-restore-proof" "$PROOF_MAX_AGE_HOURS"

if ((unsent > 0)); then
  echo "backup-freshness-check: ERROR: ${unsent} alert(s) could not be sent" >&2
  exit 1
fi
