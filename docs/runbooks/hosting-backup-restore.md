# Database backup and restore

> Purpose: recover the host database and every per-office database from backup.
> Audience: whoever operates the deployed server.
> Owner: the portal maintainer.
> **Last proven: 2026-10-07 -- Tier-1 restore rehearsal (every backup restores + documents match
> the database, non-destructive). A full stack-up recovery on a clean machine has still never been
> done. See the warning below.**

## When you need this page

Reach for it when any of these is true:

- A database is corrupt, dropped, or an office's data is wrong and you need a known-good copy.
- A migration or a seed run has damaged data and you are past the point of fixing it forward.
- `hcs-portal-backup-freshness` has emailed that the last backup is older than 26 hours, or the
  last restore proof older than 8 days.
- You are doing the pre-go-live check that the backups you have are actually restorable.

If you are simply taking a backup, that is the "Run" section and not an incident.

## READ THIS BEFORE YOU RELY ON A RESTORE

**A Tier-1 restore rehearsal was performed on 2026-10-07** (non-destructive; nothing live was
stopped, recreated or overwritten). For the backup set stamped `20261007-013018` it proved:

- a complete matched set exists on the off-box destination (the backup host and directory in
  `REMOTE_HOST` / `REMOTE_DIR` in `backup-offbox.sh`): the host database, every office database,
  and the MinIO archive, all carrying the same run stamp, byte-sizes identical to the copies on
  the portal server;
- every database `.bak` in that set restores into a scratch database -- host `CaseEvaluation`
  (113 user tables), `CaseEvaluation_falkinstein` (76), `CaseEvaluation_test-office` (76);
- the MinIO archive opens and its objects match the rows that reference them: all 55 distinct
  document and packet object hashes referenced by the falkinstein database are present in the
  same-run archive, with **zero dangling references** (one archive object, the host branding logo,
  is referenced by no appointment row, which is expected). See "Verify documents match the
  database" below for the method;
- `secrets/` -- which is in NO automated backup -- exists in a current, complete off-box copy on
  the maintainer's workstation (exact path in the local deploy runbook), byte-size matching the
  live box, including `dataprotection.pfx`, `openiddict.pfx`, `env.prod` and `admin-passwords/`.
  That copy is MANUAL: a secret changed on the server after that date is not captured until
  someone re-pulls it.

**What is still unproven (Tier 2):** that the whole stack comes back up from backup on a clean
machine and a user can sign in and download a document. Until that is done, a restore is proven
_readable and internally consistent_, not proven to _recover the business_.

**History, kept:** no full restore on real data had ever been performed by a person before
2026-10-07; the maintainer confirmed this on 2026-09-28, at handover.

The automation below _does_ include a restore proof: `backup-offbox.sh --verify-restore` performs
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

| Unit                                | When          | What                                                     |
| ----------------------------------- | ------------- | -------------------------------------------------------- |
| `hcs-portal-backup.timer`           | 01:30 nightly | off-box backup                                           |
| `hcs-portal-backup-verify.timer`    | 02:30 Sunday  | the same, plus a restore into a scratch database         |
| `hcs-portal-backup-freshness.timer` | 09:00 daily   | emails if the backup or the restore proof has gone stale |
| `hcs-portal-backup-alert@.service`  | on failure    | emails when either backup unit fails (`OnFailure=`)      |

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

## Before a deploy that carries the scoped MinIO credential

The application no longer connects to MinIO as root. `docker-compose.prod.yml` now reads
`MINIO_APP_ACCESS_KEY` and `MINIO_APP_SECRET_KEY` from `secrets/env.prod`, and `minio-init` creates that user with a
policy (`portal-app-rw`) limited to the `MINIO_BUCKET_NAME` bucket. A deploy does not edit `secrets/env.prod`, so
add the keys by hand first. Without them compose refuses to run (`MINIO_APP_ACCESS_KEY is not set`) rather than
starting the app with a blank or root credential.

```bash
cd /home/apadmin/hcs-patient-portal
# new values, never reused from anywhere; the access key must differ from MINIO_ROOT_USER
printf 'MINIO_APP_ACCESS_KEY=portal-app
MINIO_APP_SECRET_KEY=%s
' "$(openssl rand -hex 24)" >> secrets/env.prod
grep -c '^MINIO_APP_' secrets/env.prod        # must print 2
scripts/hosting/dc.sh up -d minio-init        # creates the user and policy; exits 0
scripts/hosting/dc.sh logs minio-init         # expect "Attached policy portal-app-rw"
```

Then deploy as usual. Do not change `MINIO_ROOT_PASSWORD`; `minio-init`, `backup-offbox.sh` and the Case Tracker
credential are separate from this. Re-running `minio-init` is safe: it re-applies the same policy and user and
logs `Policy already attached` the second time.

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
differ per database. Use a clearly scratch name like `CaseEvaluation_verify` and never a live
database name with `REPLACE`.)

### Verify documents match the database (the integrity check)

A `.bak` that restores proves the database is readable. It does NOT prove the documents it points
at still exist. The appointment documents and packets live in MinIO, not in the database; each row
in `AppAppointmentDocuments` and `AppAppointmentPackets` holds a `BlobName` object key. To prove a
restore is whole you must confirm those objects are present in the MinIO archive **from the same
run** (same stamp). Match on the object key's terminal hash (the final `<hex>.pdf` / `<hex>.png`),
which is stable; ABP prefixes the stored key with a container and tenant path, so the full strings
will not be equal.

```bash
# On the off-box host (REMOTE_HOST/REMOTE_DIR from backup-offbox.sh), list the object basenames.
# The far end is Windows and ships a usable tar; use the same SSH key the backup uses.
ssh "$REMOTE_HOST" "tar -tzf $REMOTE_DIR/minio/minio_<stamp>.tar.gz" \
  | tr -d '\r' | grep 'case-evaluation-documents/' | grep -vE '/$' | sed 's#.*/##' | sort -u > arch.txt
```

```sql
-- In the restored office database, the object keys its rows reference:
SELECT BlobName FROM [CaseEvaluation_verify].dbo.AppAppointmentDocuments
UNION ALL SELECT BlobName FROM [CaseEvaluation_verify].dbo.AppAppointmentPackets;
```

Reduce each `BlobName` to its terminal hash (`sed 's#.*/##'`), sort unique, and `comm -12` it with
`arch.txt`. Every key a row references must appear in the archive; any that do not are dangling
references and the restore is not whole. (An archive object referenced by no row -- e.g. the host
branding logo -- is fine and expected.) The 2026-10-07 rehearsal ran exactly this and matched 55
of 55 for falkinstein with zero missing.

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
