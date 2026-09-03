# Patient Portal -- Hosting and Production Deployment: Research Input Pack

> Companion to `2026-08-26-engineering-status.md`. That document says what state the codebase
> and CI are in. **This one says what a hosting/production-deployment plan has to work with**:
> the architecture as actually deployed, the constraints that are non-negotiable, the gaps that
> a public deployment must close, and the questions only the business can answer.
>
> Written to be handed to someone with no prior context on this project.

| Field                   | Value                                                            |
| ----------------------- | ---------------------------------------------------------------- |
| Prepared                | 2026-08-26                                                       |
| Codebase at             | `origin/main` `bc4f2029`                                         |
| Current deployment      | `development` `8695cd72`, single LAN VM, internal-only           |
| Target being researched | **Public-facing production hosting**                             |
| Regulatory context      | US healthcare, **PHI / HIPAA**, California workers' compensation |

---

## 1. What the application is

A workers' compensation Independent Medical Examination (IME) scheduling platform. the business staff
book patients with IME doctors at specific locations and time slots, then track each
appointment through a 13-state lifecycle from request through billing.

**Multi-tenant, database-per-tenant.** One tenant = one doctor's practice ("office"). Each
office gets its own SQL Server database (`CaseEvaluation_<office>`); a single host database
(`CaseEvaluation`) holds cross-office reference data and host-level identity.

**Tenancy is resolved from the Host header only.** There is no tenant switcher, no path prefix,
no query parameter. The subdomain layout is load-bearing:

```text
{office}.<BASE_DOMAIN>          -> Angular SPA
{office}.api.<BASE_DOMAIN>      -> ASP.NET Core API  (HttpApi.Host)
{office}.auth.<BASE_DOMAIN>     -> OpenIddict / ABP identity UI (AuthServer)
minio.<BASE_DOMAIN>             -> MinIO S3 API (partner document exchange)
admin                           -> RESERVED slug meaning "host scope"
minio                           -> RESERVED slug (exact-match server block)
```

**This single design decision drives most of the hosting requirements below** -- DNS, TLS,
certificate shape, routing, and CDN eligibility all follow from it.

**Who uses it**: internal the business staff (IT admin, supervisors, intake) plus four external party
roles -- applicant attorneys, defense attorneys, claim examiners, and patients. External users
self-register or arrive by emailed invitation. **A public deployment means anonymous internet
traffic reaching the registration, login, password-reset, and public-document-upload surfaces.**

---

## 2. Current deployment (the thing being replaced or extended)

### 2.1 Host

| Item          | Value                                                                                |
| ------------- | ------------------------------------------------------------------------------------ |
| Machine       | `<app-vm>`, Ubuntu 24.04.4 LTS, x86_64                                         |
| Resources     | **4 vCPU / 16 GB RAM / 48 GB disk**                                                  |
| Disk headroom | ~9.7 GB free (79% used) after the 2026-08-25 image build                             |
| Addresses     | LAN `<LAN-IP>`; VPN `<VPN-IP>` (VPN does not route to the LAN subnet)      |
| Runtime       | Docker Engine 29.6.1 + compose plugin v5.3.1                                         |
| App path      | `/home/<deploy-user>/hcs-patient-portal`                                                   |
| Access        | `ssh <ssh-alias>` (SSH config alias), key `~/.ssh/<deploy-key>`, passwordless sudo |
| Deploy branch | `development`                                                                        |

Sourced from the server-rollout record; **not re-verified on 2026-08-26.** Treat resource
numbers as accurate to 2026-08-25.

### 2.2 Domain and TLS -- the single biggest blocker to going public

```text
BASE_DOMAIN = <app>.<corp-zone>.local
```

`<corp-zone>.local` is the **corporate Active Directory DNS zone**. Consequences:

- **`.local` is not a public TLD and cannot be resolved on the internet.**
- **No public CA can issue a certificate for it.** The current certificate is issued by the
  internal `<Internal-Root-CA>` (CN `<app>.<corp-zone>.local`, 4 SANs = base plus three
  wildcards, RSA-2048, valid Jul 2026 to Jul 2028).
