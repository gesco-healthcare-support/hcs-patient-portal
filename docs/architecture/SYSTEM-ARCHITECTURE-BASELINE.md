# System architecture baseline

> Complete description of what this system is and how its parts fit together, written to be read
> by someone with **no access to the repository**.
>
> **Describes what is, not what should be.** It makes no recommendations. It exists as the input to
> a system-design and infrastructure exercise, which is separate.

| Field                      | Value                                                                                                                                                                                                                                                                                                 |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Verified                   | 2026-08-28, against `origin/main`                                                                                                                                                                                                                                                                     |
| Method                     | Direct source reads, plus live queries against the running deployment                                                                                                                                                                                                                                 |
| Relationship to other docs | The 22 documents under `docs/architecture`, `docs/api`, `docs/database`, `docs/business-domain`, `docs/backend` and `docs/frontend` are all stamped **"Last verified: 2026-06-01"** and have not been re-checked. Where this document and those disagree, this one was read from source more recently |

---

## 1. What the system does

A workers' compensation Independent Medical Examination (IME) scheduling platform. Staff at a
medical-evaluation company book patients with IME doctors at specific locations and time slots,
collect the supporting documents, assemble them into a packet for the doctor, and track each
appointment through a lifecycle from initial request to billing.

**Who uses it, and this matters for exposure design:**

| Actor                               | Type     | Reaches the system how                             |
| ----------------------------------- | -------- | -------------------------------------------------- |
| IT Admin                            | internal | Host-scope console at the reserved `admin` slug    |
| Staff Supervisor, Intake            | internal | Their office subdomain                             |
| Applicant attorney                  | external | Self-register or emailed invitation                |
| Defense attorney                    | external | Self-register or emailed invitation                |
| Claim examiner (insurance adjuster) | external | Self-register or emailed invitation                |
| Patient                             | external | Emailed invitation, or an anonymous tokenised link |

The four external roles are **capability-identical**; they differ only in which records they can
see. Public exposure means anonymous internet traffic reaching registration, login, password
reset, consent-response and document-upload surfaces.

---

## 2. Component inventory

### 2.1 Backend, 10 projects

Standard ABP layered (DDD) solution. `HealthcareSupport.CaseEvaluation.*`:

| Project                 | Responsibility                                                                            |
| ----------------------- | ----------------------------------------------------------------------------------------- |
| `Domain.Shared`         | Constants, enums, localisation resources, error codes. No dependencies                    |
| `Domain`                | Entities, aggregate roots, domain services (Managers), domain events, background jobs     |
| `Application.Contracts` | DTOs, application-service interfaces, permission definitions                              |
| `Application`           | Application services (use cases), authorisation, Mapperly mappings, notification handlers |
| `EntityFrameworkCore`   | Two DbContexts, model configuration, repositories, two migration sets                     |
| `HttpApi`               | Controllers                                                                               |
| `HttpApi.Client`        | Generated client proxies                                                                  |
| `HttpApi.Host`          | **Runtime process 1** -- the API. Hosts Hangfire server, health checks, rate limiters     |
| `AuthServer`            | **Runtime process 2** -- OpenIddict authorisation server plus Razor identity UI           |
| `DbMigrator`            | **Runtime process 3** -- one-shot. Applies migrations and runs seed contributors          |

Measured surface: **56 application services, 52 HTTP controllers, 37 aggregate roots, 32 domain
managers, 23 files containing background-job definitions.**

### 2.2 Frontend

Angular 20.3.19 SPA, `@angular/build:application` builder. **87 components, 45 routed paths.**
ABP's `@abp/ng.core` 10.0.2 provides auth, config and localisation plumbing. Generated API proxies
live under `angular/src/app/proxy/` and are excluded from analysis.

**There is no server-side rendering.** No `@angular/ssr`, no `@angular/platform-server`, no server
entry file, no hydration provider, and no `ssr`/`prerender`/`server` builder target. It is a pure
client-rendered SPA served as static files by nginx.

### 2.3 Supporting services

