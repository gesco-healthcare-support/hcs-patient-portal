# Runtime and data profile

> Measured facts about what this system actually consumes and produces, taken from the running
> deployment rather than estimated.
>
> **Describes what is, not what should be.** Companion to
> `docs/architecture/SYSTEM-ARCHITECTURE-BASELINE.md`.

| Field                | Value                                                                                                                                 |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Measured             | 2026-08-28, live queries against the running deployment                                                                               |
| Environment measured | The single internal VM. **This is a test environment.** No real patient data exists; every record was created by the team for testing |

---

## 1. The machine

| Item            | Value                                                     |
| --------------- | --------------------------------------------------------- |
| OS              | Ubuntu 24.04.4 LTS, x86_64                                |
| Resources       | **4 vCPU / 16 GB RAM / 48 GB disk**                       |
| Disk headroom   | ~9.7 GB free (79% used) after an image build              |
| Runtime         | Docker Engine 29.6.1, compose plugin v5.3.1               |
| Exposure        | Internal corporate network only. Never publicly reachable |
| Container count | 8 long-running plus 2 one-shot                            |

Container memory caps are declared in compose and total roughly 16.5 GB against 16 GB of RAM,
which is deliberate over-subscription on the assumption that SQL Server's 7 GB cap is the only
one that will actually be approached.

---

## 2. Databases: measured sizes

Two databases exist. The launch target is twelve (one host plus eleven offices), so **ten office
databases have never been created**.

| Database                                  | Data    | Transaction log |
| ----------------------------------------- | ------- | --------------- |
| `CaseEvaluation` (host)                   | 72.0 MB | **1,480.0 MB**  |
| `CaseEvaluation_falkinstein` (one office) | 72.0 MB | 200.0 MB        |

**The host transaction log is twenty times its data size.** That is not a normal steady state. It
indicates a recovery model and log-maintenance question that has never been addressed --
plausibly FULL recovery with no log backups, which grows without bound until the disk fills. On a
48 GB disk with 9.7 GB free, that matters. Whoever designs the production database configuration
should treat recovery model, log sizing and log backup as an explicit decision rather than a
default.

---

## 3. Row counts: where the data actually is

### One office database (`CaseEvaluation_falkinstein`), top tables by row count

| Table                        | Rows  | What it is                           |
| ---------------------------- | ----- | ------------------------------------ |
| `AbpEntityPropertyChanges`   | 2,689 | Audit: per-field before/after values |
| `AbpAuditLogActions`         | 1,820 | Audit: per-action records            |
| `AbpAuditLogs`               | 1,449 | Audit: per-request records           |
| `AbpPermissionGrants`        | 487   | Permission grants                    |
| `AppDoctorAvailabilities`    | 354   | Generated appointment slots          |
| `AbpEntityChanges`           | 258   | Audit: per-entity change records     |
| `AppNotificationOutboxItems` | 244   | Email delivery ledger                |
| `AbpSecurityLogs`            | 163   | Login/security events                |
| `AbpSessions`                | 141   | Sessions                             |
| `AppAppNotifications`        | 90    | In-app notifications                 |
| `AppNotificationTemplates`   | 67    | Seeded templates                     |
| `AppStates`                  | 50    | Seeded US states reference data      |
| `AppAppointmentPackets`      | 45    | Rendered packets                     |
| `AppIntegrationOutboxItems`  | 28    | Case Tracker outbound ledger         |

### Business entities in the same database

| Entity                | Count  |
| --------------------- | ------ |
| Appointments          | **16** |
| Patients              | 14     |
| Users                 | 14     |
| Appointment documents | 13     |
| Change requests       | 3      |

### Host database

|                            | Count |
| -------------------------- | ----- |
| `SaasTenants` (offices)    | 1     |
| `AbpAuditLogs`             | 655   |
| `AbpEntityPropertyChanges` | 519   |
| `HangFire.Job`             | 544   |

---

## 4. The single most important observation for sizing

**Sixteen appointments have produced roughly 1,450 audit-log rows, 1,820 audit actions and 2,689
entity property changes in one office database.** Business rows are outnumbered by audit rows by
well over a hundred to one.

**Audit logging, not appointment volume, is what drives data growth in this system.**

Two caveats that must travel with that number, because it would be misleading otherwise:

1. **This is a development and testing environment.** Sixteen appointments have been created,
   edited, cancelled, rebooked and poked at by developers far more than a real appointment would
   be. The audit-per-appointment ratio here is an upper bound, not a production ratio.
2. **Retention has never been configured.** The project's own HIPAA inventory records "Audit log
   retention: not configured -- no explicit policy". Nothing prunes these tables. Whatever ratio
   holds in production, it accumulates indefinitely by default, and a six-year retention
   expectation applies to the audit trail specifically.

The design question this raises, which no infrastructure choice answers by itself: audit data has
different retention, access and durability requirements from operational data, and it currently
lives in the same tables, in the same databases, under the same backup policy.

---

## 5. Load: what is known and what is not

**No load test has ever been run. No performance measurement of any kind exists.**

There is no measured figure for concurrent users, requests per second, appointments per day,
document upload volume, or page latency. The bundle budgets in `angular.json` are the scaffold
defaults (2 MB initial warning, 2.5 MB error) and have never been checked against actual output.
No Core Web Vitals measurement has been taken.

