# Fast path: the portal on one Azure VM

Written 2026-10-13. **Nothing here has been deployed yet.** `bicep build` and `bicep lint` (CLI 0.47.16, the version
CI pins) report zero diagnostics on every file. That proves the syntax and the type model agree; it does not prove a
resource provisions. The first real test is `what-if` against the subscription.

What it costs, line by line: [COSTS.md](COSTS.md). About $181 a month at the defaults.

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
2. The DNS zone named exactly as the base domain exists in that resource group.
3. An SSH key pair exists. Only the public half goes in the parameters (`ssh-keygen -t ed25519`).
4. The subscription has the encryption-at-host feature registered. Once per subscription, and it takes a few
   minutes:

   ```bash
   az feature register --namespace Microsoft.Compute --name EncryptionAtHost
   az feature show --namespace Microsoft.Compute --name EncryptionAtHost --query properties.state -o tsv   # wait for Registered
   ```

   Without it the VM is refused. Setting `encryptionAtHost` to false skips this, at the cost described above.

```bash
git clone https://github.com/gesco-healthcare-support/hcs-patient-portal.git
cd hcs-patient-portal/infra/azure/fastpath
cp main.parameters.example.json main.parameters.json   # fill in baseDomain and adminSshPublicKey

# Not optional on a first run: read what it will create.
az deployment group what-if -g <resource-group> -f main.bicep -p @main.parameters.json

az deployment group create -g <resource-group> -f main.bicep -p @main.parameters.json -n portal-fastpath
```

The outputs give the public IP, the VM's identity, the backup account name and the zone's name servers.

## After the template, before the application

The template stops at an empty VM with Docker installed on the data disk. The rest is the go-live runbook:

1. Confirm cloud-init finished and Docker really is on the data disk: `findmnt /srv` shows the data disk and
   `docker info --format '{{.DockerRootDir}}'` prints `/srv/docker`. If cloud-init could not find the disk, it
   stopped before installing Docker on purpose; see "Known gaps".
2. Clone the repository to `/home/apadmin/hcs-patient-portal` and create `secrets/` with a NEW `env.prod`.
3. **Load the two MinIO images from the rescue archive.** They cannot be pulled from any registry (see `../README.md`,
   step 6 of "After the template").
4. Issue the TLS certificate with `scripts/hosting/renew-tls-cert.sh` (Let's Encrypt, DNS-01 against the zone, as the
   VM's identity, which already has DNS Zone Contributor). Run `sudo bash scripts/hosting/renew-tls-cert.sh --staging`
   FIRST: it proves the zone, the identity and the role against Let's Encrypt's staging CA and installs nothing. Then
   run it without `--staging`, and install `hcs-portal-tls-renew.{service,timer}` from `scripts/hosting/systemd` so it
   renews itself. The script's header says what each step checks.
5. Bring the stack up, migrator first, then install the backup units (`docs/runbooks/hosting-backup-restore.md`).
   Run `scripts/hosting/backup-azure-blob.sh --dry-run` before enabling the timers: it is the first real test of the
   VM's identity against the backup account.

## Later: Azure Relay for the Case Tracker's document reads

After go-live, and before the Case Tracker's document reads are switched on, redeploy with
`deployCaseTrackerRelay: true` (and `relayAlertEmail` set). That adds:

- a Relay namespace and one hybrid connection, `case-tracker-documents`;
- a listen-only and a send-only SAS rule;
- "Azure Relay Listener" for the VM's identity, on that hybrid connection only;
- optionally, an alert when the hybrid connection has had no listener for 15 minutes.

The VM side is `scripts/hosting/relay-listener.sh` with `hcs-portal-relay.service`, and the MinIO vhost stays closed
(`MINIO_ALLOW_DIRECTIVES="deny all;"`). The whole design, including the Case Tracker's side, is in
`docs/integration/case-tracker-relay.md`. It costs about $15 a month (COSTS.md).

## Known gaps, honestly

- **Never deployed.** In particular, none of the following has been checked against Azure:
  - that creating an account with immutability support AND infrastructure encryption on, while the blob service
    enables versioning, is accepted in one deployment;
  - that `Standard_D4s_v5` offers encryption at host in West US 2 together with Trusted Launch. Microsoft says
    support varies by size and region; check before deploying with
    `az vm list-skus -l westus2 --size Standard_D4s_v5 --query "[].capabilities[?name=='EncryptionAtHostSupported']"`;
  - that the custom role's three data actions are all `rclone` needs. The backup script was proven against the
    Azurite emulator, which does not enforce roles.
- **The data disk's device name.** cloud-init looks for LUN 0 at `/dev/disk/azure/data/by-lun/0` and then
  `/dev/disk/azure/scsi1/lun0`. Ubuntu 24.04 images on Azure have shipped both naming schemes. If neither appears,
  cloud-init stops before installing Docker, and `/var/log/cloud-init-output.log` says so. That is deliberate:
  otherwise Docker would quietly put every database and document on the OS disk.
- **Role assignments can lag** by a few minutes after a deployment. A first backup run straight after deploying may
  be refused; run it again.
- **Platform patching reboots the VM** when an update needs it. Docker restarts the containers, but uploads return
  503 for about a minute while ClamAV loads its signatures, as they do after any restart.
- **One VM, one zone.** No high availability. The data disk can be zone-redundant (`Premium_ZRS`, the default), but
  the VM itself is a single instance. Recovery is the nightly backup.