| Service         | Role                                                                                                     |
| --------------- | -------------------------------------------------------------------------------------------------------- |
| SQL Server 2022 | Host database plus one database per office                                                               |
| Redis 7         | Three distinct jobs -- see section 6                                                                     |
| MinIO           | S3-compatible object storage, six logical containers                                                     |
| packet-renderer | Small Python/Flask service that renders document packets to PDF                                          |
| nginx (x2)      | One reverse proxy terminating TLS and routing by Host; one inside the Angular image serving static files |

---

## 3. Multi-tenancy: the single most load-bearing design decision

**One tenant = one doctor's practice ("office") = one SQL Server database.**

### 3.1 How a tenant is resolved

**From the HTTP `Host` header, and nothing else.** There is no tenant switcher, no path prefix, no
query parameter in normal operation. The leftmost DNS label is the office slug.

```text
{office}.<BASE_DOMAIN>          -> Angular SPA
{office}.api.<BASE_DOMAIN>      -> HttpApi.Host
{office}.auth.<BASE_DOMAIN>     -> AuthServer
minio.<BASE_DOMAIN>             -> MinIO S3 API (exact match, not wildcard)
admin                           -> RESERVED slug meaning host scope
minio                           -> RESERVED slug, consumed by the exact-match rule above
```

A custom `HostAwareDomainTenantResolveContributor` reads the office from the Host. A request whose
Host matches no office returns "Tenant not found". **A bare IP address cannot reach the
application at all.**

Important caveat recorded honestly: ABP registers four `__tenant` resolvers by default (query
string, header, cookie, route). Whether those are disabled in this configuration **has never been
tested**, and a remediation item exists to prove it. Treat "Host header only" as the intended
design, not a verified property.

### 3.2 How an office database is addressed

Each office's connection string is stored on its SaaS tenant record. It is **derived**, not
hand-configured: `TenantConnectionStringProvider` takes the host `Default` connection string (or
an override key `App:TenantDbTemplate`) and swaps the database name to `CaseEvaluation_{slug}`.

Consequence for infrastructure design: **office databases can be relocated to a different SQL
instance by setting one configuration key.** No code change.

### 3.3 How an office is created

`IOfficeDatabaseProvisioner.ProvisionAsync` -- one seam, two callers: the runtime "New Practice"
flow in the host UI, and the deploy-time migration handler. It applies the tenant schema (creating
the database if absent, via EF Core `Database.MigrateAsync()`) and seeds catalogues, an admin user
and the office's doctor. Operations are idempotent, so a retry after partial failure completes.

**Adding an office is currently a host-UI action, not a deployment.** That property is
load-bearing to how the business expects to grow.

### 3.4 Two DbContexts

|                    | Host                      | Tenant                          |
| ------------------ | ------------------------- | ------------------------------- |
| Class              | `CaseEvaluationDbContext` | `CaseEvaluationTenantDbContext` |
| `DbSet<>` declared | 46                        | 44                              |
| Migrations         | 90                        | 15                              |
| First migration    | 2026-01-31                | 2026-06-24                      |

Both derive from `CaseEvaluationDbContextBase<T>` and share `CaseEvaluationSharedModelConfiguration`.
**An entity mapped in both requires a migration in both sets.** Forgetting one produces an office
database missing a table, which surfaces as an invalid-object-name exception at runtime.

---

## 4. Identity, session and tokens

- **OpenIddict** authorisation server (ABP Commercial `Volo.Abp.OpenIddict.Pro`), OIDC with PKCE.
- **Access token lifetime: 15 minutes** (`CaseEvaluationAuthServerModule.cs:162`). The only
  lifetime explicitly configured; everything else is ABP/OpenIddict default.
- **Token signing** uses a certificate mounted read-only into the AuthServer container from
  `OPENIDDICT_PFX_PATH`, with its passphrase supplied by environment. **Never baked into an image.**
  Losing or replacing it invalidates every issued token.
- **ASP.NET DataProtection keys live in Redis** under `CaseEvaluation-Protection-Keys`, with
  `SetApplicationName("CaseEvaluation")` set identically in **both** the AuthServer and the API.
  That shared keyring is what lets the two processes read each other's protected payloads --
  login state and email-confirmation tokens among them.
