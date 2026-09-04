#!/usr/bin/env bash
# Off-box backup (2026-08-21). Ships the database dumps AND the MinIO objects to a second
# machine, because until now every copy lived on the same filesystem as the data it protected.
#
# WHY THIS EXISTS ON TOP OF backup-databases.sh
#   That script is correct and is reused verbatim here -- it enumerates the host DB plus every
#   per-office DB from sys.databases, so new offices are covered without anyone remembering.
#   What it never had was (a) a schedule, (b) an off-box destination despite its own header
#   requiring one, and (c) any coverage of MinIO, where the documents live.
#
# WHY IT RUNS AS ROOT
#   The .bak files are written by the SQL Server container as uid 10001, mode 640. The host
#   user (apadmin, uid 1001) is "other" on those files and cannot READ them, so copying as
#   apadmin fails. The same ownership breaks deletion: the backups directory is owned by
#   10001, so the retention prune in backup-databases.sh has been failing silently -- its
#   find ends in "2>/dev/null || true", so nothing ever reported it. Running as root avoids
#   both. Do not "fix" this by loosening permissions on a directory full of PHI.
#
# WHAT IT DOES NOT PROTECT AGAINST
#   The destination is another VM on the SAME VMware host as this one. That covers guest-level
#   loss -- corruption, deletion, ransomware in one guest, a rebuilt VM -- but NOT host or
#   datastore failure, where both guests die together. This is the interim routine committed to
#   on 2026-08-17, not disaster recovery. True off-site remains open.
#
# SCALING LIMIT
#   MinIO is archived whole each run (8 MiB today). That is fine now and will not be once the
#   Case Tracker fills its bucket. When the archive gets big, switch to an incremental transport
#   -- rsync is not available on the Windows destination, so that likely means mc mirror to a
#   second MinIO or an S3 target rather than tar+scp.
#
# USAGE
#   sudo ./scripts/hosting/backup-offbox.sh              # full run
#   sudo ./scripts/hosting/backup-offbox.sh --verify-restore   # also prove a dump restores
#
# Env (all optional):
#   REMOTE_HOST (default portalbackup@192.168.101.41)
#   REMOTE_DIR  (default C:/PortalBackups)
#   SSH_KEY     (default /home/apadmin/.ssh/backup_offbox_ed25519)
#   REMOTE_RETENTION_DAYS (default 30)   BACKUP_RETENTION_DAYS (default 14, local)
set -euo pipefail

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"
STAGING="${STAGING:-/var/backups/hcs-portal}"
REMOTE_HOST="${REMOTE_HOST:-portalbackup@192.168.101.41}"
REMOTE_DIR="${REMOTE_DIR:-C:/PortalBackups}"
SSH_KEY="${SSH_KEY:-/home/apadmin/.ssh/backup_offbox_ed25519}"
REMOTE_RETENTION_DAYS="${REMOTE_RETENTION_DAYS:-30}"
DOCKER_NET="${DOCKER_NET:-hcs-patient-portal_default}"
STAMP="$(date +%Y%m%d-%H%M%S)"
SSH_OPTS=(-i "$SSH_KEY" -o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)

log() { echo "[$(date -u +%FT%TZ)] $*"; }
fail() { echo "[$(date -u +%FT%TZ)] ERROR: $*" >&2; exit 1; }

[ -f "$ENV_FILE" ] || fail "env file not found: $ENV_FILE"
[ -r "$SSH_KEY" ] || fail "ssh key not readable: $SSH_KEY (are you root?)"

# ---------------------------------------------------------------- 1. databases
log "1/6 dumping databases (host + every office) via backup-databases.sh"
# Invoked via bash, not ./ -- the repo file has no execute bit and even root needs one to exec.
BACKUP_DIR="$BACKUP_DIR" bash ./scripts/hosting/backup-databases.sh

# The prune inside that script cannot delete container-owned files. Do it here, loudly.
log "1b/6 pruning local dumps older than ${BACKUP_RETENTION_DAYS:-14}d (root, errors NOT suppressed)"
find "$BACKUP_DIR" -maxdepth 1 -name '*.bak' -type f -mtime "+${BACKUP_RETENTION_DAYS:-14}" -print -delete

# ---------------------------------------------------------------- 2. minio
log "2/6 mirroring MinIO buckets to staging"
mkdir -p "$STAGING/minio"
MINIO_USER="$(grep -E '^MINIO_ROOT_USER=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
MINIO_PASS="$(grep -E '^MINIO_ROOT_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
[ -n "$MINIO_USER" ] && [ -n "$MINIO_PASS" ] || fail "MinIO credentials not found in $ENV_FILE"

for bucket in case-evaluation-documents case-tracker-documents; do
  # Credentials go in via MC_HOST_* so nothing is written to an mc config file on disk.
  docker run --rm --network "$DOCKER_NET" \
    -e "MC_HOST_m=http://${MINIO_USER}:${MINIO_PASS}@minio:9000" \
    -v "$STAGING/minio:/backup" \
    minio/mc:latest mirror --overwrite --remove "m/${bucket}" "/backup/${bucket}" >/dev/null \
    || fail "mc mirror failed for ${bucket}"
  log "     mirrored ${bucket}"
done

