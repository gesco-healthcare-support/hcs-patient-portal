# Fast path: the portal on one Azure VM

Written 2026-10-13. **Nothing here has been deployed yet.** `bicep build` and `bicep lint` (CLI 0.47.16, the version
CI pins) report zero diagnostics on every file. That proves the syntax and the type model agree; it does not prove a
resource provisions. The first real test is `what-if` against the subscription.

What it costs, line by line: [COSTS.md](COSTS.md). About $185 a month at the defaults.

## What this is

One Linux VM runs the same `docker-compose.prod.yml` stack the office server runs: SQL Server, Redis, MinIO, the
application, the packet renderer, ClamAV and nginx, all as containers, with nginx terminating TLS. Beside it, one
storage account receives the nightly off-server backup (`scripts/hosting/backup-azure-blob.sh`) and keeps it for
seven years.

It is the quick way to go live. The full design one folder up (Application Gateway with WAF, Azure SQL, managed
Redis, private endpoints) is built later, in stages. The VM keeps the full design's name and admin user, so the
two can share a resource group during the move and the backup and alert units work unchanged.

| What | Fast path | Full design (`../`) |
| --- | --- | --- |
| Internet edge | the VM's public IP, nginx terminates TLS | Application Gateway WAF v2 |
| Database | SQL Server **Express** in a container | Azure SQL elastic pool |
| Cache | Redis container | Azure Managed Redis |
| Documents | MinIO on the VM's data disk | MinIO on the host for now (decision 16) |
| Backups | one GRS account, nightly script | GZRS accounts plus a locked audit archive |
| Admin access | SSH opened on demand | Bastion |

## Files

| File | What it does |
| --- | --- |
| `main.bicep` | Parameters, the custom no-delete backup role, and every role assignment |
| `modules/network.bicep` | Virtual network, VM subnet with a storage service endpoint, NSG, static public IP, optional Bastion |
| `modules/host.bicep` | The VM, its two disks, its identity, and the cloud-init that installs Docker on the data disk |
| `modules/backup-storage.bicep` | The backup account, its two containers, versioning, soft delete and lifecycle tiers |
| `modules/dns-records.bicep` | Address and CAA records in the EXISTING DNS zone |
| `modules/uptime.bicep` | The outside "site is down" check and its email (on by default) |
| `modules/relay.bicep` | Optional Azure Relay for the Case Tracker's document reads (off by default) |
| `main.parameters.example.json` | Copy to `main.parameters.json` (gitignored) and fill in |
| `COSTS.md` | Every line priced from the Azure Retail Prices API |

## Decisions the defaults encode

- **The DNS zone is not created here.** Create the zone for the base domain by hand, in the same resource group,
  before anything else: its four name servers must go to whoever runs the parent domain, and that exchange should
  start before the VM exists. This template adds records to the zone and never deletes it.
- **SSH is closed.** The office has no fixed public address, so there is no permanent allow-list. Open port 22 to
  your current address when you need a shell and close it afterwards (portal: VM, Networking, add an inbound rule
  with source "My IP address"; or set `sshAllowedSourcePrefixes` and redeploy). For an emergency with no network
  access at all, the portal's **Run command** and **Serial console** need no open port. Paid alternatives are in
  COSTS.md. Bastion Developer, the free tier, is not offered in West US 2.
- **The VM cannot delete backups.** It gets a custom role that reads, lists and writes blobs but cannot delete them.
  With versioning on, an overwrite keeps the previous version too. So a compromised VM can neither delete the
  backups nor destroy them by overwriting. The backup script never needs to delete anything.
- **Nothing is ever deleted by a lifecycle rule.** Objects move Hot, then Cool after 30 days, then Cold after 90.
  Database dumps go on to Archive after `dbBackupArchiveAfterDays` (365). Documents stay in Cold, because a restore
  after losing the VM needs every document at once and Archive takes hours to read.
- **GRS, not ZRS or GZRS.** Only LRS, GRS and RA-GRS accounts support the Archive tier.
- **Everything is encrypted at rest, and two layers more are on by default.** Managed disks and the storage
  account are always encrypted with Microsoft-managed keys. On top of that:
  - **encryption at host** (`encryptionAtHost`) also covers what lives on the Azure host: the OS disk's
    read/write cache and the temporary storage. It needs a one-time feature registration (see Deploy).
  - **infrastructure encryption** (`requireInfrastructureEncryption`) encrypts the backup account a second time
    with a separate key. Like immutability support, it can be chosen only when the account is created and can
    never be changed. Microsoft recommends it where double encryption is a compliance requirement. It is on here
    because the account holds seven years of patient data and the choice cannot be revisited; turn it off before
    the first deploy if you disagree.

  Neither has a separate price in the Retail Prices API. **Customer-managed keys are deliberately not used**: they
  add a Key Vault whose loss, or a disabled key, shuts the VM down and locks the backups, to protect against
  something the Microsoft-managed keys already cover. SonarCloud flags the disks and the storage account for
  this (rules S6388 and S6378); those findings are a recorded decision, not an oversight.
