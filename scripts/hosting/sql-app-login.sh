#!/usr/bin/env bash
# The application's own SQL Server login, so the portal does not connect as sa (2026-10-13). For the Azure VM's
# FRESH start; the office server keeps sa (compose falls back to it when SQL_APP_LOGIN is unset).
#
# WHY IT MUST HAPPEN BEFORE THE FIRST OFFICE. Each office's connection string is built from ConnectionStrings:Default
# and STORED on the tenant row in the host database when the practice is created. Whatever login the app uses then is
# baked into every office, and into every backup of the host database. Choosing it later means rewriting those rows.
#
# WHAT THE LOGIN GETS, AND WHY THAT IS THE MINIMUM.
#   - Server-wide: CREATE ANY DATABASE, and nothing else. Practice creation runs EF's MigrateAsync from the API, which
#     creates the office database (OfficeDatabaseProvisioner); the migrator creates the host database the same way.
#     NOT sysadmin, and NOT dbcreator (which may also alter or drop ANY database).
#   - Per database: OWNER. A database's creator is its owner, so on a fresh start the login owns every portal database
#     it creates: schema migrations, Hangfire's schema in the host database, and data, all as dbo. --adopt makes it
#     the owner of databases someone else created, e.g. after a restore run as sa.
#   - sa stays for the operator only: the SQL container's healthcheck, the backup scripts and restores. The
#     application containers no longer receive the sa password at all.
# Honest limit: owning every portal database means the login can read all portal data, as sa could. What it can no
# longer do is administer the server: create logins, change configuration, reach the OS, or touch other databases.
#
# USAGE (as root on the VM, from the repository root, with sql-server running):
#   scripts/hosting/sql-app-login.sh            create the login if missing (idempotent), then verify its rights
#   scripts/hosting/sql-app-login.sh --adopt    make it the owner of CaseEvaluation and every CaseEvaluation_* database
#
# It NEVER changes an existing login's password. Changing it means rewriting every office's stored connection string
# in the same step (DEPLOYMENT runbook, "rotation procedure"); doing only half breaks every office. So a password in
# env.prod that no longer matches the login is reported as an error, not "fixed".
#
# Settings (secrets/env.prod): SQL_APP_LOGIN, SQL_APP_PASSWORD. The app password reaches SQL Server on stdin or in the
# container's environment, never on a command line.
# Env (all optional): ENV_FILE, COMPOSE_FILE, SQL_CONTAINER (default: compose's sql-server container).
set -euo pipefail

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

log() { echo "[$(date -u +%FT%TZ)] $*"; }
fail() { echo "[$(date -u +%FT%TZ)] ERROR: $*" >&2; exit 1; }
env_value() {
  local key="$1"
  { grep -E "^${key}=" "$ENV_FILE" || true; } | head -1 | cut -d= -f2- | tr -d '\r' \
    | sed -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/"
}

mode="create"
case "${1:-}" in
  "") ;;
  --adopt) mode="adopt" ;;
  *) fail "unknown argument: $1 (expected nothing or --adopt)" ;;