- **Two sign-in doors exist.** Internal staff and external parties authenticate through different
  entry points; testing one does not exercise the other.
- **Dual accounts are possible and intentional.** Because identity is per-database, the same email
  address can hold a host account and an office account with separate password hashes. Password
  reset and login are therefore subdomain-scoped: resetting at the wrong subdomain appears to do
  nothing. This has generated real support incidents.

### Infrastructure consequences

1. Redis is on the **authentication critical path**. Losing its contents logs everyone out and
   breaks in-flight confirmation links.
2. The OpenIddict signing certificate must persist across deployments and be present before the
   AuthServer starts.
3. Any load balancer must preserve the `Host` header end to end (section 3.1).

---

## 5. Runtime processes and startup order

`docker-compose.prod.yml` defines ten services; eight are long-running.

```text
sql-server ──┐
redis ───────┼──> db-migrator (one-shot, must exit 0)
minio ───────┘         │
   │                   ├──> authserver ──┐
   └──> minio-init     └──> api ─────────┼──> reverse-proxy  [ports 80, 443]
        (one-shot)          │            │
                            └── packet-renderer
                                              angular ───────┘
```

- `db-migrator` runs **on every bring-up**, applies migrations and seeds, then exits. `authserver`
  and `api` both gate on `service_completed_successfully`.
- Healthchecks: `/health-status` on api and authserver, `start_period: 120s`.
- `reverse-proxy` is the **only** service publishing host ports.
- Memory caps: api 2 GB, authserver 1500 MB, packet-renderer 1500 MB, sql-server 10 GB with
  `MSSQL_MEMORY_LIMIT_MB` 7168, redis 512 MB, minio 1 GB, angular 256 MB, proxy 256 MB.

**Known operational trap:** nginx resolves `proxy_pass` upstreams once at worker start. Only the
`minio` block carries the `resolver` plus variable `proxy_pass` fix. After any backend rebuild the
reverse proxy must be force-recreated or routing silently breaks on stale container IPs.

---

## 6. Redis: three jobs, all load-bearing

Redis is not merely a cache here. It carries three distinct responsibilities:

1. **Distributed cache** -- ABP `AbpCachingStackExchangeRedisModule`. Permission grants, dynamic
   claims, settings, application configuration.
2. **DataProtection keyring** -- section 4. Shared between two processes.
3. **Distributed lock** -- `Medallion.Threading.Redis`, registered as
   `RedisDistributedSynchronizationProvider` in both the API and the AuthServer.

Persistence is AOF (`--appendonly yes`) with a named volume.

**No application-level lock acquisition sites were found in `src`.** The provider is registered
for ABP framework use (background job scheduling, seeding coordination). That is worth verifying
independently before assuming lock traffic is negligible.

**Cache key shape** matters for any move to a shared cache: ABP's typed `IDistributedCache<T>`
prefixes tenant-scoped keys with `t:{tenantId},`. Untyped usage does not. Key shapes observed:
`pn:R,pk:{roleName}`, `pn:U,pk:{userGuid}`, `pn:C,pk:{clientId}`.

---

## 7. Background processing

**Hangfire**, `Hangfire.SqlServer` 1.8.21 with `Volo.Abp.BackgroundJobs.HangFire`.

- Storage is the **host** `Default` connection string, so the Hangfire schema lives in the host
  database.
- `QueuePollInterval = TimeSpan.Zero` -- aggressive polling, which means a continuous query floor
  against the host database even at idle.
- `DisableGlobalLocks = true`, `SlidingInvisibilityTimeout = 5 min`, `CommandBatchMaxTimeout = 5 min`.
- Worker pool is explicitly pinned (default would be `ProcessorCount * 5`), because a burst
  oversubscribes the 2-worker packet renderer and the SMTP relay into timeouts and retries.
- **Only `HttpApi.Host` runs the processing server.** The AuthServer sets
  `IsJobExecutionEnabled = false`.

