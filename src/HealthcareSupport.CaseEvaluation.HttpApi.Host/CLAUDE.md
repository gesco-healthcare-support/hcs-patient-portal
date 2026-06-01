# HttpApi.Host -- runnable ASP.NET Core host (port 44327)

Wires all ABP modules, runs Hangfire recurring jobs, and enforces
cross-cutting concerns (rate limiting, upload caps, exception-status
mapping, JWT issuer validation, CORS, data-protection).

---

## What lives here

| File | Purpose |
|---|---|
| `CaseEvaluationHttpApiHostModule.cs` | Root ABP module -- all ConfigureServices + OnApplicationInitialization |
| `Program.cs` | Host bootstrap; loads `appsettings.Local.json` + `appsettings.secrets.json` |
| `appsettings.json` | Baseline config (LocalDB, Redis disabled, AuthServer 44368) |
| `appsettings.secrets.json` | ABP license + SMTP creds (sensitive; treat as secret even if not gitignored) |
| `BackgroundJobs/AnonymousHangfireDashboardAuthorizationFilter.cs` | Allow-all filter for `/hangfire`; hardening deferred post-MVP |
| `RateLimiting/PasswordResetEmailPeekMiddleware.cs` | Peeks JSON body `email` field for password-reset rate partitioning |
| `HealthChecks/` | Health-check registrations wired via `AddCaseEvaluationHealthChecks()` |

---

## Conventions

### Hangfire recurring jobs

9 jobs registered in `ConfigureHangfireRecurringJobs()` via
`RecurringJob.AddOrUpdate`. All run in `America/Los_Angeles` (with
`Pacific Standard Time` Windows fallback). Cron chain (PT, daily):

| Job class (Domain layer) | Cron | Time (PT) |
|---|---|---|
| `JointDeclarationAutoCancelJob` | `0 6 * * *` | 06:00 |
| `AppointmentDayReminderJob` | `0 7 * * *` | 07:00 |
| `CancellationRescheduleReminderJob` | `0 8 * * *` | 08:00 |
| `RequestSchedulingReminderJob` | `0 8 * * *` | 08:00 |
| `DueDateApproachingJob` | `15 8 * * *` | 08:15 |
| `PackageDocumentReminderJob` | `30 8 * * *` | 08:30 |
| `DueDateDocumentIncompleteJob` | `45 8 * * *` | 08:45 |
| `PendingDailyDigestJob` | `0 9 * * *` | 09:00 |
| `InternalStaffQueueDigestJob` | `15 9 * * *` | 09:15 |

Job CLASSES live in `Domain/*/Notifications/Jobs/`. Only job
REGISTRATION lives here. Every job iterates tenants by disabling the
`IMultiTenant` filter then switching `ICurrentTenant` per tenant --
omitting the `ICurrentTenant.Change` loop processes all tenants as
host-scope (no data returned, silent failure).

### Rate limiter

Three anonymous endpoint families are rate-limited
(see `ConfigurePasswordResetRateLimiter`):

| Path prefix | Partition strategy | Limit |
|---|---|---|
| `/api/public/external-account` (password reset) | per-email primary (5/hr) chained with per-IP secondary (50/hr) | both must pass |
| `/api/public/appointment-documents` POST upload-by-code | per-verification-code (5/hr) | single layer |
| `/api/public/external-signup/register` POST | per-IP (5/hr) | single layer |

`PasswordResetEmailPeekMiddleware` runs before `UseRateLimiter()` and
stashes the `email` field from the JSON body into
`HttpContext.Items["pwd-reset.email"]`. The partitioner reads that key
first; fallback chain is `?email=` query -> JWT `sub` -> client IP.

New anonymous endpoints that need abuse protection require a new
partition in `ConfigurePasswordResetRateLimiter` -- the global limiter
returns `GetNoLimiter` for unmatched paths.

### Exception-to-HTTP-status mapping

Every new domain error code that is a client-input validation failure
needs an explicit mapping in `Configure<AbpExceptionHttpStatusCodeOptions>`
(or `CaseEvaluationExceptionStatusCodeMappings.MapSharedRegistrationAndInternalUserCodes`
for codes shared with AuthServer). Without it ABP maps all
`BusinessException` types to 403, which the SPA interprets as a
permissions failure and does not show an error message to the user.