esac
[[ $# -le 1 ]] || fail "too many arguments"

[[ -f "$ENV_FILE" ]] || fail "env file not found: $ENV_FILE"
login="$(env_value SQL_APP_LOGIN)"
password="$(env_value SQL_APP_PASSWORD)"
[[ "${login,,}" != "sa" ]] || fail "SQL_APP_LOGIN must not be sa: the point is a login that is NOT the administrator"
[[ "$login" =~ ^[A-Za-z][A-Za-z0-9_]{2,63}$ ]] || fail "SQL_APP_LOGIN is missing or not a plain login name (letters, digits, underscore)"
# Length and character classes, plus nothing that would break the connection string (;) or quoting (' " { }).
(( ${#password} >= 16 )) || fail "SQL_APP_PASSWORD must be at least 16 characters"
[[ "$password" =~ [A-Z] && "$password" =~ [a-z] && "$password" =~ [0-9] ]] \
  || fail "SQL_APP_PASSWORD needs upper case, lower case and digits (SQL Server's password policy)"
[[ "$password" != *[\;\'\"\{\}[:space:]]* ]] \
  || fail "SQL_APP_PASSWORD must not contain ; ' \" { } or spaces: it is placed inside a connection string"

container="${SQL_CONTAINER:-$(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps -q sql-server 2> /dev/null || true)}"
[[ -n "$container" ]] || fail "the sql-server container is not running"

# T-SQL on stdin, as sa. The sa password is read from the container's own environment (as its healthcheck does).
as_sa() { docker exec -i "$container" bash -c "${SQLCMD} -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -C -b -h -1 -W"; }
# One query as the app login. Its password travels in SQLCMDPASSWORD, passed through from this process's environment.
as_app() {
  SQLCMDPASSWORD="$password" docker exec -i -e SQLCMDPASSWORD "$container" \
    "$SQLCMD" -S localhost -U "$login" -C -b -h -1 -W -Q "SET NOCOUNT ON; $1"
}

if [[ "$mode" == create ]]; then
  log "creating SQL login ${login} if it does not exist (CREATE ANY DATABASE only)"
  as_sa <<SQL
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'${login}')
BEGIN
  CREATE LOGIN [${login}] WITH PASSWORD = N'${password}', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF,
    DEFAULT_DATABASE = [master];
  PRINT 'created';
END
ELSE
  PRINT 'exists';
GRANT CREATE ANY DATABASE TO [${login}];
SQL
else
  log "making ${login} the owner of every portal database"
  as_sa <<SQL
SET NOCOUNT ON;
DECLARE @db sysname, @sql nvarchar(max);
DECLARE dbs CURSOR LOCAL FAST_FORWARD FOR
  SELECT name FROM sys.databases WHERE name = N'CaseEvaluation' OR name LIKE N'CaseEvaluation[_]%';
OPEN dbs;
FETCH NEXT FROM dbs INTO @db;
WHILE @@FETCH_STATUS = 0
BEGIN
  -- A login that is already a USER in the database cannot become its owner: drop that user first. It owns nothing
  -- here (dbo owns the schemas), and as owner the login is dbo anyway.
  SET @sql = N'USE ' + QUOTENAME(@db) + N'; IF EXISTS (SELECT 1 FROM sys.database_principals p JOIN sys.server_principals s'
    + N' ON p.sid = s.sid WHERE s.name = N''${login}'' AND p.name <> N''dbo'') BEGIN DECLARE @u sysname = (SELECT p.name'
    + N' FROM sys.database_principals p JOIN sys.server_principals s ON p.sid = s.sid WHERE s.name = N''${login}'');'
    + N' DECLARE @drop nvarchar(300) = N''DROP USER '' + QUOTENAME(@u); EXEC (@drop); END';
  EXEC sp_executesql @sql;
  SET @sql = N'ALTER AUTHORIZATION ON DATABASE::' + QUOTENAME(@db) + N' TO [${login}]';
  EXEC sp_executesql @sql;
  PRINT CONCAT('owner of ', @db, ' -> ${login}');
  FETCH NEXT FROM dbs INTO @db;
END
CLOSE dbs;
DEALLOCATE dbs;
SQL
fi

# Verify as the login itself: it can sign in with the password in env.prod, it may create databases, and it is not an
# administrator. A failed sign-in here usually means env.prod and the existing login disagree: see the header.
rights="$(as_app "SELECT CONCAT(IS_SRVROLEMEMBER('sysadmin'), ',', IS_SRVROLEMEMBER('dbcreator'), ',', HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE'), ',', HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY LOGIN'), ',', HAS_PERMS_BY_NAME(NULL, NULL, 'CONTROL SERVER'))" 2>&1)" \
  || fail "${login} could not sign in with the password in ${ENV_FILE}. If the login already existed, its password differs: do NOT change it here (see the header)."
rights="$(printf '%s' "$rights" | tr -d '[:space:]')"
[[ "$rights" == "0,0,1,0,0" ]] \
  || fail "${login} has the wrong server rights (sysadmin,dbcreator,CREATE ANY DATABASE,ALTER ANY LOGIN,CONTROL SERVER = ${rights}; want 0,0,1,0,0)"
log "verified: ${login} signs in, may create databases, and is not sysadmin, dbcreator, a login administrator or in control of the server"
