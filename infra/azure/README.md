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
| `subscription/bootstrap.bicep` | Human-run, subscription scope: resource groups, the three CI identities, their rights. See "CI" |
| `subscription/assignable-roles.json` | The only roles the infra deploy identity may assign. Read by the bootstrap and by `scripts/infra-ci.py check` |

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

1. **The bootstrap has run** (see "CI" below). It creates the resource group and the identity that deploys into it.
   Nobody deploys this template with personal Owner rights once CI exists; a local `what-if` is still fine.
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

## CI

`.github/workflows/infra.yml` has three jobs:

- **Infra: Bicep** builds and lints every template (zero diagnostics), then runs `scripts/infra-ci.py check`.
- **Infra: What-If** runs on pull requests from this repository and posts one comment per PR.
- **Infra: Deploy** runs from `production` behind the `azure-production-infra` environment.

### Three identities, not one

A federated credential authenticates AS its identity, and role assignments attach to the identity. So one identity
with three credentials would hand the pull-request preview the deploy's rights. There are three user-assigned
managed identities, each with ONE federated credential:

| identity | OIDC subject (suffix) | rights |
| --- | --- | --- |
| `id-gh-whatif-<env>` | `:pull_request` | Reader + `Portal Infra What-If` (what-if and validate) on the workload group (`whatIfScope`; widen it only together with a job that previews at subscription scope) |
| `id-gh-infra-<env>` | `:environment:azure-production-infra` | on the workload group: Contributor; RBAC administrator constrained by ABAC to `subscription/assignable-roles.json`, service principals only, never a CI identity; `Portal Lock Writer` (write, no delete) |
| `id-gh-app-<env>` | `:environment:azure-production-app` | AcrPush on the registry, Virtual Machine Contributor on the VM (bootstrap phase 2) |

They live in `rg-portal-<env>-identity`, apart from the workload group, so the infra identity cannot rewrite any
federated credential. **A CI identity's own rights are only ever changed by a subscription Owner running the
bootstrap.** CI cannot widen its own rights: adding a role to `assignable-roles.json` does nothing until the
bootstrap runs again.

**What the ABAC condition does not do.** Contributor includes `virtualMachines/runCommands/write`, and a run command
executes as the VM. So inside the workload group the infra identity can reach whatever the VM identity can. The
condition stops escalation beyond the group and to roles outside the set. The control on the identity itself is the
environment: `production` only, and a required reviewer.

**Federated credentials live in the identity group only.** Every federated credential in this design belongs to one of
the three CI identities, and only the bootstrap manages them. Contributor on the workload group includes
`Microsoft.ManagedIdentity` writes, so the bootstrap also defines a deny policy on
`Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials` and assigns it at the workload group
(`subscription/modules/workload-policy.bicep`).

- It is assigned at the workload group only. The CI identities' own credentials are created in the identity group,
  and a subscription-wide assignment would refuse the bootstrap itself. The workload templates create no federated
  credentials.
- Contributor cannot remove it: `Microsoft.Authorization/*/Write` and `*/Delete` are in Contributor's NotActions.
- `scripts/infra-ci.py check` fails if the deny, or its workload-group scope, goes.

### One-off setup (subscription Owner)

1. **Opt the repository into immutable OIDC subjects** (repository settings, Actions, OIDC; or
   `gh api -X PUT repos/gesco-healthcare-support/hcs-patient-portal/actions/oidc/customization/sub` with
   `use_immutable_subject: true`). Do it BEFORE step 2: the bootstrap's default subject prefix is the immutable
   form, `repo:gesco-healthcare-support@274625791/hcs-patient-portal@1205316583`.
2. **Bootstrap phase 1.** Copy `subscription/bootstrap.parameters.example.json` to `bootstrap.parameters.json`
   (gitignored), then run:

   ```bash
   az deployment sub what-if -l westus2 -f subscription/bootstrap.bicep -p @subscription/bootstrap.parameters.json
   az deployment sub create  -l westus2 -f subscription/bootstrap.bicep -p @subscription/bootstrap.parameters.json \
     -n portal-bootstrap
   ```

3. **GitHub.** Create two environments, `azure-production-infra` and `azure-production-app`, each with:
   - deployment branches "Selected branches and tags", with the single branch rule `production`;
   - required reviewer `lev0398`, with "Prevent self-reviews" OFF;
   - administrators may NOT bypass (`can_admins_bypass: false`).

   Then set the secrets and variables:

   | where | secrets | variables |
   | --- | --- | --- |
   | repository | `AZURE_WHATIF_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | `AZURE_RESOURCE_GROUP`, then `INFRA_PREVIEW_REQUIRED=true` |
   | `azure-production-infra` | `AZURE_CLIENT_ID` (infra), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SQL_ADMIN_LOGIN`, `SQL_ADMIN_PASSWORD`, `ADMIN_SSH_PUBLIC_KEY`, `TLS_CERTIFICATE_SECRET_ID` | `AZURE_RESOURCE_GROUP`, `ENV_NAME`, `BASE_DOMAIN`, `SQL_ENTRA_ADMIN_OBJECT_ID`, `SQL_ENTRA_ADMIN_NAME`, `DEPLOY_GATEWAY`, `WAF_MODE`, `CREATE_DNS_ZONE` |
   | `azure-production-app` | `AZURE_CLIENT_ID` (app), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | added by A6 |

   None of the client, tenant or subscription ids is a credential. They are secrets so that public logs mask them.
