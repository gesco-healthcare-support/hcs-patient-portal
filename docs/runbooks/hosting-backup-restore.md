# Database backup and restore

> Purpose: recover the host database and every per-office database from backup.
> Audience: whoever operates the deployed server.
> Owner: the portal maintainer.
> **Last proven: 2026-10-13 -- Tier-2 restore rehearsal: the whole system was stood up from one
> matched backup set on a machine that is not the server, a user signed in, opened an appointment
> and downloaded a packet. Tier 1 (every backup restores, documents match) was proven 2026-10-07.
> Recovery onto a new Azure VM from the backup account is written below but not yet rehearsed.**

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

**A Tier-2 restore rehearsal passed on 2026-10-13.** The backup set stamped `20261013-013222` was
copied read-only off the server to a separate machine and restored into a throwaway compose project.
The application images were built from the commit the server runs, and the secrets came from the
off-box `secrets/` copy. Outbound mail and every Case Tracker setting were neutralised first, and the
ports were bound to that machine's loopback address. It proved:

- the host database and both office databases restore (113 / 76 / 76 user tables), and the migrator
  reaches every office through its stored connection string;
- **every row that references a stored object finds it, matched on the EXACT object key**: 69 of 69
  (documents, packets and office logos). Zero missing, and zero objects that no row references;
- the migrator rewrites the sign-in client's redirect addresses to the new base domain, so a restore
  under a different domain signs in;
- the off-box `secrets/` copy is restore-complete for that release: both certificates load with
  their passphrases, and the stored connection strings accept the SQL password in it;
- a person signed in, opened an appointment and downloaded a packet;
- nothing left the machine: no mail was sent and the Case Tracker outbox held every row.

**Times measured:** about 5 minutes from an empty project to ready once images exist (databases
about 70 s, documents about 15 s, migrator 46 s, applications about 2 min). Building the five images
from source took about an hour, and one attempt failed on a public npm registry timeout and passed on
retry.

**Learned:**

- **Office connection strings are stored in the host database**, built from the SQL credentials in
  force when each office was created. So a restore must use the same `MSSQL_SA_PASSWORD` as the
  source, and changing that password needs the stored strings updated in the same step.
- Upload malware scanning was not running in that rehearsal, so uploads remain unexercised there.

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
cannot reach. Until the cutover, the timers and alerts above stay in force on the office server.

### On an Azure VM running the same compose stack: `backup-azure-blob.sh`

The first Azure deployment runs the same compose stack on one VM, so SQL Server and MinIO are still containers
and still need an off-server copy. `scripts/hosting/backup-azure-blob.sh` provides it, and **replaces**
`backup-offbox.sh` on that machine. Install one of the two per machine, never both.

- **Databases**: each night's `.bak` of the host database and every office database goes to the `db-backups`
  container under `<run stamp>/`, with `MANIFEST.sha256` uploaded last. **A run folder without a manifest is
  incomplete: do not restore from it.**
- **Documents**: each MinIO bucket is copied to the `documents` container under `<bucket>/`. Only new or
  changed objects are sent.
- **Nothing is ever deleted in Azure.** The script only copies; how long anything is kept is set on the storage
  account (versioning, soft delete, lifecycle tiers). `tests/python/test_backup_azure_blob.py` fails if a
  destructive command appears in the script.
- **No storage key exists.** The script signs in as the VM's managed identity, which needs Storage Blob Data
  Contributor on both containers.

**Proven so far: against the Azurite emulator only (2026-10-13)**, with a real SQL Server, a real MinIO and the
pinned rclone image -- dry run, nightly run, a document deleted at the source staying in the backup, only new or
changed objects being sent, and the weekly restore proof reading the host dump back and restoring it. The managed
identity path cannot be exercised outside Azure, so **the first run on the VM is the first real proof**: run
`--dry-run` there before enabling the timers.

Settings go in `secrets/env.prod` (see the "Off-server backup to Azure Blob" block in `env.prod.example`).
`BACKUP_ALERT_RECIPIENTS` and the SMTP settings are used exactly as on the office server. To install:

```bash
# Prove the wiring first: lists what would be copied, dumps and uploads nothing, touches no marker.
sudo bash scripts/hosting/backup-azure-blob.sh --dry-run

sudo cp scripts/hosting/systemd/hcs-portal-backup-azure*.service \
        scripts/hosting/systemd/hcs-portal-backup-azure*.timer \
        scripts/hosting/systemd/hcs-portal-backup-alert@.service \
        scripts/hosting/systemd/hcs-portal-backup-freshness.* /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now hcs-portal-backup-azure.timer hcs-portal-backup-azure-verify.timer \
                            hcs-portal-backup-freshness.timer
# The weekly unit downloads the host dump back FROM AZURE, checks it against the manifest, restores it into a
# scratch database and drops it. Run it once now: it writes both freshness markers.
sudo systemctl start hcs-portal-backup-azure-verify.service
```