- Trust depends on the internal root CA being installed on each client. On-prem GPO covers
  domain-joined PCs; **Azure-AD/Intune devices need a separate Intune trusted-root profile**,
  which was still an open gate with IT.
- Three wildcard DNS records are required and were only made to synthesise correctly on IT's
  second attempt: `*.appointment-portal...`, `*.api.appointment-portal...`,
  `*.auth.appointment-portal...`.

**Going public therefore requires a real registrable domain and a public certificate covering
three separate wildcard levels.** A single `*.example.com` certificate does NOT cover
`office.api.example.com` -- wildcards match one label. So a public deployment needs SANs for
`*.<domain>`, `*.api.<domain>`, and `*.auth.<domain>` (plus the apex). With Let's Encrypt that
forces **DNS-01 validation**, which in turn constrains the DNS provider choice to one with an
API the ACME client supports.

### 2.3 Runtime topology

`docker-compose.prod.yml`, ten service definitions, eight long-running:

| Service           | Image / build                                | Memory cap                         | Published?                                      |
| ----------------- | -------------------------------------------- | ---------------------------------- | ----------------------------------------------- |
| `reverse-proxy`   | `nginx:alpine`                               | 256m                               | **Yes -- 80 and 443, the only published ports** |
| `angular`         | built, nginx static                          | 256m                               | no                                              |
| `api`             | built, `HttpApi.Host`                        | 2g                                 | no                                              |
| `authserver`      | built, `AuthServer`                          | 1500m                              | no                                              |
| `packet-renderer` | built, Python/Flask                          | 1500m                              | no                                              |
| `sql-server`      | `mcr.microsoft.com/mssql/server:2022-latest` | 10g (`MSSQL_MEMORY_LIMIT_MB` 7168) | no                                              |
| `redis`           | `redis:7-alpine` (AOF on)                    | 512m                               | no                                              |
| `minio`           | `minio/minio:latest`                         | 1g                                 | no                                              |
| `db-migrator`     | built, one-shot                              | --                                 | no                                              |
| `minio-init`      | `minio/mc`, one-shot                         | --                                 | no                                              |

Named volumes: `sqldata`, `redisdata`, `miniodata`. Logging: `json-file`, 10 MB x 5 per
service.

Notable behaviours worth carrying into any redesign:

- **Redis AOF persistence is load-bearing.** It holds the ASP.NET DataProtection keyring shared
  between `api` and `authserver`. Lose it and every session drops and every pending
  email-confirmation link breaks.
- `db-migrator` runs EF migrations plus seed contributors on every bring-up, and `authserver`
  and `api` both gate on `service_completed_successfully`.
- Split-horizon OIDC: `api` reaches the AuthServer internally
  (`AuthServer__MetaAddress: http://authserver:8080`) but validates tokens against the public
  issuer.
- `packet-renderer` is a separate Python service that renders document packets.

### 2.4 Deployment process

**Entirely manual. There is no CD.** Despite the name, `.github/workflows/deploy-dev.yml` only
builds, tests, and opens the next cascade PR -- it never touches the server.

The working procedure is:

```bash
bash scripts/hosting/backup-databases.sh
git pull --ff-only origin development
docker compose --env-file secrets/env.prod -f docker-compose.prod.yml build db-migrator api authserver angular
docker compose --env-file secrets/env.prod -f docker-compose.prod.yml up -d
docker compose --env-file secrets/env.prod -f docker-compose.prod.yml up -d --force-recreate reverse-proxy
```

Two traps that have each broken the stack in practice and must survive into any replacement:

1. **`--env-file secrets/env.prod` is mandatory on every compose invocation.** There is no
   `.env` in the repo root, so compose auto-loads nothing. Without the flag every secret
   resolves to a blank string and `up -d` recreates the containers with no DB password, no TLS
   paths, and no base domain.