What _is_ known about the steady-state load shape, from configuration rather than measurement:

- **Hangfire polls continuously.** `QueuePollInterval = TimeSpan.Zero` means a constant query
  floor against the host database even with zero users.
- **Three recurring jobs run every 15 minutes**, and each iterates every office database. At
  eleven offices that is 33 office-database connection cycles per hour from scheduled work alone,
  before any user traffic. At thirty-three offices it is 99.
- **Six more jobs run daily**, clustered between 03:00 and 09:15.

For an eleven-office launch serving a single medical-evaluation business, the user-facing load is
plausibly very small. The scheduled load is not zero and scales linearly with office count.

---

## 6. Configuration surface

35 environment variables drive the production stack. Grouped by what a hosting design must supply:

| Group               | Variables                                                                 | Note                                                            |
| ------------------- | ------------------------------------------------------------------------- | --------------------------------------------------------------- |
| Domain and TLS      | `BASE_DOMAIN`, `TLS_CERT_PATH`, `TLS_KEY_PATH`, `HTTP_PORT`, `HTTPS_PORT` | Certificate is two mounted files                                |
| Token signing       | `OPENIDDICT_PFX_PATH`, `AUTHSERVER_CERT_PASSPHRASE`                       | Must persist across deploys                                     |
| Database            | `MSSQL_SA_PASSWORD`                                                       | **One `sa` credential** shared by app, auth server and migrator |
| Encryption          | `STRING_ENCRYPTION_PASSPHRASE`                                            | Losing it makes encrypted values unreadable                     |
| Object storage      | `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, 2 bucket names                  | App authenticates as root                                       |
| Licensing           | `ABP_LICENSE_CODE`, `ABP_NUGET_API_KEY`                                   | **Required at image build time**, not just runtime              |
| Mail                | 7 `SMTP_*`                                                                | Must override the shipped placeholders                          |
| Partner integration | 4 `CASE_TRACKER_*`                                                        | Blank by default; fails closed                                  |
| Backup              | `BACKUP_DIR`, `BACKUP_RETENTION_DAYS`                                     | Defaults to a path on the data disk                             |
| Resource caps       | 5 memory limits                                                           | Tuned for 16 GB                                                 |
| Migration mode      | `DBMIGRATOR_ENVIRONMENT`                                                  | `Development` seeds test data. Must never be set in production  |

Connection strings are **assembled inline in the compose file** from `MSSQL_SA_PASSWORD`, and
carry `TrustServerCertificate=True`.

Secrets live in a single mode-600 file on the server. No secret manager, no rotation, no access
audit.

---

## 7. Operational procedures as they exist today

### Deployment

**Entirely manual. There is no CD.** The workflow named `deploy-dev.yml` builds, tests and opens
the next promotion pull request; it never touches a server.

```
backup -> git pull --ff-only -> docker compose build <changed services>
       -> docker compose up -d
       -> docker compose up -d --force-recreate reverse-proxy
```

Two traps that have each broken the deployment in practice:

1. **Every compose invocation needs `--env-file`.** There is no `.env` in the repository root, so
   without it every variable resolves to an empty string and `up -d` recreates the stack with no
   database password, no TLS paths and no base domain.
2. **The reverse proxy must be force-recreated after any backend rebuild**, or nginx serves stale
   cached upstream IPs and routing silently breaks.

### Backup

A shell script enumerates `CaseEvaluation` plus every `CaseEvaluation_*` from `sys.databases` and
runs native `BACKUP DATABASE` inside the container, pruning past a retention window.

State: it works, but `BACKUP_DIR` defaults to a path **on the same disk as the data**, no cron
schedule was confirmed installed, and **no restore has ever been performed**.

### Monitoring

None. No metrics export, no APM, no OpenTelemetry, no uptime checks, no alerting.
`AspNetCore.HealthChecks.UI` uses in-memory storage, so health history does not survive a restart.
Serilog writes console and file sinks; container logs rotate at 10 MB x 5 per service.

An open finding constrains log shipping: the project's HIPAA inventory records PII logging as
enabled by default, with full user claims including email, names and tokens written to log files.
Until that is fixed, any aggregation target inherits PHI.

---

## 8. What has never been measured or tested

Listed so nothing here is mistaken for a known quantity:

- Concurrent users, requests per second, appointments per day, document volume
- Page load, Core Web Vitals, bundle size against budget
- Database restore, at all
- Failover or recovery time, at all
- Behaviour above one office database under real use
- Cost of running anything, anywhere
- Whether tenant resolution actually rejects the four ABP `__tenant` override paths
- Hangfire's idle load against a shared database

---

## 9. Provenance

| Number                  | How obtained                                                               |
| ----------------------- | -------------------------------------------------------------------------- |
| Database sizes          | `sys.master_files`, live, 2026-08-28                                       |
| Row counts              | `sys.partitions` and direct `COUNT(*)`, live, 2026-08-28                   |
| Machine resources       | Server rollout record, verified 2026-07-14, **not re-verified 2026-08-28** |
| Job schedules           | Read from source constants                                                 |
| Configuration surface   | `env.prod.example` and `docker-compose.prod.yml`                           |
| Everything in section 8 | Absence confirmed by search; no measurement exists to cite                 |