- **Immutability support is on, with no policy.** Account-level version immutability can only be enabled when the
  account is created. Enabling it locks nothing; it keeps the option open for whenever counsel decides whether
  backups must be write-once for seven years.
- **Locks.** The backup account and the public IP carry CanNotDelete locks: the account holds the only off-server
  copy, and the IP is in DNS and on mail-relay allow-lists. A subscription Owner removes a lock deliberately.
- **The backup account admits only the VM subnet.** It is reached through a Microsoft.Storage service endpoint,
  with every other network refused and no shared key. Add an address to `backupStorageAllowedIpRanges` only if
  someone needs to browse it in the portal.

## Deploy

Run everything from **Azure Cloud Shell** in the portal. It already has the Azure CLI and Bicep, so nothing gets
installed on a laptop. Use an account that is Owner on the resource group: the template creates a custom role
definition and role assignments.

Before the first deploy:

1. The resource group exists, in West US 2.
2. The DNS zone named exactly as the base domain exists in that resource group. Not needed when
   `manageDnsZone` is false; see "If the names are A records at the registrar" below.
3. An SSH key pair exists. Only the public half goes in the parameters (`ssh-keygen -t ed25519`).
4. The subscription has the encryption-at-host feature registered. Once per subscription, and it takes a few
   minutes:

   ```bash
   az feature register --namespace Microsoft.Compute --name EncryptionAtHost
   az feature show --namespace Microsoft.Compute --name EncryptionAtHost --query properties.state -o tsv   # wait for Registered
   ```

   Without it the VM is refused. Setting `encryptionAtHost` to false skips this, at the cost described above.
5. The resource providers the template uses are registered: Compute, Network, Storage, Insights and
   OperationalInsights. **Once the feature above shows Registered, register Compute again**, or the feature does
   not reach the provider:

   ```bash
   for p in Compute Network Storage Insights OperationalInsights; do az provider register -n Microsoft.$p; done
   az provider show -n Microsoft.Compute --query registrationState -o tsv   # wait for Registered
   ```

