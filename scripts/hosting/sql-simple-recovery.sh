#!/usr/bin/env bash
# Puts `model` and every portal database in SIMPLE recovery. Idempotent; safe to run at any time, on either server.
#
# WHY SIMPLE. No transaction-log backup is ever taken (the backups are nightly full dumps), so FULL recovery gave no
# point-in-time restore and only made the log grow without bound: the host database's log reached 7.9 GB and the
# weekly restore proof then ran the data disk out of space. SIMPLE lets the log truncate at each checkpoint.
# `model` is the template for every NEW database, so it is set too; otherwise the next office would start in FULL.
#
# WHEN TO RUN IT. After the first migrator run on a fresh server, and after restoring onto a fresh SQL volume
# (a fresh volume has `model` in FULL again, and restored databases keep whatever model they were backed up in).
#
# WHAT IT TOUCHES. `model`, and databases named exactly CaseEvaluation or CaseEvaluation_<slug> that are ONLINE and
# have database_id > 4. Never system databases, never RestoreProof* scratch copies.
#
# USAGE (as root, from the repository root, with sql-server running):
#   scripts/hosting/sql-simple-recovery.sh                    set SIMPLE, CHECKPOINT, print before and after
#   scripts/hosting/sql-simple-recovery.sh --shrink-log 256   also shrink each portal log file to 256 MB
#
# Shrinking is optional and one-off (after a log has already grown); it runs DBCC SHRINKFILE twice with a
# CHECKPOINT between, because a log can only shrink past its active part once that part has been released.
# Env (all optional): ENV_FILE, COMPOSE_FILE, SQL_CONTAINER (default: compose's sql-server container).
set -euo pipefail

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

log() { echo "[$(date -u +%FT%TZ)] $*"; }
fail() { echo "[$(date -u +%FT%TZ)] ERROR: $*" >&2; exit 1; }

shrink_mb=""
case "${1:-}" in
  "") ;;
  --shrink-log)
    shrink_mb="${2:-}"
    [[ "$shrink_mb" =~ ^[0-9]+$ && "$shrink_mb" -ge 1 ]] || fail "--shrink-log needs a size in MB, e.g. --shrink-log 256"
    [[ $# -eq 2 ]] || fail "too many arguments"
    ;;
  *) fail "unknown argument: $1 (expected nothing or --shrink-log <MB>)" ;;
esac

[[ -f "$ENV_FILE" ]] || fail "env file not found: $ENV_FILE"
container="${SQL_CONTAINER:-$(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps -q sql-server 2> /dev/null || true)}"
[[ -n "$container" ]] || fail "the sql-server container is not running"

# T-SQL on stdin, as sa. The sa password is read from the container's own environment and handed to sqlcmd by NAME
# (SQLCMDPASSWORD), so it is on no command line: not docker's, not bash's, not sqlcmd's.
as_sa() { docker exec -i "$container" bash -c "SQLCMDPASSWORD=\"\$MSSQL_SA_PASSWORD\" ${SQLCMD} -S localhost -U sa -C -b -h -1 -W -s '|'"; }

report() {
  echo "-- database | recovery | log_reuse_wait | log MB"
  as_sa <<'SQL'
SET NOCOUNT ON;
SELECT d.name, d.recovery_model_desc, d.log_reuse_wait_desc, SUM(mf.size) * 8 / 1024
FROM sys.databases d
JOIN sys.master_files mf ON mf.database_id = d.database_id AND mf.type_desc = 'LOG'
WHERE d.name = N'model' OR (d.database_id > 4 AND d.state_desc = 'ONLINE'
  AND (d.name = N'CaseEvaluation' OR d.name LIKE N'CaseEvaluation[_]%'))
GROUP BY d.name, d.recovery_model_desc, d.log_reuse_wait_desc
ORDER BY d.name;
SQL
}

log "BEFORE"
report

log "setting SIMPLE recovery on model and every portal database"
as_sa <<SQL
SET NOCOUNT ON;
ALTER DATABASE [model] SET RECOVERY SIMPLE;
DECLARE @db sysname, @log sysname, @s nvarchar(max), @shrink int = ${shrink_mb:-0};
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
  SELECT d.name FROM sys.databases d
  WHERE d.database_id > 4 AND d.state_desc = 'ONLINE' AND (d.name = N'CaseEvaluation' OR d.name LIKE N'CaseEvaluation[_]%');
OPEN c; FETCH NEXT FROM c INTO @db;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @s = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET RECOVERY SIMPLE;';
  EXEC (@s);
  SET @s = N'USE ' + QUOTENAME(@db) + N'; CHECKPOINT;';
  EXEC (@s);
  IF @shrink > 0
  BEGIN
    SELECT TOP 1 @log = mf.name FROM sys.master_files mf WHERE mf.database_id = DB_ID(@db) AND mf.type_desc = 'LOG';
    SET @s = N'USE ' + QUOTENAME(@db) + N'; DBCC SHRINKFILE (' + QUOTENAME(@log, '''') + N', ' + CAST(@shrink AS nvarchar(20))
      + N') WITH NO_INFOMSGS; CHECKPOINT; DBCC SHRINKFILE (' + QUOTENAME(@log, '''') + N', ' + CAST(@shrink AS nvarchar(20))
      + N') WITH NO_INFOMSGS;';
    EXEC (@s);
  END
  PRINT CONCAT(@db, ': SIMPLE');
  FETCH NEXT FROM c INTO @db;
END
CLOSE c; DEALLOCATE c;
SQL

log "AFTER"
report
log "DONE"
