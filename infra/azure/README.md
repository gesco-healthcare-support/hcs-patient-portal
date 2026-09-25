# Azure infrastructure for the Patient Portal

Written 2026-09-24, revised 2026-09-25. Target: the **Standard tier** of the Azure design pack - managed web
firewall, managed cache, **one** application host.

`bicep build main.bicep` (Bicep CLI 0.47.16) produces a 112 KB template with zero diagnostics, and `bicep lint` is
clean on every file. **Nothing here has ever been deployed**, so treat a clean build as "the syntax and the type model
agree", not as "this works".

What it costs, line by line, is in [COSTS.md](COSTS.md).

---

## What this is, in one paragraph

One Linux VM runs the same containers the office box runs today, minus SQL Server and Redis, which become managed
services (MinIO stays on the host for now, decision 16). An Application Gateway with the web firewall in front is the
only thing on the internet; the host has no public address at all. Everything the host talks to is reached over a
private endpoint, and the host authenticates as itself rather than with stored passwords. Its outbound traffic leaves
through a NAT gateway with one fixed address.

## Why a VM rather than a container service

App Service multi-container, which would have taken `docker-compose.prod.yml` directly,
**retires on 2027-03-31**, and its replacement puts every container on one localhost so
every internal address in the compose file changes. A VM keeps the compose file working
essentially as written, which is the smallest possible application-side change during a
platform move. This is a deliberate choice, not a default.

## Files

| File | What it does |
| --- | --- |
| `main.bicep` | Orchestrator. Owns resource names and all role assignments |
| `modules/network.bicep` | Virtual network, three subnets, network security groups, NAT gateway, private DNS zones |
| `modules/platform.bicep` | Key Vault (and the data-protection wrapping key), container registry, Log Analytics workspace |
| `modules/data.bicep` | SQL logical server, elastic pool and the host database; Azure Managed Redis; documents and backups storage |
| `modules/host.bicep` | The application host, its disks, identity, cloud-init and syslog collection rule |
| `modules/edge.bicep` | Public IP, Application Gateway WAF v2 with its WAF policies, public DNS zone and records |
| `main.parameters.example.json` | Copy to `main.parameters.json` (gitignored) and fill in |
| `COSTS.md` | Monthly cost of every resource, from the Azure Retail Prices API |

---

## THE THING MOST LIKELY TO BE BROKEN BY A WELL-MEANING EDIT

**The application decides which office a request belongs to from the Host header.**

Application Gateway preserves the original Host header only when **both** `hostName` is
unset **and** `pickHostNameFromBackendAddress` is false. Microsoft states it plainly: *"If
you don't set either value, the original client Host header is passed through
unchanged."* Both are left alone in `edge.bicep`, deliberately.

Set either one and every request arrives belonging to **no office**. That is not a
degraded request - it is the abstention path that lands in host context.

This is the **opposite** default to an AWS Application Load Balancer, where host
preservation is off unless you turn it on. Worth knowing if this is ever ported.

Health probes are the one exception, and Microsoft explains why: a probe is sent outside
the context of any request, so it cannot derive a host name. The probe therefore sets one
explicitly and turns off `pickHostNameFromBackendHttpSettings`.

## The second thing: the object-storage host has its own listener

`minio.<base>` gets its own HTTPS listener, its own WAF policy with request-body enforcement off (the partner's S3 PUTs
carry whole files as the body), and routing priority **50**. Gateway wildcards span labels, so the main listener's
`*.<base>` also matches `minio.<base>`; the lower priority number is evaluated first, so the minio rule must stay below
the main rule's 100. A per-site WAF policy **replaces** the main one for its listener - any custom rule added to the main
policy must be copied to the minio policy if it should cover that host.

---

## Prerequisites, in order

1. **A resource group**, and Owner on it - `Owner` rather than `Contributor` because the
   template creates role assignments.
2. **A bootstrap Key Vault** holding the SQL admin password and, ideally, the deploy SSH
   key. This is a *different* vault from the one the template creates, which does not
   exist at parameter time.
3. **An Entra group for the SQL administrator**, and its object id. Required: the host's managed identity signs in to
   SQL as an Entra login, and only an Entra admin can create the first one.
4. **DNS delegation decided.** Either this template creates the zone (`createDnsZone:
   true`) and you hand the four name servers it outputs to whoever runs the parent domain,
   or the zone already exists and you set `createDnsZone: false`.

## Deploy - the first time is two phases

The gateway reads its TLS certificate from Key Vault. The certificate is issued on the host (DNS-01 through the zone
this template creates) after the host exists, and the vault is reachable only from inside the network. So:

1. **Phase 1**, `deployGateway: false` and `tlsCertificateSecretId` empty. Everything except the gateway, its WAF
   policies and its diagnostics. The public IP and the DNS zone ARE created, because issuance needs the zone.
