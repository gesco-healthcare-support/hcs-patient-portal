# Database backup + restore (T13)

Nightly native SQL Server backups of the host database plus every per-office database.

## What it does

`scripts/hosting/backup-databases.sh` enumerates `CaseEvaluation` + `CaseEvaluation_*` (via
`sys.databases`, so new offices are covered automatically) and runs `BACKUP DATABASE` inside the
sql-server container to `/var/opt/mssql/backups`, which `docker-compose.prod.yml` bind-mounts to
the host `BACKUP_DIR`. Files are named `<db>_<YYYYMMDD-HHMMSS>.bak`. Backups older than
`BACKUP_RETENTION_DAYS` are pruned.

## PHI + destination

The `.bak` files contain PHI. Point `BACKUP_DIR` at an access-controlled, OFF-BOX destination (a
mounted network share per IT policy). Never commit them (`backups/` is git-ignored + docker-ignored).
The SQL container's `mssql` user (uid 10001) must be able to write `BACKUP_DIR` (chown/chmod on Linux).

## Run

```bash
./scripts/hosting/backup-databases.sh
```

Env (all optional): `BACKUP_DIR` (dest), `BACKUP_RETENTION_DAYS` (default 14), `COMPOSE_FILE`,
`ENV_FILE`, `SQL_SERVICE`. On Windows Git Bash prefix with `MSYS_NO_PATHCONV=1` so the container
sqlcmd path is not mangled; on the Linux server it is not needed.

## Schedule (server, cron)

```
30 1 * * *  cd /opt/hcs-patient-portal && ./scripts/hosting/backup-databases.sh >> /var/log/hcs-backup.log 2>&1
```

Suggested retention: 14 daily on-box + weekly copies retained ~8 weeks off-box (finalize with IT).

## Restore one database

```bash
docker compose -f docker-compose.prod.yml --env-file secrets/env.prod exec -T sql-server \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<MSSQL_SA_PASSWORD>" -C -b \
  -Q "RESTORE DATABASE [CaseEvaluation_falkinstein] FROM DISK = N'/var/opt/mssql/backups/CaseEvaluation_falkinstein_<stamp>.bak' WITH REPLACE, RECOVERY;"
```

For a full restore, stop the app services (`docker compose ... stop authserver api`) first so no
connections block the restore, restore each database, then start them again. NEVER `down -v`.

## Verify a backup is restorable

Restore into a scratch name (uses the same logical file names as the source) and drop it:

```sql
RESTORE DATABASE [CaseEvaluation_verify]
  FROM DISK = N'/var/opt/mssql/backups/<file>.bak'
  WITH MOVE 'CaseEvaluation'     TO '/var/opt/mssql/data/verify.mdf',
       MOVE 'CaseEvaluation_log' TO '/var/opt/mssql/data/verify_log.ldf',
       REPLACE, RECOVERY;
-- spot-check, then:
DROP DATABASE [CaseEvaluation_verify];
```

(Confirm the logical names with `RESTORE FILELISTONLY FROM DISK = N'<file>.bak';` -- they may
differ per database.)
