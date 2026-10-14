# Fast path: monthly cost

Every price below is from the **Azure Retail Prices API** (`prices.azure.com/api/retail/prices`), queried
**2026-10-13**: West US 2, USD, pay-as-you-go list prices, 730 hours a month. List prices exclude any discount
the company's agreement carries and any tax. Re-query before quoting a figure to anyone. The full design's
costs are in [../COSTS.md](../COSTS.md).

## What the template deploys, at its defaults

| Resource | SKU | Unit price | Per month |
| --- | --- | --- | --- |
| VM | `Standard_D4s_v5`, Linux (4 vCPU, 16 GiB) | $0.192 / hour | **$140.16** |
| OS disk | Premium SSD `P6 LRS` (64 GiB) | $9.28 / month | $9.28 |
| Data disk | Premium SSD `P10 ZRS` (128 GiB) | $26.88 / month | $26.88 |
| Public IP | Standard, static IPv4 | $0.005 / hour | $3.65 |
| DNS zone | 1 public zone | $0.50 / zone / month | $0.50 |
| DNS queries | assume 1 million a month | $0.40 / million | $0.40 |
| Outside "site is down" check | Standard availability test, 3 regions every 15 minutes (8,760 runs) | $0.0005 / run | $4.38 |
| Network, NSG, VNet, locks, roles | | free | $0.00 |
| Encryption at host, backup infrastructure encryption | | no separate meter | $0.00 |
| **Fixed total** | | | **$185.25** |
| Backup storage | see below; grows with data | | under $1 at pilot volume |
| Internet egress | first 100 GB a month free, then $0.087 / GB | | expected $0 |

**About $185 a month at the defaults**, below the $200-300 the brief expected. The VM is 76% of it.

## The backup account (Standard_GRS)

| Tier | Stored, per GB per month | Where objects sit |
| --- | --- | --- |
| Hot | $0.0368 | first 30 days |
| Cool | $0.0200 | days 30-90 |
| Cold | $0.00792 | after 90 days; documents stay here |
| Archive | $0.00299 | database dumps after 365 days |

Plus **$0.02 per GB written** for geo-replication (the GRS copy in the paired region) and per-operation charges
that come to cents at this volume. Reading data back out of Cool, Cold or Archive is charged per GB ($0.01, $0.03 and
$0.02 to $0.10), which only matters during a restore.

Two worked figures, both resting on assumptions rather than measurement:

- **Pilot, first year.** Today's nightly dumps total about 96 MB (host plus two offices, measured 2026-10-07), so
  about 3 GB of dumps and well under 1 GB of documents a month. **Under $1 a month.**
- **Seven years at the assumed full volume** of 20 offices and 2,000 appointments a month. Documents grow about
  6 GB a month, so about 500 GB after seven years, in Cold: about $4. If dumps reach 1 GB a night, every night
  kept seven years is about 2.5 TB, mostly in Archive: about $8, with the most recent year in Cool and Cold
  adding about $4. **About $16 a month in year seven.** Nightly full dumps kept forever are the main growth;
  thinning old dumps to weekly is a deletion rule, which is a retention decision, not a cost one.

LRS instead of GRS halves storage ($0.0184 Hot, $0.0100 Cool, $0.0036 Cold, $0.00099 Archive per GB) and drops
the geo-replication charge, but then one region's loss takes both the server and its backup. At these sizes the
difference is cents to a few dollars a month.

## Decisions with a price attached

**Data disk.** It holds SQL Server, MinIO and the images; the backup is the recovery copy either way.

| Size | LRS | ZRS (default) |
| --- | --- | --- |
| 128 GiB (P10, default) | $17.92 | $26.88 |
| 256 GiB (P15) | $34.56 | $51.84 |
| 512 GiB (P20) | $66.56 | $99.84 |

A managed disk can be grown later without redeploying; it can never be shrunk.

**Getting a shell on the VM.** The office has no fixed public address, so SSH cannot be allow-listed
permanently.

| Option | Per month | Notes |
| --- | --- | --- |
| Open port 22 to your current address only when needed, then close it | $0 | Default. One NSG rule, added and removed in the portal or with `sshAllowedSourcePrefixes` |
| Defender for Servers Plan 2, which includes just-in-time VM access | $14.60 ($0.020 / hour) | Opens 22 to your address for a set time on request, then closes it. Plan 2 adds other server protections too |
| Azure Bastion Basic | $142.35 ($0.19 / hour plus its public IP) | Browser SSH, no port open at all. Billed every hour it exists; `deployBastion: true` |
| Azure Bastion Developer (free) | not available | Not offered in West US 2 |

**Azure Relay for the Case Tracker's document reads** (`deployCaseTrackerRelay`, off by default; design in
`docs/integration/case-tracker-relay.md`). Priced from the Retail Prices API, West US 2, 2026-10-13:

| Item | Unit price | Per month |
| --- | --- | --- |
| Hybrid Connections listener | $0.0134 / listener-hour | $9.78 for the one listener |
| Hybrid Connections data | first 5 GB a month free, then $1.00 / GB | $5.00 at about 10 GB of documents a month |
| Metric alert (no listener for 15 minutes) and its email | about $0.10 per alerted series | about $0.10 |
| **Documents through Relay** | | **about $15** |
| The changes feed through Relay too (a second hybrid connection and listener) | | about **$10 more**; see the design doc |

**The outside check** (`deployUptimeCheck`, on by default; `modules/uptime.bicep`). Retail Prices API, West US 2,
2026-10-13:

| Item | Unit price | Per month |
| --- | --- | --- |
| Standard availability test runs | $0.0005 / run | $4.38 at 15 minutes (8,760 runs); $13.14 at 5 minutes |
| Results in Log Analytics | first 5 GB a month free, then $2.30 / GB | $0: well under 1 MB a day, capped at 0.1 GB a day |
| Metric alert and its email | first 10 alert series and 1,000 emails a month free | $0 |
| **Outside check** | | **about $4.38** |

The free classic "URL ping" test is no longer offered (Microsoft retired it on 2026-09-30). Two free
alternatives were weighed and not chosen: a third-party uptime service (a new vendor account outside Azure that
no template can create) and a scheduled GitHub Actions job (its schedule can slip by an hour under load, and it
stops after 60 days without repository activity). A VM-level metric alert is cheap but sees the VM, not the site:
nginx down, the API failing or an expired certificate would all look healthy.

**Committing to the VM.** A reservation for this size in West US 2 costs **$1,038 for one year** (about $86.50 a
month, 38% less) or **$1,993 for three years** (about $55.36 a month, about 60% less). Worth it once the fast path is
the settled home for at least a year; it is a billing commitment, not a template setting.

## Not included

- Any Azure Backup vault or disk snapshots of the VM. The nightly off-server backup is the recovery copy.
- Defender for Cloud, Key Vault, Application Gateway, managed databases, and monitoring beyond the outside check
  (its small workspace holds only the check's results). Those belong to the full design, built in stages.
- SQL Server licensing. The stack runs SQL Server **Express** on Azure, which is free and licensed for production
  (10 GB per database). The Developer edition the office server uses is not licensed for production.
