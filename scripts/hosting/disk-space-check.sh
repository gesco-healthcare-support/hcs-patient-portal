#!/usr/bin/env bash
# Disk space check (2026-10-23). Daily at 08:45 (hcs-portal-disk-space.timer). Emails through backup-alert.sh when
# the OS disk (/) or the data disk (/srv) fills past a line, and says what is using the space.
#
# WHY IT EXISTS
#   Nothing else watches free space; deploy-release.sh looks only while a deploy runs. A full data disk stops SQL
#   Server writing, so every save in the portal fails and the nightly backup fails with it; a full OS disk stops
#   logging and image builds. Both fill slowly, so one look a day with a margin is enough, as long as someone is
#   told.
#
# HOW
#   For each path in DISK_PATHS that exists, reads the percent used from df. A path on a filesystem already checked
#   is skipped, so one disk never alerts twice. The level is 0 below DISK_WARN_PERCENT, 1 at or above it, and 2 at
#   or above DISK_CRIT_PERCENT. It emails when the level rises above the level last emailed for that filesystem,
#   and again every DISK_REMIND_DAYS while the level stays at 1 or 2, so a filling disk is reported as it crosses
#   each line and is not forgotten after one email. Falling below DISK_WARN_PERCENT clears the record, so the next
#   rise emails again. An alert that could not be sent records nothing, so the next run tries again. A dry run
#   (BACKUP_ALERT_DRY_RUN=1) records nothing either.
#
# WHAT THE EMAIL CARRIES
#   The filesystem's size, used, free and percent, and what is using space: the SQL Server volume, the MinIO
#   volume, the backups folder, and Docker's own images and build cache. Sizes only: no file names, no PHI. And the
#   one cleanup that is safe on this stack, the build cache. Never prune images: the pinned MinIO image can no
#   longer be pulled from any registry.
#
# Env (all optional): DISK_PATHS (default "/ /srv"), DISK_WARN_PERCENT (70), DISK_CRIT_PERCENT (85),
#   DISK_REMIND_DAYS (7), STATE_DIR (default /var/lib/hcs-portal/disk-space), ENV_FILE (default secrets/env.prod,
#   read for BACKUP_DIR), REPO_DIR, BACKUP_ALERT_DRY_RUN (passed through to backup-alert.sh).
# Exit status: 0 when every alert that was due was sent; 1 when one could not be sent or a disk could not be read;
#   2 bad settings.
set -euo pipefail

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"
DISK_PATHS="${DISK_PATHS:-/ /srv}"
WARN="${DISK_WARN_PERCENT:-70}"
CRIT="${DISK_CRIT_PERCENT:-85}"
REMIND_DAYS="${DISK_REMIND_DAYS:-7}"
STATE_DIR="${STATE_DIR:-/var/lib/hcs-portal/disk-space}"
ENV_FILE="${ENV_FILE:-secrets/env.prod}"
DRY_RUN="${BACKUP_ALERT_DRY_RUN:-}"

bad() { echo "disk-space-check: ERROR: $*" >&2; exit 2; }
for name in WARN CRIT REMIND_DAYS; do
  [[ "${!name}" =~ ^[0-9]+$ ]] || bad "$name must be a whole number, got [${!name}]"
done
((WARN > 0 && WARN < CRIT && CRIT <= 100)) || bad "need 0 < DISK_WARN_PERCENT < DISK_CRIT_PERCENT <= 100"
((REMIND_DAYS > 0)) || bad "DISK_REMIND_DAYS must be at least 1"

mkdir -p "$STATE_DIR"
now="$(date +%s)"
problems=0

# KiB as a short human size: 1536 -> "1.5 MiB".
human() { awk -v k="$1" 'BEGIN { split("KiB MiB GiB TiB", u, " "); i = 1; while (k >= 1024 && i < 4) { k /= 1024; i++ }; printf "%.1f %s", k, u[i] }'; }

# The size of a directory, or why it is unknown.
size_of() {
  local dir="$1" kib
  [[ -d "$dir" ]] || { echo "(not found)"; return 0; }
  kib="$(du -sk "$dir" 2>/dev/null | awk 'NR == 1 { print $1 }')" || true
  if [[ "$kib" =~ ^[0-9]+$ ]]; then human "$kib"; else echo "(could not be measured)"; fi
}

# The directory behind the compose volume labelled <name> (sqldata, miniodata); empty when there is none.
volume_dir() {
  local volume
  volume="$(docker volume ls -q --filter "label=com.docker.compose.volume=$1" 2>/dev/null | awk 'NR == 1')" || true
  [[ -n "$volume" ]] || return 0
  docker volume inspect -f '{{.Mountpoint}}' "$volume" 2>/dev/null || true
}

# The size of the compose volume labelled <name>, or why it is unknown.
volume_size() {
  local dir
  dir="$(volume_dir "$1")"
  if [[ -n "$dir" ]]; then size_of "$dir"; else echo "(volume not found)"; fi
}