6. **The subscription has vCPU quota for the VM's family in the region.** A new subscription can start at 0 for a
   family. A quota request in the portal can then be refused automatically, and the way out is a support request
   (issue type "Service and subscription limits (quotas)", quota type "Compute-VM (cores-vCPUs) subscription limit
   increases"), which is answered in business hours. On a first deploy it took a weekend. So check this early, before the deploy is scheduled:

   ```bash
   az vm list-usage -l westus2 --query "[?name.value=='standardDSv5Family' || name.value=='cores']" -o table
   ```

   `Standard_D4s_v5` needs 4 free in the DSv5 family AND in the regional total. The template's `what-if` fails
   only on `QuotaExceeded` when this is short.

```bash
git clone https://github.com/gesco-healthcare-support/hcs-patient-portal.git
cd hcs-patient-portal/infra/azure/fastpath
cp main.parameters.example.json main.parameters.json   # fill in baseDomain, adminSshPublicKey, uptimeAlertEmail

# Not optional on a first run: read what it will create.
az deployment group what-if -g <resource-group> -f main.bicep -p @main.parameters.json

az deployment group create -g <resource-group> -f main.bicep -p @main.parameters.json -n portal-fastpath
```

The outputs give the public IP, the VM's identity, the backup account name and the zone's name servers (empty when
`manageDnsZone` is false).

## After the template, before the application

The template stops at an empty VM with Docker installed and its `data-root` on the data disk (its image store is
not, until step 1 moves it). The rest is the go-live runbook:

1. Confirm cloud-init finished and Docker really is on the data disk: `findmnt /srv` shows the data disk and
   `docker info --format '{{.DockerRootDir}}'` prints `/srv/docker`. If cloud-init could not find the disk, it
   stopped before installing Docker on purpose; see "Known gaps".

   **Then move Docker's image store to the data disk, before the first image is loaded or built.** Docker 29
   uses the containerd image store, so images and build cache live in `/var/lib/containerd` on the OS disk, NOT
   under `data-root`, and `DockerRootDir` printing `/srv/docker` does not show it. On the first VM the build
   alone put 13 GB on the 64 GB OS disk. Bind-mount the store from the data disk, and make containerd and Docker
   refuse to start without that disk: `/srv` is mounted `nofail`, so the boot does not wait for it.

   ```bash
   sudo systemctl stop docker.socket docker.service containerd.service
   sudo install -d -m 700 /srv/containerd
   sudo rsync -aHAX --numeric-ids /var/lib/containerd/ /srv/containerd/
   sudo mv /var/lib/containerd /var/lib/containerd.old && sudo install -d -m 700 /var/lib/containerd
   echo '/srv/containerd /var/lib/containerd none bind,nofail,x-systemd.requires-mounts-for=/srv 0 0' \
     | sudo tee -a /etc/fstab
   sudo install -d /etc/systemd/system/containerd.service.d /etc/systemd/system/docker.service.d
   printf '[Unit]\nRequiresMountsFor=/srv /var/lib/containerd\n' \
     | sudo tee /etc/systemd/system/containerd.service.d/10-data-disk.conf
   printf '[Unit]\nRequiresMountsFor=/srv/docker\n' | sudo tee /etc/systemd/system/docker.service.d/10-data-disk.conf
   sudo systemctl daemon-reload
   sudo systemctl start var-lib-containerd.mount containerd.service docker.socket docker.service
   findmnt -no SOURCE --target /var/lib/containerd   # the data disk, ending in [/containerd]
   systemctl show -p RequiresMountsFor containerd.service docker.service
   docker image ls                                   # the same images as before the move
   sudo rm -rf --one-file-system /var/lib/containerd.old   # only after the line above is checked
   ```

   - **A bind mount, not containerd's `root =` setting:** `/etc/containerd/config.toml` belongs to the
     containerd package, and an upgrade that reset it would quietly put the store back on the OS disk.
   - **`nofail` on the bind mount too:** a mount that fails without it sends the boot to emergency mode, and on
     Azure that means no SSH. The `RequiresMountsFor` drop-ins are what keep containerd and Docker from starting
     on an empty directory instead.
   - **Later, check that it held:** `sudo du -sh /var/lib/containerd` grows while `df -h /` stays put.
   - **Once the stack runs, reboot once** (`sudo systemctl reboot`) and check that every container comes back
     healthy by itself.
   - **The disks' device names can swap between boots** (`sda` / `sdb`), which is why every mount uses a label or
     a path, never `/dev/sdX`.

   Then set the VM's clock to the offices' time zone. The image starts on UTC, and the backup and certificate timers
   use local time (`OnCalendar=01:30`), so on UTC the nightly backup would run in the evening. No container
   mounts the host's time zone, so this changes only the timers and the journal:

   ```bash
   sudo timedatectl set-timezone America/Los_Angeles
   ```

   These are steps, deliberately not in the template's cloud-init, and so is the backup folder in step 5. The
   VM's custom data cannot be changed on an existing VM, so editing it would make every later redeploy of this
   template fail against the running VM.
2. Clone the repository to `/home/apadmin/hcs-patient-portal`. From its root, write a NEW `secrets/env.prod`
   with the generator, fill in what it lists, then check it:

   ```bash
   install -d -m 700 secrets
   python3 scripts/hosting/vm-env-prod.py generate --alert-email <address>
   # edit secrets/env.prod: replace every CHANGE_ME the generator listed
   python3 scripts/hosting/vm-env-prod.py check
   ```

   The generator starts from `env.prod.example`, and stops if that file has a key it has no decision for. It
   generates every secret within the limits of what reads it. It creates the file mode 600 and never overwrites
   one. It prints no values. A person supplies only the ABP licence and NuGet key, the SMTP account and the
   backup account name; `--alert-email` fills the Let's Encrypt contact and both alert lists. On this VM, unlike
   the office server, it writes:

   - `MSSQL_PID=Express` (licensed for production; the default, Developer, is not);
   - `SQL_APP_LOGIN` and `SQL_APP_PASSWORD`, the application's own SQL login instead of `sa`. Office rows store
     only their database name and every office connects with this login, so the password can be changed later
     with `ALTER LOGIN`, the new value in `env.prod`, and a recreate of api, authserver and db-migrator;
   - `MINIO_ALLOW_DIRECTIVES="deny all;"` and `DOCS_ALLOW_DIRECTIVES="deny all;"`. Empty is NOT closed:
     compose then falls back to the private address ranges;
   - MinIO keys of at most 20 characters and secrets of at most 40, which is all MinIO accepts;
   - `CASE_TRACKER_INTEGRATION_TOKEN` and `CASE_TRACKER_FEED_TOKEN` as two DIFFERENT values; the push
     (`CASE_TRACKER_BASE_URL`, `CASE_TRACKER_INTAKE_TOKEN`) stays empty, because the Case Tracker pulls;
   - `AZURE_BACKUP_STORAGE_ACCOUNT` left for the template's `backupStorageAccountName` output.

   `check` refuses (exit 1) a file that breaks any of those rules, still holds a placeholder, sets a key twice
   or is readable by anyone but its owner. It names keys and never values. Run it again after every hand edit.
3. **Load the two MinIO images from the rescue archive.** They cannot be pulled from any registry (see `../README.md`,
   step 6 of "After the template").
4. Issue the TLS certificate with `scripts/hosting/renew-tls-cert.sh` (Let's Encrypt, DNS-01 against the zone, as the
   VM's identity, which already has DNS Zone Contributor; with A records instead, steps 4 and 5 change order, see
   below). Run `sudo bash scripts/hosting/renew-tls-cert.sh --staging`
   FIRST: it proves the zone, the identity and the role against Let's Encrypt's staging CA and installs nothing. Then
   run it without `--staging`, and install `hcs-portal-tls-renew.{service,timer}` from `scripts/hosting/systemd` so it
   renews itself. The script's header says what each step checks.

   **The chain is pinned to ISRG Root X1, and the script refuses any other.** Let's Encrypt can offer more than one
   chain. The one served on 2026-10-20 ends with Root YR cross-signed by ISRG Root X1. The Case Tracker reaches this
   server from a stock Java runtime that trusts ISRG Root X1 but not Root YR. So a chain that stops at Root YR
   would look fine in a browser and in curl, and break the changes feed, attendance and document reads at once.
   - The script asks lego for the X1 chain (`--preferred-chain`). lego falls back to the default chain, without a
     word, when the one asked for is not offered.
   - So the script also refuses to install a certificate whose chain does not end at a certificate issued by ISRG
     Root X1. The run fails (and emails); the certificate in place keeps being served.
   - `--staging` checks the staging CA's counterpart, `(STAGING) Pretend Pear X1`.
   - If Let's Encrypt stops offering the X1 chain, renewals fail with that reason about 30 days before expiry. That
     is the time to add Root YR to the Case Tracker's trust store, then set `ACME_PREFERRED_CHAIN` on purpose.
5. Bring the stack up. Before the FIRST `up`, create the folder SQL Server writes its dumps into, owned by its
   container user. Otherwise Docker creates it owned by root, and every backup fails with
   `Operating system error 5(Access is denied.)`:

   ```bash
   sudo install -d -o 10001 -g "$(id -gn)" -m 775 backups   # uid 10001 = the SQL container's mssql user
   ```

   Then start `sql-server`, create the application's login with `sudo bash scripts/hosting/sql-app-login.sh` (it
   verifies the login is not sysadmin), run the migrator and wait for it to exit 0, then start the rest. A plain
   `scripts/hosting/dc.sh up -d` (and `--force-recreate reverse-proxy`, through its dependencies) starts the two
   one-shot containers again, `db-migrator` and `minio-init`. That is harmless: both are idempotent and exit 0.

   Then install the backup units (`docs/runbooks/hosting-backup-restore.md`). Run
   `scripts/hosting/backup-azure-blob.sh --dry-run` before enabling the timers. It is the first real test of the
   VM's identity against the backup account, and it also checks that SQL Server can write `backups/`.