To restore from Azure, fetch one complete run folder, check it, then follow "Restore one database" below with
those files. The same pinned rclone image and the VM's identity do the download:

```bash
STAMP=<a run stamp whose folder HAS a MANIFEST.sha256>
printf 'RCLONE_CONFIG_AZ_TYPE=azureblob\nRCLONE_CONFIG_AZ_ACCOUNT=%s\nRCLONE_CONFIG_AZ_USE_MSI=true\n' \
  "<storage account>" > /tmp/rclone-restore.env
sudo docker run --rm --env-file /tmp/rclone-restore.env -v "$PWD/backups/restore-$STAMP:/out" \
  <the RCLONE_IMAGE pinned in backup-azure-blob.sh> copy "az:db-backups/$STAMP" /out
(cd "backups/restore-$STAMP" && sha256sum -c MANIFEST.sha256)   # every line must say OK
rm /tmp/rclone-restore.env
```

Documents do NOT come back "from the same night". The `documents` container is one cumulative mirror: each run
copies only new or changed objects into it and nothing is ever deleted there, so it always holds every object
ever backed up, a superset of what any one night's databases reference. Copy it back whole, before the
applications start. The next section does both halves for a whole system.

### Recover the whole system onto a new Azure VM from the backup account

**Use this when the VM is gone or unusable.** It rebuilds the system on a new VM from the newest complete run
in the backup account. Nothing here touches the backup account except reading it, and the account carries a
delete lock, so a mistake here cannot cost the backups.

**Not yet rehearsed end to end** (written 2026-10-13). Downloading a run and checking its manifest is proven
against the Azurite emulator only. Everything after the download was proven by the 2026-10-13 Tier-2 rehearsal,
from a set copied off the office server: restoring host and offices, loading documents, the migrator rewriting the
sign-in redirects, and the exact-key document check. A first drill of this exact path is planned on the VM, into
a throwaway compose project, with outbound mail and the Case Tracker neutralised.

#### What must exist BEFORE you start, or the recovery stops part-way

1. **The off-VM copy of `secrets/`.** The nightly backup does not include it. Four things in it matter:
   - **The application's SQL password must be the SAME as the lost VM's**: `SQL_APP_LOGIN` and `SQL_APP_PASSWORD`
     where the app has its own login (the Azure VM, `scripts/hosting/sql-app-login.sh`), otherwise
     `MSSQL_SA_PASSWORD`. Each office's connection string is built from `ConnectionStrings:Default` and stored on
     the tenant row in the host database (`DoctorTenantAppService`, `SetDefaultConnectionString`), and requests are
     routed with the stored copy. With a different password, the host database works and **every office fails to
     log in to SQL Server**.
   - `openiddict.pfx` and its passphrase. A new certificate also works, but signs everyone out, which a restore
     does anyway.
   - `dataprotection.pfx` and its passphrase.
   - `admin-passwords/`.
2. **The two MinIO images.** They cannot be pulled from any registry. Load them from the rescue archive (see the
   header of `docker-compose.prod.yml`).
3. **The release that was running:** the commit to check out. Newer code is fine, because the migrator applies
   any pending migrations; older code is not.

#### Steps

1. **Build the VM and its stack, up to and including the TLS certificate**, as `infra/azure/fastpath/README.md`
   describes ("Deploy", then "After the template, before the application", steps 1-4). Use the RESTORED
   `secrets/` from item 1 above, not new ones. Redeploying the template gives the new VM its own identity and
   assigns it the backup-account roles. Assignments can take a few minutes to apply.

2. **Find the newest COMPLETE run.** A run folder without `MANIFEST.sha256` is incomplete: the manifest is
   uploaded last.

   ```bash
   cd ~/hcs-patient-portal
   printf 'RCLONE_CONFIG_AZ_TYPE=azureblob\nRCLONE_CONFIG_AZ_ACCOUNT=%s\nRCLONE_CONFIG_AZ_USE_MSI=true\nRCLONE_CONFIG=/dev/null\n' \
     "<storage account>" > /tmp/rclone-az.env
   RC="sudo docker run --rm --env-file /tmp/rclone-az.env <the RCLONE_IMAGE pinned in backup-azure-blob.sh>"
   $RC lsf --dirs-only az:db-backups | sort | tail -5          # newest last
   $RC lsf az:db-backups/<stamp>/                               # MUST list MANIFEST.sha256 and one .bak per database
   ```