# What is using space on this machine, one line each. Sizes only.
usage_report() {
  local backups line
  printf '  SQL Server volume (the databases):   %s\n' "$(volume_size sqldata)"
  printf '  MinIO volume (the documents):        %s\n' "$(volume_size miniodata)"
  backups="$({ grep -E '^BACKUP_DIR=' "$ENV_FILE" 2>/dev/null || true; } | awk 'NR == 1' | cut -d= -f2-)"
  backups="${backups:-./backups}"
  [[ "$backups" == /* ]] || backups="$REPO_DIR/${backups#./}"
  printf '  backups folder (local backup sets):  %s\n' "$(size_of "$backups")"
  printf '  Docker (images, containers, volumes, build cache):\n'
  if line="$(docker system df --format '{{.Type}}: {{.Size}} ({{.Reclaimable}} reclaimable)' 2>/dev/null)" && [[ -n "$line" ]]; then
    printf '%s\n' "$line" | sed 's/^/    /'
  else
    printf '    (docker system df could not be read)\n'
  fi
}

advice() {
  cat <<'EOF'
What to do:
  - If the SQL Server or MinIO volume is the large part, the disk itself needs to grow (Azure portal: the VM's
    data disk -> Size + performance, then grow the filesystem on the VM).
  - Docker's build cache is the one thing safe to clear: sudo docker builder prune -f --filter until=48h
  - NEVER run docker image prune or docker system prune, and never remove a volume: the pinned MinIO image can no
    longer be downloaded from any registry, and the volumes ARE the databases and documents.
EOF
}

seen=" "
for path in $DISK_PATHS; do
  if [[ ! -e "$path" ]]; then
    echo "disk-space-check: $path does not exist on this machine; skipped"
    continue
  fi
  line="$(df -P -k "$path" | awk 'NR == 2')" || true
  read -r fs blocks used avail pct mount <<< "$line" || true
  pct="${pct%\%}"
  if [[ ! "${pct:-}" =~ ^[0-9]+$ || ! "${blocks:-}" =~ ^[0-9]+$ ]]; then
    echo "disk-space-check: ERROR: could not read df for $path: [$line]" >&2
    problems=$((problems + 1))
    continue
  fi
  pct=$((10#$pct))
  if [[ "$seen" == *" $fs "* ]]; then
    echo "disk-space-check: $path is on $fs, already checked; skipped"
    continue
  fi
  seen+="$fs "

  level=0
  if ((pct >= CRIT)); then level=2; elif ((pct >= WARN)); then level=1; fi

  key="fs${mount//\//_}"
  state="$STATE_DIR/$key"
  last_level=0
  last_sent=0
  if [[ -f "$state" ]]; then
    read -r last_level last_sent < "$state" || true
    [[ "$last_level" =~ ^[012]$ && "$last_sent" =~ ^[0-9]+$ ]] || { last_level=0; last_sent=0; }
  fi

  if ((level == 0)); then
    echo "disk-space-check: $mount OK, ${pct}% used (warning at ${WARN}%)"
    rm -f "$state"
    continue
  fi

  if ((level <= last_level && now - last_sent < REMIND_DAYS * 86400)); then
    echo "disk-space-check: $mount ${pct}% used; already emailed $(((now - last_sent) / 86400)) day(s) ago"
    # A disk that fell from critical to warning keeps its date, so climbing back to critical emails again.
    if ((level < last_level)) && [[ "$DRY_RUN" != "1" ]]; then echo "$level $last_sent" > "$state"; fi
    continue
  fi

  if ((level == 2)); then
    what="$mount is ${pct}% full (critical)"
    consequence="Above ${CRIT}% there is little room left. When the data disk is full, SQL Server stops accepting
writes: every save in the portal fails, and so does the nightly backup."
  else
    what="$mount is ${pct}% full"
    consequence="This is the early warning: nothing is failing yet. The next email comes at ${CRIT}%, or in
${REMIND_DAYS} days if it stays above ${WARN}%."
  fi

  detail="The filesystem mounted at ${mount} (${fs}) is ${pct}% full: $(human "$used") used of $(human "$blocks"), $(human "$avail") free.
The warning line is ${WARN}% and the critical line is ${CRIT}%.

${consequence}

What is using space on this machine:
$(usage_report)

$(advice)"

  echo "disk-space-check: $mount ${pct}% used, level $level: emailing"
  if bash ./scripts/hosting/backup-alert.sh disk "$what" "$detail"; then
    [[ "$DRY_RUN" = "1" ]] || echo "$level $now" > "$state"
  else
    problems=$((problems + 1))
  fi
done

if ((problems > 0)); then
  echo "disk-space-check: ERROR: ${problems} disk(s) could not be checked or reported" >&2
  exit 1
fi
