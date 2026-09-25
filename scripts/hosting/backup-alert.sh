#!/usr/bin/env bash
# Backup alert (#945, 2026-09-24). Emails a person when the off-box backup fails, or when the daily freshness
# check finds that it has stopped succeeding.
#
# WHY IT EXISTS
#   The backup units used to fail only into the journal, and nobody reads the journal at 01:30. Backups here
#   were already silently broken for ten days once (see the header of backup-offbox.sh). This script is what
#   OnFailure= on those units starts, and what backup-freshness-check.sh calls.
#
# HOW IT SENDS
#   curl over SMTP, with the portal's own relay settings from secrets/env.prod (SMTP_HOST, SMTP_PORT,
#   SMTP_ENABLE_SSL, SMTP_USERNAME, SMTP_PASSWORD, SMTP_FROM_ADDRESS), to BACKUP_ALERT_RECIPIENTS (addresses
#   separated by ; or ,). The password reaches curl through a config on stdin, never the command line, so it
#   cannot appear in the process list or the journal.
#
# WHAT IT CARRIES
#   The unit name, the host, the time and, for a failure, the unit's last journal lines -- file names, sizes and
#   counts. No PHI.
#
# USAGE
#   backup-alert.sh failed <unit>            # from hcs-portal-backup-alert@.service (%i = the failed unit)
#   backup-alert.sh stale <what> <detail>    # from backup-freshness-check.sh
#   BACKUP_ALERT_DRY_RUN=1 backup-alert.sh ...   # print the message and recipients instead of sending
#
# Env (all optional): ENV_FILE (default secrets/env.prod), REPO_DIR, BACKUP_ALERT_DRY_RUN.
# Exit status: 0 sent (or dry run), 1 not sent (and why, on stderr, which lands in the journal), 2 bad usage.
set -euo pipefail

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"
ENV_FILE="${ENV_FILE:-secrets/env.prod}"

usage() { echo "usage: backup-alert.sh failed <unit> | stale <what> <detail>" >&2; exit 2; }
refuse() { echo "backup-alert: ERROR: $* -- this alert was NOT sent" >&2; exit 1; }

# One key from the env file; empty when absent. Never echoed.
env_value() { { grep -E "^$1=" "$ENV_FILE" || true; } | head -1 | cut -d= -f2-; }

kind="${1:-}"
case "$kind" in
  failed) [[ $# -eq 2 ]] || usage; unit="$2" ;;
  stale) [[ $# -eq 3 ]] || usage; what="$2"; detail="$3" ;;
  *) usage ;;
esac

[[ -f "$ENV_FILE" ]] || refuse "env file not found: $ENV_FILE"

recipients_raw="$(env_value BACKUP_ALERT_RECIPIENTS)"
smtp_host="$(env_value SMTP_HOST)"
smtp_port="$(env_value SMTP_PORT)"
smtp_port="${smtp_port:-587}"
smtp_ssl="$(env_value SMTP_ENABLE_SSL)"
smtp_user="$(env_value SMTP_USERNAME)"
smtp_pass="$(env_value SMTP_PASSWORD)"
from="$(env_value SMTP_FROM_ADDRESS)"

# Split on ; and , -- trimmed, blanks and repeats (case-insensitive) dropped.
mapfile -t recipients < <(printf '%s\n' "$recipients_raw" | tr ';,' '\n\n' \
  | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | awk 'NF && !seen[tolower($0)]++')

[[ ${#recipients[@]} -gt 0 ]] || refuse "BACKUP_ALERT_RECIPIENTS is empty in $ENV_FILE"
[[ -n "$smtp_host" ]] || refuse "SMTP_HOST is empty in $ENV_FILE"
[[ -n "$from" ]] || refuse "SMTP_FROM_ADDRESS is empty in $ENV_FILE"

host="$(hostname)"
now="$(date -u +%FT%TZ)"

if [[ "$kind" = "failed" ]]; then
  subject="Patient Portal backup: ${unit} failed on ${host}"
  if command -v journalctl >/dev/null 2>&1; then
    journal="$(journalctl -u "$unit" -n 40 --no-pager 2>&1 || echo "(the journal could not be read)")"
  else
    journal="(journalctl is not available on this machine)"
  fi
  body="The unit ${unit} on ${host} entered the failed state (alert sent ${now}).

Until it succeeds again, the newest off-box copy of the portal's databases and documents is older than
intended. Check the journal (journalctl -u ${unit}) and the runbook docs/runbooks/hosting-backup-restore.md.

Last journal lines:
${journal}"
else
  subject="Patient Portal backup: the ${what} has not succeeded recently on ${host}"
  body="${detail}

Checked ${now} on ${host}. The backup may have stopped running rather than failing (a timer not enabled after a
rebuild, or the machine off at the scheduled time), which is why this is checked separately from failures.
See the runbook docs/runbooks/hosting-backup-restore.md."
fi

to_line="$(IFS=,; echo "${recipients[*]}")"
message="$(mktemp)"
trap 'rm -f "$message"' EXIT
{
  printf 'From: %s\n' "$from"
  printf 'To: %s\n' "$to_line"
  printf 'Subject: %s\n' "$subject"
  printf 'Date: %s\n' "$(date -R)"
  printf 'MIME-Version: 1.0\n'
  printf 'Content-Type: text/plain; charset=us-ascii\n'
  printf '\n%s\n' "$body"
} > "$message"

# smtps:// for implicit TLS on 465; otherwise smtp:// and, when SSL is on, STARTTLS is required.
scheme="smtp"
tls_args=()
if [[ "${smtp_ssl,,}" = "true" ]]; then
  if [[ "$smtp_port" = "465" ]]; then scheme="smtps"; else tls_args=(--ssl-reqd); fi
fi
url="${scheme}://${smtp_host}:${smtp_port}"
rcpt_args=()
for r in "${recipients[@]}"; do rcpt_args+=(--mail-rcpt "$r"); done

if [[ "${BACKUP_ALERT_DRY_RUN:-}" = "1" ]]; then
  echo "DRY RUN: would send via ${url} ${tls_args[*]:-} as '${smtp_user:-(no login)}' to: ${to_line}"
  cat "$message"
  exit 0
fi

# Credentials as a curl config on stdin. Backslashes and double quotes are escaped for the config syntax.
auth_config=""
if [[ -n "$smtp_user" ]]; then
  cred="${smtp_user}:${smtp_pass}"
  cred="${cred//\\/\\\\}"
  cred="${cred//\"/\\\"}"
  auth_config="user = \"${cred}\""
fi

if ! printf '%s\n' "$auth_config" | curl --silent --show-error --crlf --connect-timeout 15 --max-time 60 \
    "${tls_args[@]}" --url "$url" --mail-from "$from" "${rcpt_args[@]}" --upload-file "$message" --config -; then
  refuse "curl could not deliver it through ${url}"
fi

echo "backup-alert: sent '${subject}' to ${#recipients[@]} recipient(s)"