2. **The reverse proxy must be force-recreated after any backend rebuild.** nginx resolved
   `proxy_pass http://authserver:8080` once at worker start and cached the IP; a backend rebuild
   moves container IPs and routing silently breaks. The MinIO block has since been fixed with
   `resolver 127.0.0.11 valid=10s` plus a variable `proxy_pass`; **the api/auth/angular blocks
   have not**, so the manual step is still required.

### 2.5 Configuration surface

35 variables in `env.prod.example`. Grouped:

- **Identity/TLS**: `BASE_DOMAIN`, `TLS_CERT_PATH`, `TLS_KEY_PATH`, `OPENIDDICT_PFX_PATH`,
  `AUTHSERVER_CERT_PASSPHRASE`
- **Data**: `MSSQL_SA_PASSWORD`, `STRING_ENCRYPTION_PASSPHRASE`, `MINIO_ROOT_USER`,
  `MINIO_ROOT_PASSWORD`, `MINIO_BUCKET_NAME`, `MINIO_CASE_TRACKER_BUCKET_NAME`
- **Licensing**: `ABP_LICENSE_CODE`, `ABP_NUGET_API_KEY` (ABP **Commercial** -- a paid licence
  is required to build)
- **Mail**: 7 `SMTP_*` variables (currently `<smtp-relay>:587` STARTTLS)
- **Partner integration**: 4 `CASE_TRACKER_*` (Case Tracker portal, deliberately blank by
  default so a misconfigured deploy fails closed rather than POSTing ePHI to a wrong host)
- **Ops**: `BACKUP_DIR`, `BACKUP_RETENTION_DAYS`, 4 memory caps, `HTTP_PORT`, `HTTPS_PORT`,
  `DBMIGRATOR_ENVIRONMENT`

Secrets live in a single mode-600 file at `~/hcs-patient-portal/secrets/env.prod` on the box.
No secret manager, no rotation mechanism, no audit of access.

### 2.6 Backup

`scripts/hosting/backup-databases.sh` enumerates `CaseEvaluation` plus every `CaseEvaluation_*`
from `sys.databases` and runs native `BACKUP DATABASE` inside the container to a bind-mounted
host directory, pruning past `BACKUP_RETENTION_DAYS` (default 14).

Status: **works, but is not yet a backup strategy.**

- It had never actually run until 2026-07-22 (the `./backups` directory was `root:root 755` and
  the mssql container uid 10001 could not write it).
- `BACKUP_DIR` still defaults to `./backups` **on the same box and the same disk**.
- No cron schedule was confirmed installed.
- **No restore has ever been tested.**
- `.bak` files contain PHI once real data exists.

There is also `scripts/hosting/backup-offbox.sh`, and SonarCloud flags a clear-text protocol in
it (`shell:S5332` at line 80) -- worth reading before relying on it.

---

## 3. Hard constraints

These are not preferences. A hosting proposal that violates one of them does not work.

1. **HIPAA.** The system stores and transmits PHI. Any third-party infrastructure provider must
   sign a **Business Associate Agreement** before real patient data touches it. This governs
   provider choice, region, logging destinations, and email relay equally.
2. **Host-header tenancy.** Anything that rewrites, normalises, or collapses the Host header
   breaks tenant resolution outright. This constrains CDN, load-balancer, and ingress choices,
   and it means health checks and internal callers must use a real office host
   (`admin.api.<domain>` for host scope) rather than a bare hostname or IP. A bare IP returns
   "Tenant not found".
