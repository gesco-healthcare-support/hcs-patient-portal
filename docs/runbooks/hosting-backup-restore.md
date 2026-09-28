# Database backup and restore

> Purpose: recover the host database and every per-office database from backup.
> Audience: whoever operates the deployed server.
> Owner: the portal maintainer.
> **Last proven by a human: never. See the warning below.**

## When you need this page

Reach for it when any of these is true:

- A database is corrupt, dropped, or an office's data is wrong and you need a known-good copy.
- A migration or a seed run has damaged data and you are past the point of fixing it forward.
- `hcs-portal-backup-freshness` has emailed that the last backup is older than 26 hours, or the
  last restore proof older than 8 days.
- You are doing the pre-go-live check that the backups you have are actually restorable.

If you are simply taking a backup, that is the "Run" section and not an incident.

## READ THIS BEFORE YOU RELY ON A RESTORE

**No restore has ever been performed on real data by a person.** The maintainer confirmed this on
2026-09-28, at handover.

The automation below *does* include a restore proof: `backup-offbox.sh --verify-restore` performs
a genuine `RESTORE DATABASE` into a scratch name and writes a `last-restore-proof` marker, and
`hcs-portal-backup-verify.timer` is meant to run it weekly. But the scripts existing in this
repository is not evidence that the timers are installed and running on the server, and the
maintainer's answer suggests they may not be.

**So the first thing to do is establish which world you are in.** On the server:

```bash
systemctl list-timers 'hcs-portal-backup*'
systemctl status hcs-portal-backup-verify.service --no-pager
ls -l /var/backups/hcs-portal/last-success /var/backups/hcs-portal/last-restore-proof
```

Expected if the automation is live: three timers listed with a next elapse, and both marker files
present with recent timestamps. If the timers are absent, or `last-restore-proof` is missing or
old, then **you have backups of unknown restorability** and the priority is to prove one restores
into a scratch database before you need it in anger. That procedure is "Verify a backup is
restorable" at the end of this page, and it is safe: it restores under a different name and drops
it, touching nothing live.

Until somebody has done that and dated it, treat every instruction below as untested.

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

**This overwrites the target database. There is no undo once it completes.** Before running it,
take a fresh backup of the database you are about to overwrite, even if you believe it is corrupt:
it is the only way back if the backup you restore turns out to be worse.

### 1. Establish what you are restoring from

```bash
ls -lt "$BACKUP_DIR"/CaseEvaluation_falkinstein_*.bak | head -5
```

Pick deliberately. The newest file is not always the right one: if the damage was caused by a bad
migration at a known time, you want the last backup BEFORE it.

### 2. Confirm the file is readable and see what is inside

```bash
docker compose -f docker-compose.prod.yml --env-file secrets/env.prod exec -T sql-server \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<MSSQL_SA_PASSWORD>" -C -b \
  -Q "RESTORE HEADERONLY FROM DISK = N'/var/opt/mssql/backups/CaseEvaluation_falkinstein_<stamp>.bak';"
```

Expect one row with a `BackupFinishDate` you recognise. An error here means a corrupt or truncated
file: **stop, and pick a different backup.** Do not proceed to overwrite a live database using a
file you could not read.

### 3. Stop the applications

```bash
docker compose -f docker-compose.prod.yml --env-file secrets/env.prod stop authserver api
```

Open connections block a restore. **Never `down -v`** -- that destroys the named volumes and with
them every database and the DataProtection keys.

### 4. Restore

```bash
docker compose -f docker-compose.prod.yml --env-file secrets/env.prod exec -T sql-server \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<MSSQL_SA_PASSWORD>" -C -b \
  -Q "RESTORE DATABASE [CaseEvaluation_falkinstein] FROM DISK = N'/var/opt/mssql/backups/CaseEvaluation_falkinstein_<stamp>.bak' WITH REPLACE, RECOVERY;"
```

Expect `RESTORE DATABASE successfully processed N pages`. `-b` makes sqlcmd exit non-zero on
error, so check the exit status rather than reading the text.

**If it fails part-way**, the database is left in a restoring or suspect state and the application
will not start against it. Do not start the services. Either re-run the restore with a different
backup file, or restore the pre-restore backup you took in step 0.

### 5. Start the applications and check

```bash
docker compose -f docker-compose.prod.yml --env-file secrets/env.prod start authserver api
curl -sk -o /dev/null -w '%{http_code}\n' https://health.<BASE_DOMAIN>/health-status
```

Expect `200`. Then sign in to the affected office and confirm the data you expected to recover is
present, because a successful restore of the wrong file is still a successful restore.

### Full restore

Same sequence, restoring the host `CaseEvaluation` database and every `CaseEvaluation_<slug>`
database while the applications are stopped. Restore the host database first: the office
connection strings are stored on the tenant rows in it, so an office database restored against a
host database that does not know about that office is not reachable.

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

## Escalation

- Before step 4 of a restore, tell the portal maintainer which database and which backup file:
  the overwrite has no undo.
- At once, with the services left stopped, if step 2 finds no readable backup or a restore fails
  part-way. Send the file name, the command and its full output.
- If the office is not serving the recovered data within 30 minutes of step 5, escalate with the
  same detail and the output of
  `docker compose -f docker-compose.prod.yml --env-file secrets/env.prod logs --tail=200 api`.
- If a restore proof or the freshness check reports a failure, escalate within the working day:
  it means the backups may not be restorable when they are needed.