- Input/precondition violations -> `HttpStatusCode.BadRequest` (400)
- File-too-large -> `HttpStatusCode.RequestEntityTooLarge` (413) -- so
  the SPA can branch the user-facing message without parsing the body
- Shared registration/internal-user codes -> extracted to
  `CaseEvaluationExceptionStatusCodeMappings` so AuthServer also maps them

### Upload caps (two-tier)

Framework cap: 12 MB (`KestrelServerOptions.Limits.MaxRequestBodySize`
and `FormOptions.MultipartBodyLengthLimit`). App-layer cap: 10 MB
(enforced in `AppointmentDocumentsAppService`). The 2 MB headroom
prevents multipart boundary overhead from tripping the raw 413 before
the localized `BusinessException` can fire.

IMPORTANT: do not collapse the two caps to a single value -- the gap is
intentional. The framework 413 has no localized message; the app-layer
413 carries `data.MaxBytes` + `data.ActualBytes` so the SPA can render
"max 10 MB" with the actual size.

### JWT issuer validator (ADR-006/ADR-007)

The custom `IssuerValidator` callback accepts any issuer whose host is
exactly the authority host OR a single-label subdomain of it
(e.g. `falkinstein.localhost`). This lets each tenant's token
(`iss: http://falkinstein.localhost:44368/`) validate against the shared
OIDC key ring. Multi-label subdomains and unrelated hosts are rejected.
Do not replace this with a simple `ValidIssuer` string -- that would
break tenant token acceptance.

### VirtualFileSystem guard

`ReplaceEmbeddedByPhysical` calls in `ConfigureVirtualFileSystem` are
each wrapped in `Directory.Exists(...)`. Do not remove the guard:
Docker containers do not mount the host source tree, so the path does
not exist there. Removing the guard replaces embedded locale JSON with
an empty directory, making every `L("Key")` call return the literal key.

### Middleware order

```
Localization -> Routing -> StaticAssets -> StudioLink -> SecurityHeaders
-> Cors -> Authentication -> MultiTenancy -> UnitOfWork -> DynamicClaims
-> Authorization -> PasswordResetEmailPeekMiddleware -> RateLimiter
-> Swagger -> HangfireDashboard -> Auditing -> SerilogEnrichers -> Endpoints
```

`PasswordResetEmailPeekMiddleware` must come before `UseRateLimiter` so
the body-peek stash is ready when the partitioner executes.
`UseRateLimiter` comes after `UseAuthorization` so the JWT `sub` claim
is available as a fallback partition key.

---

## Gotchas

- `SendAppointmentEmailJob` (Domain) swallows SMTP exceptions and logs a
  warning, letting Hangfire mark the job Succeeded. Emails fail silently
  until real ACS credentials replace the `REPLACE_ME` values in
  `appsettings.secrets.json`. When real creds land, remove the try/catch
  so Hangfire's default retry policy engages.

- `/hangfire` dashboard uses `AnonymousHangfireDashboardAuthorizationFilter`
  (allow-all). It is intentionally unauthenticated in Wave 0 dev mode;
  production hardening is a deferred task. Do not add real job triggers
  that have destructive side effects while the dashboard is open.

- Data-protection keys are persisted to Redis when `Redis:Configuration`
  is set. Without this, AuthServer and HttpApi.Host (separate Docker
  containers, separate filesystems) use different key rings and
  `InvalidToken` errors appear on email confirmation + password reset.
  In local dev with `Redis:IsEnabled=false` and a single process, the
  in-memory default is fine.

- `PasswordResetEmailPeekMiddleware` only peeks bodies where
  `Content-Length` is known, positive, and <= 4 KB. Malformed JSON and
  missing `email` field silently no-op; the partitioner falls through to
  client IP. This means a request with an oversized or missing body is
  still rate-limited, just per-IP instead of per-account.

---

## Related

- docs/decisions/006-subdomain-tenant-routing.md -- subdomain tenant routing rationale
- docs/decisions/007-host-aware-tenant-resolver.md -- HostAwareDomainTenantResolveContributor
- docs/api/MIDDLEWARE-AND-PIPELINE.md -- full middleware pipeline reference
