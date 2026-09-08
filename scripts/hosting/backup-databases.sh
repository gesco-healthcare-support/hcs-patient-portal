#!/usr/bin/env bash
# T13 (in-house hosting, 2026-07-09). Back up the host database + every per-office database.
#
# Enumerates CaseEvaluation + CaseEvaluation_* dynamically (via sys.databases) so new offices
# are covered automatically. Runs BACKUP DATABASE inside the sql-server container -- SQL Server
# can only write to its own filesystem -- to /var/opt/mssql/backups, which the prod compose
# bind-mounts to the configurable host destination BACKUP_DIR (a network share on the server, a
# local dir for verification). Old .bak files past the retention window are pruned.
#
# The .bak files contain PHI -> the destination MUST be access-controlled + off-box (IT policy).
# Never commit them (backups/ is git-ignored + docker-ignored).
#
# SCHEDULING: run nightly from the server, e.g. cron:
#   30 1 * * *  cd /opt/hcs-patient-portal && ./scripts/hosting/backup-databases.sh >> /var/log/hcs-backup.log 2>&1
#
# USAGE (from the worktree/deploy root):
#   ./scripts/hosting/backup-databases.sh
# Env (all optional): COMPOSE_FILE, ENV_FILE, SQL_SERVICE, BACKUP_DIR, BACKUP_RETENTION_DAYS.
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-secrets/env.prod}"
SQL_SERVICE="${SQL_SERVICE:-sql-server}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
CONTAINER_BACKUP_DIR="/var/opt/mssql/backups"
STAMP="$(date +%Y%m%d-%H%M%S)"

# SA password from the git-ignored env file (never echoed).
SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
[ -n "$SA_PASSWORD" ] || { echo "ERROR: MSSQL_SA_PASSWORD not found in ${ENV_FILE}" >&2; exit 1; }

mkdir -p "$BACKUP_DIR"

sqlcmd() {
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T "$SQL_SERVICE" \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -b "$@"
}

# App databases: the host DB + every office DB (CaseEvaluation_<slug>).
DBS="$(sqlcmd -h -1 -W -Q \
  "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name = 'CaseEvaluation' OR name LIKE 'CaseEvaluation\_%' ESCAPE '\' ORDER BY name;")"

count=0
for db in $DBS; do
  db="$(echo "$db" | tr -d '\r')"
  [ -n "$db" ] || continue
  target="${CONTAINER_BACKUP_DIR}/${db}_${STAMP}.bak"
  echo "Backing up ${db} -> ${BACKUP_DIR}/${db}_${STAMP}.bak"
  sqlcmd -Q "BACKUP DATABASE [${db}] TO DISK = N'${target}' WITH INIT, FORMAT, STATS = 25;"
  count=$((count + 1))
done

# Retention: drop .bak files older than the window on the host destination.
find "$BACKUP_DIR" -maxdepth 1 -name '*.bak' -type f -mtime "+${RETENTION_DAYS}" -print -delete 2>/dev/null || true

echo "Backup complete: ${count} database(s) at ${STAMP}; dest=${BACKUP_DIR}; retention=${RETENTION_DAYS}d."