3. **Three-level wildcard TLS.** See [2.2](#22-domain-and-tls----the-single-biggest-blocker-to-going-public).
4. **ABP Commercial licence.** `ABP_LICENSE_CODE` and `ABP_NUGET_API_KEY` are required at build
   time. Build infrastructure must be able to hold these secrets.
5. **SQL Server.** EF Core is configured against SQL Server
   (`Volo.Abp.EntityFrameworkCore.SqlServer`) with 90 host and 15 tenant migrations already
   written. Changing engine is a rewrite, not a configuration change.
6. **Database-per-office scaling.** Every new office adds a database. Whatever hosts SQL must
   support dynamic database creation and per-database backup. **The migration runner has no
   per-tenant error handling** -- one office failing mid-loop leaves the fleet on split schema
   versions with no report (logged in `docs/backlog.md`).
7. **Shared DataProtection keyring.** `api` and `authserver` must share it. If they are ever
   scaled to more than one instance each, the keyring must be shared storage, not per-container.
8. **Outbound SMTP.** Account setup, invitations, confirmations, consent links, and reset links
   all depend on deliverable mail. A sibling project has already lost mail to Microsoft 365
   filtering on raw-IP links, so sender reputation and link hostnames matter.

---

## 4. Gaps a public deployment must close

Ordered by how much they matter once the app faces the open internet. Each is evidenced, not
speculative.

### 4.0 An unauthenticated administrative interface is mounted in the production path

> Added 2026-08-31, after the rest of this document was written. Numbered 4.0 to preserve the
> existing numbering; it belongs first on severity.

The Hangfire job dashboard is served at `/hangfire` with an authorisation filter whose entire
body returns `true`:

```csharp
public class AnonymousHangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
```

It is mounted inside `if (!AbpStudioAnalyzeHelper.IsInAnalyzeMode)`, which is an ABP Studio
guard, **not an environment gate**. The dashboard is therefore present in production, and it
is registered with `IgnoreAntiforgeryToken = true`.

What it exposes to anyone who reaches the URL: every queued, completed and failed job with the
arguments it was called with and full exception detail, plus buttons to trigger, requeue and
delete jobs. Job arguments carry appointment identifiers and recipient addresses. This is an
unauthenticated administrative console on a system holding ePHI.

A fix is planned and is small. It is stated here because it is load-bearing for the design
questions this brief asks: it means the edge cannot assume every application route is either
public-by-design or authenticated, and any answer about admin-surface exposure, network
segmentation or WAF path rules has to account for administrative routes reachable without
credentials. Assume the code fix lands; design as though administrative surfaces still need
their own containment.

### 4.1 Edge security: nothing is configured

`docker/nginx-proxy/default.conf.template` (157 lines) contains **zero `add_header`
directives**. There is:

- no HSTS
- no Content-Security-Policy
- no X-Frame-Options / frame-ancestors
- no X-Content-Type-Options
- no Referrer-Policy or Permissions-Policy
- no `ssl_protocols` / `ssl_ciphers` pinning (nginx defaults apply)
- no `server_tokens off`
- **no `limit_req` rate limiting of any kind**
- no WAF, no bot protection, no DDoS absorption

Application-tier rate limiting exists but is narrow. **Corrected 2026-08-31 after reading the
source:** the single `AddRateLimiter` in the solution
(`CaseEvaluationHttpApiHostModule.cs:628`) covers password reset, the public document upload,
the API registration endpoint and the partner integration path. Every other business endpoint
falls through to no limiter.

The structurally important part: **sign-in is served by the AuthServer, a separate process that
registers no rate limiter at all.** The AuthServer's own Razor pages also call application
services in-process, so API middleware never runs for them -- meaning a limiter added as
middleware in one process does not protect the equivalent flow in the other. This is a
two-process problem, not a missing-attribute problem, and it constrains where throttling can
usefully live (edge, per-process middleware, or shared application service).

This was a reasonable posture for a LAN behind corporate network controls. It is not a posture
for a public host.

### 4.2 No default server on 443, and an apex with no route

There are four 443 server blocks: `*.auth.<base>`, `*.api.<base>`, `minio.<base>`, `*.<base>`.
There is no `default_server` on 443 and **no block for the bare apex**, because an nginx
wildcard label cannot be empty.

Two consequences, the first already confirmed live:

- `api.<base>` (no office label) does not match `*.api.<base>`; it falls through to `*.<base>`
  and is served by the **Angular container**. `App__SelfUrl: https://api.${BASE_DOMAIN}` in
  `docker-compose.prod.yml` therefore points at a host that serves the SPA. Inert today because
  all real API traffic is office-prefixed, but wrong if anything ever relies on `SelfUrl`.
- By the same rule the bare apex `<base>` matches nothing and falls to the **first** 443 block,
  the AuthServer. `App__AngularUrl` and the OpenIddict `RootUrl` are both configured to the
  apex. **This needs confirming on a public deployment**, where users will type the apex and
  where `www.` will also need a decision. It is called out here as a question, not asserted as a
  live bug.

### 4.3 SQL Server licensing

`docker-compose.prod.yml:38` sets `MSSQL_PID: "Developer"`, with the comment already in the file:

> Developer edition is licensed for dev/test (this LAN box is UAT). Switch to a licensed edition
> (Express/Standard) before any real-patient production use.

**Developer edition may not be used in production.** This is a licensing and cost decision that
belongs in the hosting research: Express (free, 10 GB per database, 1410 MB buffer pool),
Standard, or a managed service (Azure SQL Database / Managed Instance, AWS RDS for SQL Server).
The db-per-office model interacts strongly with per-database pricing -- **this is likely the
single largest cost variable in the whole exercise.**

### 4.4 Database credentials

Every service connects as `sa`:

```text
ConnectionStrings__Default: "Server=sql-server;Database=CaseEvaluation;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True"
```

One shared, all-powerful account for app, migrator, and auth server, with
`TrustServerCertificate=True` disabling server certificate validation. Least privilege would
give the migrator DDL rights and the runtime app DML only. `TrustServerCertificate=True` is
tolerable on a private docker network and is not tolerable if the database ever moves off-host.

### 4.5 Single point of failure, everywhere

One VM. One SQL container. One Redis. One MinIO. One nginx. No replication, no failover, no
health-based restart beyond `restart: unless-stopped`, no second availability zone. A host
reboot is a full outage; a disk failure is data loss back to the last `.bak` (which is on the
same disk).

Disk is also tight: **9.7 GB free of 48 GB** after the last image build, before any real data or
document volume.

### 4.6 No observability

Serilog writes console and file sinks. `AspNetCore.HealthChecks.UI` uses **in-memory storage**,
so health history does not survive a restart. There is:

- no centralised log aggregation
- no metrics or APM
- no OpenTelemetry
- no alerting -- nobody is paged when anything breaks
- no uptime monitoring

Container logs are capped at 50 MB per service and rotate away.

For a HIPAA system, audit logging deserves separate treatment from application logging. ABP's
`Volo.Abp.AuditLogging` is present; whether its retention satisfies policy is an open question.

### 4.7 Dependency exposure

88 open Dependabot alerts, all npm, including six Angular CVEs that ship to the browser. Three
are cross-request or cross-user data-exposure shapes (`HttpTransferCache` cache-key ambiguity,
credentialed-response caching, hydration cache poisoning) which matter more than usual in a
multi-tenant PHI application. Detail in the status document, section 5.4.

31 SonarCloud security hotspots are untriaged, including 6 HIGH csrf and 3 HIGH auth.

### 4.8 No staging environment

`staging` and `production` branches have not moved since 2026-05-01 and are 600+ commits behind.
There is exactly one environment. Any change that reaches users has been tested only on the same
box that serves them, or locally.

### 4.9 Secrets handling

A single mode-600 file on the box. The server-rollout record notes that several values were
pasted into chat transcripts during setup and are flagged for rotation, and that a second
redundant deploy key was installed and should be pruned. There is no vault, no rotation, no
access audit. `docs/security/SECRETS-MANAGEMENT.md` exists and should be read alongside this.

### 4.10 Known functional gaps that affect go-live

- **Host-scope password reset silently sends nothing.** `ExternalAccountAppService` dispatches a
  per-tenant template; for a user with no `TenantId` it logs "skipping send" and the page shows
  generic success anyway. Internal staff who reset at `admin.<base>` get no email. Tenant scope
  works end to end. Adrian wants this fixed; it is deferred.
- **Dual-account confusion.** The same email can hold both a host account and a per-office
  account with separate password hashes. Reset and login are subdomain-scoped, so resetting at
  the wrong subdomain appears to do nothing. This has already generated two real support
  incidents.
- **Public document upload page is unreachable.** The route and endpoint work, but
  `BuildPublicDocumentUploadUrlAsync` has no callers, so the link is never emailed.

---

## 5. What already exists and should not be rebuilt

| Asset                                     | Where                                                                                                                 |
| ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Production compose stack                  | `docker-compose.prod.yml` (well commented, decisions explained inline)                                                |
| Local-seed override                       | `docker-compose.prod.localseed.yml`                                                                                   |
| Reverse proxy config                      | `docker/nginx-proxy/default.conf.template`                                                                            |
| Dockerfiles (multi-stage, `prod` targets) | `angular/`, `src/*.AuthServer/`, `src/*.HttpApi.Host/`, `src/*.DbMigrator/`, `docker/packet-renderer/`                |
| Backup + restore                          | `scripts/hosting/backup-databases.sh`, `backup-offbox.sh`, `docs/runbooks/hosting-backup-restore.md`                  |
| Certificate generation                    | `scripts/hosting/gen-local-certs.sh`, `gen-openiddict-cert.sh`                                                        |
| Compose wrapper                           | `scripts/hosting/dc.sh`                                                                                               |
| systemd units                             | `scripts/hosting/systemd/`                                                                                            |
| Verification runbook                      | `docs/runbooks/hosting-local-verification.md`                                                                         |
| Tenant isolation gate                     | `docs/runbooks/database-per-office-go-live-isolation-gate.md`                                                         |
| Security documentation                    | `docs/security/` -- THREAT-MODEL, HIPAA-COMPLIANCE, SECRETS-MANAGEMENT, DATA-FLOWS, AUTHORIZATION, SESSION-AND-TOKENS |
| Multi-tenancy architecture                | `docs/architecture/MULTI-TENANCY.md`, `ABP-FRAMEWORK.md`, `OVERVIEW.md`                                               |
| Database design                           | `docs/database/` -- EF-CORE-DESIGN, MIGRATION-GUIDE, SCHEMA-REFERENCE, DATA-SEEDING                                   |

**Read `docs/security/THREAT-MODEL.md` and `HIPAA-COMPLIANCE.md` before proposing an
architecture.** They were not audited for freshness in this pass, but they represent prior
reasoning that should be engaged with rather than bypassed.

---

## 6. Facts that make this easier than it looks

Worth stating, because the gap list above reads worse than the actual position.

1. **There is no real patient data yet.** Confirmed by Adrian on 2026-08-18: every appointment,
   patient, and party record on the box is synthetic and created for testing. Staff go-live has
   not happened. **A migration to public hosting can therefore happen before any PHI migration
   burden exists.** This is a large and time-limited advantage. It expires the moment real
   practices take real bookings.
2. **The prod stack is proven to build and boot.** Full-stack builds and health checks have been
   exercised repeatedly since 2026-07-15, including tenant isolation assertions through the
   proxy (`admin` 200, `bogus.api` 404).
3. **Only two databases exist in production today** -- host plus `CaseEvaluation_<office-a>`.
   The launch target is **11 offices**, so 10 of the 11 tenant databases have never been
   created. They get provisioned fresh on whatever the target platform turns out to be, which
   means there is almost nothing to migrate and the platform choice is not constrained by
   existing data volume.
4. **The .NET dependency tree is clean.** Zero NuGet Dependabot alerts.
5. **The subdomain design already anticipates offices.** Adding one is a host-UI action, not a
   deployment.
6. **Everything is already containerised**, with prod-target images and no source bind mounts.
   The stack is portable to any Docker-capable host; the constraints are DNS, TLS, and the
   database, not packaging.

---

## 7. Questions only the business can answer

A hosting recommendation cannot be finalised without these. They are listed so the research can
present options conditioned on each answer rather than stalling.

**Domain and exposure**

1. What public domain will this use? Is one registered?
2. Is the whole application public, or only the external-party surfaces, with staff access
   staying on VPN/LAN?
3. Does the internal `.local` deployment continue in parallel, or is it replaced?

**Regulatory and commercial**

4. Which providers will the business sign a BAA with? Is there an existing cloud relationship
   (Microsoft 365 is already in use for mail)?
5. What is the budget envelope, monthly? This decides managed-vs-self-hosted SQL more than any
   technical factor.
6. Is there an existing SQL Server licence, or must the hosting cover it?

**Scale and service level**

7. ~~How many offices in year one, and what is the ceiling?~~ **ANSWERED 2026-08-26 (Adrian):
   11 offices today (11 practices, one doctor each, so 11 tenants and 11 databases), and the
   platform must be able to reach 22-33 without re-architecting.**
   The headroom does not need provisioning or paying for at launch, but the path to it must be
   a config or tier change rather than a migration.
8. Expected concurrent users, appointments per day, and document storage volume per month?
9. What downtime is acceptable? Is a maintenance window available, and is out-of-hours paging
   expected from a one-person team?
10. What are the recovery point and recovery time objectives -- how much data may be lost, and
    how long may recovery take?

**Operations**

11. Who operates this in production? Today it is one developer with no on-call rotation, and
    that is a genuine single point of failure independent of the infrastructure.
12. Does the business IT participate in the public deployment, or is it wholly the dev's remit? IT
    currently controls DNS and the internal CA.
13. Is there a data residency requirement (California / US-only)?

---

## 8. Suggested scope for the research

Not prescriptive, but these are the axes where the constraints above actually bite:

- **Deployment target**: managed containers, VMs, or PaaS -- evaluated against the
  Host-header constraint and BAA availability.
- **SQL hosting**: the largest cost variable, with db-per-tenant as the deciding factor.
- **Edge**: TLS termination with three-level wildcard certificates, ACME DNS-01 automation, WAF,
  rate limiting, DDoS posture, security headers.
- **Object storage**: keep MinIO or move to native S3/Blob, given a partner already has scoped
  MinIO credentials for document exchange.
- **Secrets**: managed store, and how build-time ABP licence secrets reach CI.
- **CI/CD**: there is none today; what a safe automated deploy looks like given the migration
  runner has no per-tenant error handling.
- **Observability and alerting**: log aggregation, metrics, uptime, and HIPAA audit-log
  retention as a distinct concern.
- **Backup and DR**: off-box destination, tested restore, and per-office backup at scale.
- **Environments**: whether to revive `staging` or build something else.

Each option should be costed at the year-one scale from question 7, and each should state
explicitly whether the provider offers a BAA.

---

## 9. Provenance

| Verified directly on 2026-08-26                                 | Carried from prior records, not re-verified     |
| --------------------------------------------------------------- | ----------------------------------------------- |
| Git state, branches, PRs, commits                               | Server hardware specs and disk headroom         |
| CI workflows, runs, branch protection                           | Live container health and running SHA           |
| SonarCloud metrics and issues (public API)                      | The 2026-08-18 "no real patient data" statement |
| CodeQL, Scorecard, Dependabot alerts                            | IT DNS and certificate delivery status          |
| `docker-compose.prod.yml`, nginx template, Dockerfile inventory | The deploy procedure and its two traps          |
| Migration counts, DbContext shape                               | SMTP relay configuration and behaviour          |
| `env.prod.example` variable surface                             | The host-scope password reset defect            |
| Package versions, test counts, bundle budgets                   |                                                 |

Anything in the right-hand column should be re-confirmed on the box before a plan depends on it.