6. **Re-address the host admin before its first sign-in.** On a fresh build, the migrator seeds the host admin
   (user name `admin`) with ABP's default address, `admin@abp.io`, and an unconfirmed email.
   - The portal requires a confirmed email to sign in.
   - Confirming that address, or any password reset for it, would send mail to an outside company's domain.
   - So point the account at a mailbox the operators own, and mark it confirmed. That is one UPDATE of that row in
     the host database: in a transaction, refused unless it changes exactly one row, and with fresh stamps so
     nothing issued for the old address survives.

   ```sql
   SET XACT_ABORT ON; BEGIN TRAN;
   UPDATE CaseEvaluation.dbo.AbpUsers
      SET Email = N'<operator mailbox>', NormalizedEmail = UPPER(N'<operator mailbox>'), EmailConfirmed = 1,
          SecurityStamp = UPPER(REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'')),
          ConcurrencyStamp = LOWER(REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''))
    WHERE UserName = N'admin' AND TenantId IS NULL AND Email = N'admin@abp.io';
   IF @@ROWCOUNT <> 1 BEGIN ROLLBACK; THROW 50001, 'expected exactly one row', 1; END
   COMMIT;
   ```

   Then sign in at `https://admin.<base>` with the password in `secrets/admin-passwords/admin-password-host`, and
   change it straight away.
   - Nothing forces that change on a first creation: the forced change applies only to a password the migrator
     rotated.
   - After the change, the file is stale. Keep the new password off the VM.