log "2b/6 archiving the mirror"
ARCHIVE="$STAGING/minio_${STAMP}.tar.gz"
tar -czf "$ARCHIVE" -C "$STAGING/minio" . || fail "tar failed"
log "     $(du -h "$ARCHIVE" | cut -f1) -> $(basename "$ARCHIVE")"

# ---------------------------------------------------------------- 3. ship
log "3/6 shipping to ${REMOTE_HOST}:${REMOTE_DIR}"
ssh "${SSH_OPTS[@]}" "$REMOTE_HOST" \
  "if not exist \"${REMOTE_DIR//\//\\}\\db\" mkdir \"${REMOTE_DIR//\//\\}\\db\"" >/dev/null 2>&1 || true
ssh "${SSH_OPTS[@]}" "$REMOTE_HOST" \
  "if not exist \"${REMOTE_DIR//\//\\}\\minio\" mkdir \"${REMOTE_DIR//\//\\}\\minio\"" >/dev/null 2>&1 || true

# Only this run's dumps, so the transfer does not grow with the retention window.
shipped=0
while IFS= read -r f; do
  scp -q "${SSH_OPTS[@]}" "$f" "${REMOTE_HOST}:${REMOTE_DIR}/db/$(basename "$f")" \
    || fail "scp failed for $(basename "$f")"
  shipped=$((shipped + 1))
done < <(find "$BACKUP_DIR" -maxdepth 1 -name "*_${STAMP}.bak" -type f)
[ "$shipped" -gt 0 ] || fail "no dumps matched this run's stamp ${STAMP} -- nothing shipped"

scp -q "${SSH_OPTS[@]}" "$ARCHIVE" "${REMOTE_HOST}:${REMOTE_DIR}/minio/$(basename "$ARCHIVE")" \
  || fail "scp failed for the MinIO archive"
log "     shipped ${shipped} dump(s) + 1 MinIO archive"

# ---------------------------------------------------------------- 4. verify arrival
log "4/6 verifying sizes at the destination"
for f in $(find "$BACKUP_DIR" -maxdepth 1 -name "*_${STAMP}.bak" -type f) "$ARCHIVE"; do
  base="$(basename "$f")"
  local_size="$(stat -c %s "$f")"
  case "$base" in *.bak) sub=db ;; *) sub=minio ;; esac
  remote_size="$(ssh "${SSH_OPTS[@]}" "$REMOTE_HOST" \
    "powershell -NoProfile -Command \"(Get-Item '${REMOTE_DIR}/${sub}/${base}').Length\"" 2>/dev/null | tr -d '\r')"
  [ "$local_size" = "$remote_size" ] \
    || fail "size mismatch for ${base}: local=${local_size} remote=${remote_size:-missing}"
done
log "     all sizes match"

# ---------------------------------------------------------------- 5. remote retention
log "5/6 pruning destination beyond ${REMOTE_RETENTION_DAYS}d"
ssh "${SSH_OPTS[@]}" "$REMOTE_HOST" \
  "powershell -NoProfile -Command \"Get-ChildItem '${REMOTE_DIR}' -Recurse -File | Where-Object { \$_.LastWriteTime -lt (Get-Date).AddDays(-${REMOTE_RETENTION_DAYS}) } | Remove-Item -Force -ErrorAction Stop; 'PRUNED'\"" \
  || fail "remote prune failed"

# Staging archives are transient once shipped.
find "$STAGING" -maxdepth 1 -name 'minio_*.tar.gz' -type f -mtime +2 -print -delete

# ---------------------------------------------------------------- 6. optional restore proof
if [ "${1:-}" = "--verify-restore" ]; then
  log "6/6 restore proof: loading the newest dump into a scratch database"
  SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
  newest="$(find "$BACKUP_DIR" -maxdepth 1 -name "CaseEvaluation_${STAMP}.bak" -type f | head -1)"
  [ -n "$newest" ] || newest="$(find "$BACKUP_DIR" -maxdepth 1 -name "*_${STAMP}.bak" -type f | head -1)"
  [ -n "$newest" ] || fail "no dump from this run to verify"
  cbase="/var/opt/mssql/backups/$(basename "$newest")"
  scratch="RestoreProof_${STAMP}"
  sqlc() { docker compose -f docker-compose.prod.yml --env-file "$ENV_FILE" exec -T sql-server \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -b "$@"; }
  # MOVE to fresh files so the scratch copy cannot touch the live database's data files.
  files="$(sqlc -h -1 -W -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK=N'${cbase}';" | awk '{print $1}' | grep -v '^$')"
  move=""
  i=0
  for lf in $files; do
    i=$((i + 1))
    move="${move} MOVE N'${lf}' TO N'/var/opt/mssql/data/${scratch}_${i}.dat',"
  done
  sqlc -Q "RESTORE DATABASE [${scratch}] FROM DISK=N'${cbase}' WITH ${move} RECOVERY, REPLACE;" \
    || fail "RESTORE FAILED -- the backup is not trustworthy"
  rows="$(sqlc -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM [${scratch}].sys.tables;" | tr -d '\r ')"
  sqlc -Q "DROP DATABASE [${scratch}];" >/dev/null
  log "     RESTORE PROVED: ${rows} tables read back, scratch database dropped"
else
  log "6/6 restore proof skipped (pass --verify-restore to run it)"
fi

log "DONE ${STAMP}: ${shipped} dump(s) + MinIO archive off-box at ${REMOTE_HOST}:${REMOTE_DIR}"