4. **Bootstrap phase 2**, after the first infrastructure deploy's phase 1: the same command with `appDeployTargets`
   filled in.

**Self-review is allowed on both environments because there is one person on the team. REVERT that when a second
person joins:** turn "Prevent self-reviews" on and add them as a reviewer. It is a consequence of team size, not a
judgement that review has no value.

### Public logs

The repository is public, and so are its workflow logs, artifacts and PR comments. Therefore:

- what-if runs with `--result-format ResourceIdOnly`, and the summary shows change counts and resource names only;
- create runs with `--output none`;
- the office allow-list addresses are secrets, not variables;
- **errors are digested, never printed.** An ARM validation error's message can quote the parameter value it
  rejected, and GitHub masks only secrets, not the base domain, SQL login or Entra group id that a preview reads back
  from the last deploy. So every `az deployment` and `az tag` call sends its stderr to a file on the runner that is
  never printed. A failure reports only error codes and targets, walked through `details`, through
  `scripts/infra-ci.py digest`. That covers the job log and the PR comment alike. `infra-ci.py check` fails if an az
  call's stderr is not redirected, or if an error file or a captured error is echoed.

Run `what-if` locally when you need the property diff.

### Where the parameters come from

| parameter | pull-request what-if | deploy |
| --- | --- | --- |
| `sqlAdminPassword`, `adminSshPublicKey`, `tlsCertificateSecretId` | placeholder (what-if does not evaluate secure values) | environment secret |
| `sqlAdminLogin` | last `portal-main` deploy, else the example | environment secret |
| `baseDomain`, `envName`, `sqlEntraAdminObjectId`, `sqlEntraAdminName`, `createDnsZone` | last `portal-main` deploy, else the example | environment variable |
| `deployGateway`, `wafMode` | last `portal-main` deploy, else the example | environment variable |
| everything else | template default | template default |

The preview reads the last real deploy's parameters back from Azure, so it compares a PR's template against exactly
what production runs. The flip side: a PR cannot preview a parameter change. A parameter change (for example
`DEPLOY_GATEWAY=true` for phase 2) is previewed by the deploy job's own what-if, started with `workflow_dispatch` on
`production`. The mapping from parameter to variable is `ENV_MAP` in `scripts/infra-ci.py`.

### Still to prove, on the first real run

- Whether Reader plus `Portal Infra What-If` is enough for `what-if` (MEDIUM). First run it with plain Reader and
  record the exact `AuthorizationFailed` text; then run it with the custom role.
- The OIDC subject GitHub actually presents with immutable subjects on (MEDIUM: GitHub documents only the `ref`
  example). On a mismatch, Entra's `AADSTS70021` error names the subject presented. Record it here.
- That the write-probe step is refused with `AuthorizationFailed`. It runs on every PR, so this stays proven.
- That the deny policy holds: an attempt by the infra deploy identity to create a federated identity credential in the
  workload group (on the gateway identity, for example) is rejected with `RequestDisallowedByPolicy`. Try it once,
  after bootstrap phase 1, and record the error code here.

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
   Because access keys are off, there is no Redis connection string to fall back on. Until the application signs in
   with managed identity (lane C, item C7), it cannot reach the cache at all, and the symptom is "the app does not
   start", with nothing pointing at the cache.
6. **Load the two MinIO images onto the host before step 7. They CANNOT be pulled.** `docker-compose.prod.yml` pins
   `quay.io/minio/minio:RELEASE.2025-09-07T16-13-09Z` (server) and `quay.io/minio/mc:RELEASE.2025-08-13T08-35-41Z`
   (`minio-init`). Neither tag can be pulled any more: quay.io and Docker Hub both answer 401 to anonymous pulls of
   them (checked 2026-09-30). The MinIO release that carries the next security fix was published as source only,
   with no images. On a fresh host, step 7 therefore fails on these two images, and document storage never starts.
   - **The only copies** are the image cache on the on-prem portal server, and an export of both images,
     `minio-pinned-images.tar` (81 MB), which the owner keeps outside this repository.
   - **Restore:** copy the tar to the host, run `docker load -i minio-pinned-images.tar`, then confirm both tags with
     `docker image ls 'quay.io/minio/*'`.
   - **Better, and durable:** push both images into this deployment's container registry once, and have the Azure
     compose override reference the registry copies. The host then never depends on a single file.
   - **Never** run `docker image prune` or `docker system prune` on a host that holds these images. Clear the build
     cache instead (`docker builder prune`).
   - **Do not "fix" this step by deleting it, or by changing the tags to something that pulls.** No published tag of
     this release can be pulled anonymously, and a different MinIO version is an unreviewed change to the store that
     holds every document. The lasting fix is moving the documents off MinIO (lane C, item C3, deferred under
     decision 16), which is the owner's decision, not a deploy-time workaround.
7. **Bring the stack up** with the compose file, minus `sql-server` and `redis`.

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