### The twelve recurring jobs

| Job                                     | Cron           | Frequency    |
| --------------------------------------- | -------------- | ------------ |
| `case-tracker-reconciliation`           | `*/15 * * * *` | every 15 min |
| `case-tracker-failure-alert`            | `*/15 * * * *` | every 15 min |
| `approval-reconciliation`               | `*/15 * * * *` | every 15 min |
| `case-tracker-completeness-sweep`       | `0 * * * *`    | hourly       |
| `change-request-consent-expiry-sweep`   | `30 * * * *`   | hourly       |
| `appt-jdf-auto-cancel`                  | `0 6 * * *`    | daily 06:00  |
| `appt-day-reminder`                     | `0 7 * * *`    | daily 07:00  |
| `appt-cancellation-reschedule-reminder` | `0 8 * * *`    | daily 08:00  |
| `appt-request-scheduling-reminder`      | `0 8 * * *`    | daily 08:00  |
| `appt-duedate-approaching`              | `15 8 * * *`   | daily 08:15  |
| `appt-internal-staff-queue-digest`      | `15 9 * * *`   | daily 09:15  |
| `appt-draft-cleanup`                    | `0 3 * * *`    | daily 03:00  |

**These jobs iterate offices.** A recurring sweep opens a connection per office database inside a
`ICurrentTenant.Change(officeId)` scope. At 11 offices that is 11 sequential connections per
sweep; at 33 it is 33. The three 15-minute jobs therefore drive the steady-state connection
pattern more than user traffic does.

**A known correctness hazard:** ABP falls back to the ambient tenant when a job argument does not
carry one, and the ambient tenant in a worker is null, which resolves to the **host** database.

---

## 8. Storage and documents

MinIO, S3-compatible, addressed **path-style** (bucket in the path) because the wildcard TLS
certificate covers a single label only.

Six logical blob containers:

| Container               | Holds                                      |
| ----------------------- | ------------------------------------------ |
| `appointment-documents` | Documents uploaded against an appointment  |
| `appointment-packets`   | Rendered PDF packets for the doctor        |
| `anonymous-uploads`     | Uploads through the tokenised public link  |
| `document-packages`     | Assembled bundles                          |
| `joint-declarations`    | Signed joint declaration forms             |
| `master-documents`      | Office-level template and master documents |

Two physical buckets are provisioned: the application bucket and a **partner-facing bucket** for
document exchange with an external organisation.

Two facts that constrain any storage migration:

1. **The application authenticates to MinIO as root** (`MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD`).
2. **An external partner already holds scoped credentials** against `minio.<domain>` and the
   partner bucket, with a policy granting fetch-by-key and deliberately withholding bucket
   listing. Moving to native object storage means re-credentialing that partner on their schedule.

Object keys are ABP-shaped `tenants/{tenantId:D}/...` with a **lowercase** GUID.

---

## 9. Notifications and email

- Email is the spine of the external-party workflow: invitations, confirmations, consent links,
  password resets, reminders, packet delivery.
- A **durable per-recipient outbox** (`AppNotificationOutboxItems`) records delivery attempts;
  a drain job sends and records outcome.
- Transport is SMTP over port 587 with STARTTLS to an authenticated relay, configured as ABP
  settings via `Settings__Abp.Mailing.*` environment variables. `appsettings.json` ships
  `REPLACE_ME_LOCALLY` placeholders, so production **must** override or mail silently misconfigures.
- Addressing model is deliberately **ex-parte-safe**: one message addressed to a primary party
  with the other parties CC'd, rather than separate per-party messages for the same notice.
- In-app notifications exist as database rows (`AppAppNotifications`), polled by the SPA.

**There is no realtime channel.** No SignalR package, no hub, no client. The reverse-proxy config
still carries a WebSocket upgrade `map` and `Upgrade`/`Connection` headers with a comment
referencing an "ABP SignalR notification hub" -- **that hub does not exist in this codebase.** Any
infrastructure design can assume no long-lived WebSocket connections and no sticky-session
requirement for realtime.

---

## 10. External integrations