3. **Download that run and check it.** Then put the dumps where SQL Server reads them.

   ```bash
   STAMP=<stamp>
   sudo docker run --rm --env-file /tmp/rclone-az.env -v "$PWD/backups/restore-$STAMP:/out" \
     <the pinned RCLONE_IMAGE> copy "az:db-backups/$STAMP" /out
   (cd "backups/restore-$STAMP" && sha256sum -c MANIFEST.sha256)      # every line must say OK
   sudo cp "backups/restore-$STAMP"/*.bak backups/                     # ./backups is BACKUP_DIR
   ```

4. **Start the data tier only, and restore. Host first.** Bring up `sql-server`, `redis` and `minio`, nothing
   else. Then restore the host `CaseEvaluation`, then every `CaseEvaluation_<office>` in the run, following
   "Restore one database" below, step 4. The host goes first because it holds the office connection strings.

   ```bash
   scripts/hosting/dc.sh up -d sql-server redis minio
   ```

   Where the app has its own login, recreate it with the SAME password, then hand it the restored databases.
   Restores run as `sa`, so `sa` owns them until then and the app cannot open them:

   ```bash
   sudo bash scripts/hosting/sql-app-login.sh            # creates the login; refuses a password mismatch
   sudo bash scripts/hosting/sql-app-login.sh --adopt    # after the restores: owner of every portal database
   ```

5. **Recreate the buckets, then copy the documents back.** `minio-init` makes the buckets and the application's
   MinIO user from `env.prod`. Then copy each bucket listed in `AZURE_BACKUP_BUCKETS`, using the same two remotes
   as `backup-azure-blob.sh`, in the opposite direction:

   ```bash
   scripts/hosting/dc.sh up -d minio-init && docker wait hcs-patient-portal-minio-init-1     # prints 0
   # Add the MinIO remote to the same env file: the application credential from env.prod (it may write the bucket).
   printf 'RCLONE_CONFIG_MINIO_TYPE=s3\nRCLONE_CONFIG_MINIO_PROVIDER=Minio\nRCLONE_CONFIG_MINIO_ENDPOINT=http://minio:9000\nRCLONE_CONFIG_MINIO_ACCESS_KEY_ID=%s\nRCLONE_CONFIG_MINIO_SECRET_ACCESS_KEY=%s\nRCLONE_CONFIG_MINIO_NO_CHECK_BUCKET=true\n' \
     "<MINIO_APP_ACCESS_KEY>" "<MINIO_APP_SECRET_KEY>" | sudo tee -a /tmp/rclone-az.env > /dev/null
   sudo docker run --rm --network hcs-patient-portal_default --env-file /tmp/rclone-az.env \
     <the pinned RCLONE_IMAGE> copy az:documents/case-evaluation-documents minio:case-evaluation-documents
   sudo rm /tmp/rclone-az.env
   ```

   The Case Tracker's own bucket is backed up only if it was listed in `AZURE_BACKUP_BUCKETS`.

6. **Check that the documents match the databases** before anyone signs in, as in "Verify documents match the
   database" below. List the restored objects with
   `rclone lsf -R minio:case-evaluation-documents` in place of the tar listing. **Any object a row references that
   is missing is a finding to record.** Do not paper over it.

7. **Start the applications.** Run `scripts/hosting/dc.sh up -d`; the migrator runs first and must exit 0. Then
   `scripts/hosting/dc.sh up -d --force-recreate reverse-proxy`. Then:
   - check that `/health-status` answers and that each office's API answers;
   - confirm the SQL Server edition: `SELECT @@VERSION` must not say Developer.

   Everyone is signed out: sessions lived in Redis, which is not backed up.

8. **Reinstall the host-side pieces a git checkout never installs.** These are the systemd units: the Azure
   backup, the restore proof, the freshness check, the alert and certificate renewal. Enable their timers, as
   when the VM was first built, and run a backup at once, so the new VM's first set exists before the next night.

9. **Give the Case Tracker its MinIO access back.** MinIO's users live in MinIO itself, and the document backup
   does not carry them. So each office's Case Tracker credential is gone, even though its file is still in the
   restored `secrets/`. Move that file aside and create a new one with
   `scripts/hosting/minio-case-tracker-access.sh --shared <every Case-Tracker-served office id>` (or per office,
   `... <office id>`, where an office has its own key), then hand the new credential to the Case Tracker owner.

**Settings stored in the databases come back with them.** That includes anything saved through an admin screen,
such as mail settings. That is what a production recovery wants. It is also why a _rehearsal_ of this procedure
must check the restored databases for stored mail settings before any application starts: those settings
override the env file.

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
