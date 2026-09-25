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

SUPERSEDED on the server by the systemd timers in "Off-box schedule and alerts" below; kept for a machine
without systemd.

```text
30 1 * * *  cd /opt/hcs-patient-portal && ./scripts/hosting/backup-databases.sh >> /var/log/hcs-backup.log 2>&1
```

Suggested retention: 14 daily on-box + weekly copies retained ~8 weeks off-box (finalize with IT).

## Off-box schedule and alerts (#945)

On the server, `scripts/hosting/backup-offbox.sh` runs this dump, ships it and the MinIO documents off-box, and
is scheduled by systemd:

| Unit | When | What |
| --- | --- | --- |
| `hcs-portal-backup.timer` | 01:30 nightly | off-box backup |
| `hcs-portal-backup-verify.timer` | 02:30 Sunday | the same, plus a restore into a scratch database |
| `hcs-portal-backup-freshness.timer` | 09:00 daily | emails if the backup or the restore proof has gone stale |
| `hcs-portal-backup-alert@.service` | on failure | emails when either backup unit fails (`OnFailure=`) |

Alerts go to `BACKUP_ALERT_RECIPIENTS` in `secrets/env.prod` (addresses separated by `;` or `,`), through the
portal's own SMTP relay settings in the same file. A blank list is an error in the journal and no email.

The freshness check alerts when the last successful backup is older than 26 hours or the last successful restore
proof older than 8 days. `backup-offbox.sh` writes the two markers it reads (`/var/backups/hcs-portal/last-success`
and `last-restore-proof`) only after a run fully succeeds.

Install or update (as root, from the repository root):

```bash
cp scripts/hosting/systemd/hcs-portal-backup*.service scripts/hosting/systemd/hcs-portal-backup*.timer /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now hcs-portal-backup.timer hcs-portal-backup-verify.timer hcs-portal-backup-freshness.timer
BACKUP_ALERT_DRY_RUN=1 bash scripts/hosting/backup-alert.sh stale "install check" "Dry run from the install."
systemctl start hcs-portal-backup-verify.service
```

The dry run prints the message and recipients without sending. Starting the verify service once writes both
markers, so the next morning's check does not report a missing restore proof.

To prove an alert end to end, run one backup with a destination that cannot be reached and watch for the email:

```bash
systemd-run --unit=hcs-backup-alert-test --property=OnFailure=hcs-portal-backup-alert@%n.service \
  --setenv=REMOTE_HOST=portalbackup@192.0.2.1 --working-directory=/home/apadmin/hcs-patient-portal \
  /bin/bash scripts/hosting/backup-offbox.sh
```

(`192.0.2.1` is a documentation address that answers nothing, so the ship step fails.)

## After cutover

`backup-offbox.sh` retires when the portal moves to Azure: its destination is a private address the hosted portal
cannot reach. The Azure design replaces it -- the databases on Azure SQL with point-in-time restore, documents in
blob storage with versioning and soft delete, and exports in a separate backups storage account. Until the
cutover, the timers and alerts above stay in force.

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