### 10.1 Case Tracker (partner system)

A separate portal operated by the same business, on a different stack. This application pushes
appointment and attendance data to it and reconciles state back.

- **Outbound:** a durable `AppIntegrationOutboxItems` ledger per office, drained by jobs. Message
  kinds cover intake handoff, documents, attendance outcomes and packet publication.
- **Inbound:** a reconcile endpoint they call, gated by a token this application issues.
- **Both directions are configured to fail closed.** `CaseTracker__BaseUrl` is deliberately blank
  by default, so a misconfigured deploy produces a transport error the outbox records rather than
  posting ePHI to a plausible wrong host. The inbound validator refuses every request when its
  token is unset.
- A dead-letter surface exists in the host UI for failed integration messages.

### 10.2 SMTP relay

Section 9. External, authenticated, port 587.

---

## 11. What is deliberately absent

Stated because their absence simplifies infrastructure design and would otherwise be assumed:

| Absent                      | Consequence                                                                                      |
| --------------------------- | ------------------------------------------------------------------------------------------------ |
| Server-side rendering       | No Node runtime in production. Static file serving only                                          |
| SignalR / WebSockets        | No sticky sessions needed for realtime. The nginx upgrade config is dead                         |
| SQL Server Agent            | Scheduling is entirely in-process via Hangfire                                                   |
| Raw SQL anywhere in `src`   | No cross-database queries, no three-part names, no linked servers. Verified by exhaustive search |
| Cross-database joins        | Host and tenant data are joined in memory when at all, never in SQL                              |
| Message broker              | No RabbitMQ/Kafka. ABP's local event bus is in-process; durability is the two database outboxes  |
| Second application instance | Everything runs single-instance today                                                            |

---

## 12. Failure and coupling map

What breaks what, for availability design:

| If this fails                | Effect                                                                                                                     |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Redis loses data             | Every session invalid, in-flight email-confirmation links broken, distributed lock unavailable                             |
| Redis unreachable            | Authentication and cached permission resolution fail                                                                       |
| SQL host database            | Everything. Identity, tenant registry, Hangfire, audit                                                                     |
| One office database          | That office only; others unaffected. **This is the main benefit of db-per-tenant**                                         |
| `db-migrator` fails mid-loop | Offices left on split schema versions with **no report of which succeeded** -- the runner has no per-tenant error handling |
| MinIO                        | Document upload, packet generation and partner exchange. Booking still works                                               |
| packet-renderer              | Packet generation only; `api` health gate depends on it at startup                                                         |
| SMTP relay                   | All external-party workflow stalls silently into the outbox                                                                |
| Reverse proxy                | Total outage; it is the only published service                                                                             |
| OpenIddict certificate lost  | Every issued token invalid; all users must re-authenticate                                                                 |

---

## 13. Source map

| Topic                                                        | Location                                                                     |
| ------------------------------------------------------------ | ---------------------------------------------------------------------------- |
| Compose topology                                             | `docker-compose.prod.yml`                                                    |
| Host routing                                                 | `docker/nginx-proxy/default.conf.template`                                   |
| API module wiring, Hangfire, Redis, rate limits              | `src/*.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`                      |
| OpenIddict, DataProtection, token lifetime                   | `src/*.AuthServer/CaseEvaluationAuthServerModule.cs`                         |
| DbContexts and shared model config                           | `src/*.EntityFrameworkCore/EntityFrameworkCore/`                             |
| Tenant connection strings                                    | `src/*.Domain/Data/TenantConnectionStringProvider.cs`                        |
| Office provisioning                                          | `src/*.Domain/Data/OfficeDatabaseProvisioner.cs`                             |
| Recurring jobs                                               | `src/*.Domain/**/Jobs/`                                                      |
| Blob containers                                              | `src/*.Domain/BlobContainers/`                                               |
| Case Tracker                                                 | `src/*.Domain/Integration/CaseTracker/`                                      |
| Older architecture docs (all stamped 2026-06-01, unverified) | `docs/architecture/`, `docs/api/`, `docs/database/`, `docs/business-domain/` |