7. **Test against a practice created through New Practice**, the path a real office takes, not against the
   seeded TEST office. Outside Development, the TEST office gets an admin role but no admin account, and nothing
   in the app adds the first account to an office that already exists.
   - New Practice creates the office admin (the doctor's email) with an unconfirmed email and a password nobody
     knows.
   - So the first visit is the office's own sign-in page, `https://<office>.auth.<base>/Account/ResendVerification`,
     to confirm the address. Only then use "Forgot password".
   - Forgot password on an unconfirmed address shows the usual success message but sends nothing.

## If the names are A records at the registrar (no Azure DNS zone)

The default is a delegated zone: the parent domain's owner adds four NS records once, and from then on this template
manages the records and the certificate is a DNS-01 wildcard. If the parent domain's owner prefers to keep the
records themselves, deploy with `manageDnsZone` set to false. Then no zone is needed or touched, the VM gets no DNS
role, and the certificate is issued with HTTP-01, which cannot do wildcards. What changes:

1. **Deploy first, then DNS.** The records point at the VM's static IP, which exists only after the deployment.
   Read `publicIpAddress` from the outputs (`dnsNameServers` is empty). The registrar then needs two A records,
   both to that address, with a short TTL: `<base>` and `*.<base>`.
   - A DNS wildcard also answers deeper names such as `<office>.api.<base>`, but only while no record exists
     between them. So add **no other record under `<base>`**: an explicit `api.<base>` would stop `*.<base>`
     answering `<office>.api.<base>`.
   - Four records (`<base>`, `*.<base>`, `*.api.<base>`, `*.auth.<base>`) also work.
   - A CAA record `0 issue "letsencrypt.org"` at the registrar is optional but recommended.
2. **Wait for public DNS.** `nslookup <base> 8.8.8.8` and `nslookup admin.<base> 8.8.8.8` must return the VM's
   address before any issuance: Let's Encrypt fetches the challenge from that address on port 80.
3. **env.prod:** `ACME_CHALLENGE=http-01`.
4. **The certificate comes after the stack, not before.** nginx serves the challenge, and it will not start without
   a certificate file. So:
   1. run `sudo bash scripts/hosting/renew-tls-cert.sh` once, which writes a 2-day self-signed placeholder and stops;
   2. bring the stack up (step 5 above);
   3. run the script with `--staging` (it proves port 80 is reachable from Let's Encrypt and installs nothing), then
      without it;
   4. install the renewal timer as in step 4.
5. **Every office is a name on the certificate.** HTTP-01 lists each name, and the script reads the offices from the
   host database on every run. After creating an office, run the script; the daily timer would otherwise add it
   within a day, and until then that office's addresses show a certificate warning. One certificate holds at most
   100 names, which is about 30 offices; the script refuses more.

Port 80 is already open in `modules/network.bicep`. It redirects everything to HTTPS except
`/.well-known/acme-challenge/`, which serves only the files the renewal script writes.

## The outside "site is down" check

On by default (`deployUptimeCheck`). Azure requests `https://health.<baseDomain>/health-status` from three US regions
every 15 minutes, and emails `uptimeAlertEmail` when two of the three fail. nginx answers only that path on the
health host and passes it to the API, whose health check queries the database, so a 200 means DNS, TLS, nginx, the
API and SQL Server all work. The check also fails when the TLS certificate has fewer than 14 days left, which
catches a renewal that has been failing for weeks. The backup and certificate alerts on the VM cannot report the
VM itself being down; this can. About $4.38 a month (COSTS.md).

- **Expect one "Fired" email right after the first deploy**, because nothing answers yet, and a "Resolved" email
  once the stack is up. That pair is also the proof the alert path works. The sender is
  `azure-noreply@microsoft.com`; put it on the safe-senders list, or the first one lands in Junk.
- It does not check the sign-in server or the web app's own pages. Each extra test costs the same again.
- `uptimeAlertEmail` is required while the check is on: an empty value fails validation rather than deploying a
  check that tells nobody. `uptimeFrequencySeconds: 300` alerts faster for about $13 a month.

## Azure Relay for the Case Tracker's document reads

Before the Case Tracker's document reads are switched on, redeploy with `deployCaseTrackerRelay: true` (and
`relayAlertEmail` set). That adds:

- a Relay namespace and one hybrid connection, `case-tracker-documents`;
- a listen-only and a send-only SAS rule;
- "Azure Relay Listener" for the VM's identity, on that hybrid connection only;
- optionally, an alert when the hybrid connection has had no listener for 15 minutes.

The VM side is `scripts/hosting/relay-listener.sh` with `hcs-portal-relay.service`, and the MinIO vhost stays closed
(`MINIO_ALLOW_DIRECTIVES="deny all;"`). The whole design, including the Case Tracker's side, is in
`docs/integration/case-tracker-relay.md`. It costs about $15 a month (COSTS.md).

**The redeploy, as it was done on 2026-10-20:**

- **It replaces the NSG's rule list.** The rules come wholly from the template, so an SSH rule added by hand in the
  portal is deleted. To keep SSH open for the VM steps, pass your address as its own parameter, a JSON array in
  single quotes:

  ```bash
  az deployment group what-if -g <resource-group> -f main.bicep -p @main.parameters.json \
    -p deployCaseTrackerRelay=true relayAlertEmail=<address> \
    -p 'sshAllowedSourcePrefixes=["<your public address>/32"]'
  ```

  Do not put `-p` and its value in one quoted shell variable: `az` then receives them as one argument. A later
  redeploy without the parameter closes SSH again.
- **Expect this what-if shape over the running VM:** 6 creates (namespace, hybrid connection, two rules, action
  group, alert), 1 "Unsupported" (the Listener role assignment, whose name is computed with `reference()`), and about
  10 property-level modifies that change nothing:
  - defaults the API returns but the template does not set: the data disk's `deleteOption` / `toBeDetached`, NIC
    settings, the public IP's `ddosSettings`, blob service and container settings, App Insights
    `Flow_Type` / `Request_Source`;
  - the two backup-container role assignments: an unevaluated `reference()` for the principal, and the same role
    id written at subscription scope rather than resource-group scope.

  Read each before and after. A real value change on the VM, its disks, the public IP or the storage account is a
  reason to stop.
- **Afterwards:** no reboot, every container still up, and a backup dry run still reached the backup account as the
  VM's identity.

## Known gaps, honestly

- **Deployed once, with `manageDnsZone` false (2026-10-19).** That first deployment settled:
  - Azure accepted, as written, a backup account with immutability support AND infrastructure encryption, with
    the blob service enabling versioning;
  - `Standard_D4s_v5` with encryption at host and Trusted Launch in West US 2;
  - the outside check's location ids and its two-of-three alert rule;
  - the custom role's three data actions are all `rclone` needs: the first nightly run uploaded and checked a
    matched set, and the weekly run downloaded the host dump back and restored it, all as the VM's identity;
  - cloud-init found the data disk and put Docker's `data-root` on it;
  - after the image store was moved (step 1), a reboot brought every container back healthy by itself.

  **Redeployed over the running VM once (2026-10-20), to switch on Azure Relay:** see that section for the what-if
  shape it showed. **Still never run:** the template with a DNS zone (`manageDnsZone` true, DNS-01).
- **The data disk's device name.** cloud-init looks for LUN 0 at `/dev/disk/azure/data/by-lun/0` and then
  `/dev/disk/azure/scsi1/lun0`. Ubuntu 24.04 images on Azure have shipped both naming schemes. If neither appears,
  cloud-init stops before installing Docker, and `/var/log/cloud-init-output.log` says so. That is deliberate:
  otherwise Docker would quietly put every database and document on the OS disk.
- **Role assignments can lag** by a few minutes after a deployment. A first backup run straight after deploying may
  be refused; run it again. (On the first deployment the backup ran hours later and was not refused.)
- **Platform patching reboots the VM** when an update needs it. Docker restarts the containers, but uploads return
  503 for about a minute while ClamAV loads its signatures, as they do after any restart.
- **One VM, one zone.** No high availability. The data disk can be zone-redundant (`Premium_ZRS`, the default), but
  the VM itself is a single instance. Recovery is the nightly backup.