2. **Issue the certificate** from the host into the vault. It must cover the apex, `*.<base>`, `*.api.<base>` and
   `*.auth.<base>` (`minio.<base>` and `health.<base>` fall under `*.<base>`). Take its **versionless** secret id, so a
   renewal flows through without redeploying.
3. **Phase 2**, `deployGateway: true` with `tlsCertificateSecretId` set. Every later deployment uses these values.

```bash
az deployment group create \
  --resource-group rg-portal-pilot \
  --template-file main.bicep \
  --parameters @main.parameters.json
```

Check what it would do first. This is not optional on a first run:

```bash
az deployment group what-if \
  --resource-group rg-portal-pilot \
  --template-file main.bicep \
  --parameters @main.parameters.json
```

## After the template, before the application

The template stops short of running anything. These steps are deliberately human.

1. **Take the name servers** from the `dnsNameServers` output and get the NS records
   created in the parent zone. Until that happens nothing resolves.
2. **Decide the administrative path to the host.** It has no public IP, and this template
   creates no Bastion and no jump host; that is a separate, deliberate item.
3. **The host database exists; office databases do not.** `CaseEvaluation` is created inside the pool, with 14-day
   point-in-time retention and 12-hour differentials. Adding an office is a portal action (decision D22): the
   application issues `CREATE DATABASE ... (SERVICE_OBJECTIVE = ELASTIC_POOL(name = ...))` so the database
   lands *inside* the pool. A bare `CREATE DATABASE` on Azure SQL takes the server default
   and is billed as a **standalone** database - quietly, and first visible on an invoice.
   Office databases also need the same retention policy applied, which this template cannot do for databases it does
   not know about.
4. **Put the secrets in the vault** and have the host read them at boot. The host's
   managed identity already has Key Vault Secrets User.
5. **Point the application at the managed services**: the cache on the `redisHostName` output, port 10000, TLS, signing
   in as the host's identity (access keys are off); the data-protection key at the `dataProtectionKeyUri` output.
6. **Bring the stack up** with the compose file, minus `sql-server` and `redis`.

## Things this template does NOT do, and why

| Not done | Why |
| --- | --- |
| Office databases | Adding an office is a portal action, not an infrastructure change (D22). That property is the point of the architecture |
| The TLS certificate | It is issued on the host between the two phases of the first deployment |
| Any admin path to the host | Bastion is a security decision and a separate item, not a template default |
| Object storage | MinIO stays on the host with BOTH buckets for now (decision 16): the partner reads `case-evaluation-documents` over S3, which Azure Blob does not offer. The documents are mirrored to the GZRS documents account |
| High availability of the host | D19 chose one host at launch. **Not symmetric to defer on Azure**: zone redundancy is not offered on Basic or Standard DTU, so adding it later is a database migration and a price step, not a setting |
| Application logs to Log Analytics | The application writes full user claims into its logs. Shipping them moves PHI into the workspace. Syslog and gateway logs only; container logs after that is fixed |

## Known gaps, honestly

- **Never deployed.** A clean `bicep build` proves the syntax and the type model agree. It
  does not prove a single resource provisions, that the private endpoints resolve, or that
  the gateway ever reports the backend healthy.
- **The probe path must exist.** `/health-status` has to be answered by nginx **outside**
  the 421 catch-all server block. A server-level `return` runs in
  `NGX_HTTP_SERVER_REWRITE_PHASE`, before location selection, so a `location` inside that
  block can never answer it. That nginx change is not in this directory.
- **End-to-end TLS is assumed.** The gateway speaks HTTPS to nginx on 443, because nginx's
  port 80 block returns a 301 to https for every host and forwarding plain HTTP would
  loop. nginx therefore still needs its certificate on the host.
- **The gateway starts with a minimum of 0 instances** (decision D4). It stays highly available at 0 - Microsoft
  includes that in the fixed price - but scaling out takes 3 to 5 minutes, so a sudden spike can see latency first.
  After a month of real traffic, raise `minCapacity` following Microsoft's guidance: the peak of the gateway's measured
  compute units over the month, divided by 10. Each reserved instance bills 10 capacity units; see COSTS.md.
- **Role assignments can lag.** The gateway waits for its Key Vault grants to be created, but Azure RBAC can take
  minutes to propagate after that. Phase 2 of the first deployment runs later and absorbs it.
- **The elastic pool is DTU-based**, which cannot be covered by an Azure reservation -
  reservations are vCore-only. A one-year commitment buys nothing on the largest line here.
- **Sizing rests on declared limits, not measurement.** `Standard_D4s_v5` is 16 GiB against roughly 6.5 GiB of
  declared compose limits plus the 3-4 GiB the upload malware scanner needs. No load test has ever been run, and the
  idle floor is unmeasured - the background job server polls continuously, so nothing is ever truly idle.
- **`allowSharedKeyAccess` is false on both storage accounts.** Any tool that expects an
  account key will fail. That is intended; it is also the kind of thing that surprises
  someone at 2am.
