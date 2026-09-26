# Monthly cost of the Azure infrastructure

Reference table for what `main.bicep` provisions, at its parameter defaults, in **West US 2**.

- **Source:** the public Azure Retail Prices API (`https://prices.azure.com/api/retail/prices`), queried 2026-09-25.
  - Consumption (pay-as-you-go) meters only, in USD, before tax, with no reservations and no negotiated discounts.
  - Filter: `armRegionName eq 'westus2'`, or the Global / Zone 1 meter where the service has no regional one.
- **Hours:** a month is 730 hours, the convention Microsoft's own pricing examples use. A daily meter is multiplied by
  730 / 24.
- **Not measured:** every figure is a list price times a quantity. Nothing has been deployed, so no line is an observed
  bill. Prices change; re-run the query before relying on a number.

## Fixed monthly cost

Charged whether or not anyone uses the portal.

| Resource | SKU / size | Meter (West US 2) | Per month | Decision it serves | Cheaper alternative, and why it was rejected |
| --- | --- | --- | ---: | --- | --- |
| Application Gateway | WAF_v2, fixed | $0.36 / hour | $262.80 | The only public entry point, with a web application firewall in front of a PHI portal | Standard_v2 without WAF ($0.20 / hour, $146.00): no request inspection at all |
| Gateway public IP | Standard, static IPv4 | $0.005 / hour | $3.65 | Fixed address for the DNS records | None: v2 requires a Standard static IP |
| DDoS IP Protection | 1 protected IP (the gateway's) | $0.2726 / hour | $199.00 | **Accepted** (decision D2, 2026-09-25). See the section below | Free infrastructure protection only ($0); Network Protection plan ($2,943.55) |
| NAT gateway | Standard | $0.045 / hour (Global meter) | $32.85 | A fixed outbound address; default outbound access is off (plan A1, edge-14) | Default outbound access ($0): Microsoft says it is not for production, and its address can change without notice |
| NAT public IP | Standard, static IPv4 | $0.005 / hour | $3.65 | The address partners allow-list | None: a NAT gateway needs one |
| Application host | Standard_D4s_v5 (4 vCPU, 16 GiB), Linux | $0.192 / hour | $140.16 | One host (D19), with room for the upload malware scanner (B11) | D2s_v5 ($70.08): the compose file already declares about 6.5 GiB of its 8 GiB, and ClamAV needs 3 GiB minimum, 4 GiB preferred |
| Host OS disk | Premium SSD P6 (64 GiB), LRS | $9.28 / month | $9.28 | Rebuilt from the image, never restored, so LRS | Standard SSD E6 ($4.80 plus $0.002 per 10,000 operations): lower and less predictable IOPS under the container runtime, to save $4.48 |
| Host data disk | Premium SSD P10 (128 GiB), **ZRS** | $26.88 / month | $26.88 | MinIO keeps both buckets on this disk (decision 16) | P10 LRS ($17.92): a single-datacenter copy of documents between mirror runs |
| SQL elastic pool | Standard, 100 eDTU (includes 100 GB) | $7.25 / day | $220.52 | Database per office in one pool (D22); the host database is declared inside it | 50 eDTU ($110.26): includes only 50 GB, so the 100 GB pool would add storage cost, and per-database headroom halves |
| SQL point-in-time backups | 14 days, geo-redundant | Included | $0.00 | Recovery window (D10, data-D1) | None needed: in the DTU model, PITR backup storage is part of the pool price |
| Azure Managed Redis | Balanced_B0 (0.5 GB), high availability on | $0.016 / node-hour, 2 nodes | $23.36 | Managed cache (decision 11), Entra sign-in (decision 20); size B0 (D1, 2026-09-25) | Without HA ($11.68): no SLA, and data loss and downtime on maintenance. B1 with HA is $46.72 |
| Private endpoints | 5 (SQL, cache, 2 storage, Key Vault) | $0.01 / hour each (Global meter) | $36.50 | Nothing but the gateway is reachable from the internet | Service endpoints ($0): the services' public endpoints would stay enabled |
| Private DNS zones | 4 | $0.50 / zone | $2.00 | Private names resolve to the private endpoints | None |
| Public DNS zone | 1 | $0.50 / zone | $0.50 | Wildcard records; DNS-01 certificate issuance | A zone outside Azure: issuance from the host would need that provider's credentials |
| Container registry | Basic | $0.1666 / day | $5.07 | The host pulls images with its managed identity | None: Basic is the lowest tier. Premium would add a private endpoint, which images (not PHI) do not need (idci-8) |
| **Total** | | | **$966.22** | | |

Each figure is the meter times 730 hours, or a daily meter times 730 / 24 days.

## Usage-based cost

Not in the total. Each line is priced per unit because the volume is not known before the pilot.

| Resource | Meter (West US 2) | What drives it |
| --- | --- | --- |
| NAT data processed | $0.045 / GB (Global) | Everything the host sends or fetches outbound: image pulls, OS updates, signature feed, partner calls |
| Private endpoint data processed | $0.01 / GB in and out (Global) | Traffic from the host to SQL, the cache, storage and Key Vault |
| Internet egress | First 100 GB / month free, then $0.087 / GB | Responses to users and the partner |
| Documents storage, GZRS, Hot | $0.0414 / GB-month (LRS would be $0.0184) | The continuous mirror of the MinIO documents bucket (decision 16). 100 GB is $4.14 |
| Backups storage, GZRS, Cool | $0.0225 / GB-month | Database and archive copies. GZRS is decision 15 |
| Log Analytics ingestion | First 5 GB / month free, then $2.30 / GB | Syslog (access trail and warnings) and gateway access and firewall logs |
| Log Analytics retention | $0.10 / GB-month beyond the included period | `logRetentionDays` is 90 |
| Key Vault operations | $0.03 / 10,000; RSA 3072 key operations $0.15 / 10,000 | Secret reads at deploy time; key-ring wrap and unwrap, a handful a month |
| Gateway capacity units | $0.0144 / unit-hour | `minCapacity` is 0 (decision D4), so only units traffic actually uses are billed. A steady single unit is about $10.51 a month |

## DDoS IP Protection: accepted

**Decision D2 (Adrian, 2026-09-25): on from the first deployment,** for the gateway's public IP only. The NAT address
carries outbound traffic only and is not protected.

- **Why on at all.** Azure's free infrastructure protection is always on, but it is tuned for Azure's scale, not this
  application's traffic. IP Protection adds per-IP adaptive tuning, attack metrics and alerts, and mitigation reports.
  It uses the same mitigation engine as Network Protection.
- **Why from day one.** The adaptive tuning learns the traffic profile over 7 to 14 days. Enabling it at go-live would
  leave the first exposed weeks untuned; burn-in traffic trains it first.
- **Why not Network Protection.** It is $2,943.55 a month and covers 100 IPs. Microsoft's own guidance puts IP Protection
  below Network Protection in cost under 15 IPs. The extras it would buy (rapid-response support, cost protection, the
  WAF discount) do not justify the difference at one IP.
- **Parameter:** `enableDdosIpProtection` (default `true`).

## Gateway minimum instances: 0

**Decision D4 (Adrian, 2026-09-25):** `minCapacity` is 0 at launch.

- Each minimum instance reserves 10 billed capacity units whether or not traffic uses them. The design's earlier default
  of 2 reserved 20 units: $210.24 a month with no traffic at all.
- Microsoft states a v2 gateway stays highly available even with a minimum of 0; the fixed price covers that. The
  template's earlier comment calling 2 "the floor for the v2 SLA" was not supported, and is gone.
- The cost of 0 is time, not availability: scaling out takes 3 to 5 minutes, so a sudden spike can see latency until it
  completes.
- **When to raise it:** after a month of measured traffic, following Microsoft's guidance - set the minimum from the peak
  of the gateway's measured compute units divided by 10 (one instance handles about 10).

## Added by later items, not priced here

- **A3:**
  - Bastion Premium with its public IP;
  - Defender for Servers P1, SQL, Key Vault and Resource Manager;
  - the archive storage account;
  - alerts;
  - resource locks.
- **A3, decision D3 (Adrian, 2026-09-25):** a second, small Key Vault for the per-database admin passwords. The host
  may write there and stays read-only on the main vault, so a compromised host cannot overwrite the main vault's
  secrets. One more private endpoint ($7.30 a month) plus operations.
