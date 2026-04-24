# Background jobs infrastructure

## Source gap IDs

- CC-03 (track 06 -- cross-cutting backend): background jobs wired but unused, no recurring-job runtime, no Hangfire / Quartz / IDynamicBackgroundWorkerManager integration. See [../../gap-analysis/06-cross-cutting-backend.md](../../gap-analysis/06-cross-cutting-backend.md) lines 113-116.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:16` imports `Volo.Abp.BackgroundJobs`; line 33 lists `AbpBackgroundJobsDomainModule` in `[DependsOn(...)]`. The module IS wired at the domain layer.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.csproj:17` references `Volo.Abp.BackgroundJobs.EntityFrameworkCore` v10.0.2; `src/HealthcareSupport.CaseEvaluation.Domain/HealthcareSupport.CaseEvaluation.Domain.csproj:18` references `Volo.Abp.BackgroundJobs.Domain` v10.0.2; `src/HealthcareSupport.CaseEvaluation.Domain.Shared/HealthcareSupport.CaseEvaluation.Domain.Shared.csproj:11` references `Volo.Abp.BackgroundJobs.Domain.Shared` v10.0.2. EF layer is wired too.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260131164316_Initial.cs:63-83` creates the `AbpBackgroundJobs` table (`Id`, `ApplicationName`, `JobName`, `JobArgs`, `TryCount`, `CreationTime`, `NextTryTime`, `LastTryTime`, `IsAbandoned`, `Priority`, `ExtraProperties`, `ConcurrencyStamp`). Lines 1147-1148 add index `IX_AbpBackgroundJobs_IsAbandoned_NextTryTime`. Schema is in place.
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:198-201` calls `Configure<AbpBackgroundJobOptions>(options => { options.IsJobExecutionEnabled = false; });`. AuthServer will persist new jobs but never drain the queue, matching the standard ABP split-host guidance.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/` does NOT override `AbpBackgroundJobOptions.IsJobExecutionEnabled`, so the default (true) applies. HttpApi.Host is the implicit worker host today.
- Source grep for `IAsyncBackgroundJob|BackgroundWorkerBase|Hangfire|Quartz` across `src/**/*.cs` returns zero matches -- all 47 hits in the repo-wide grep are `packages.lock.json` entries, migration Designer snapshots (which include the `AbpBackgroundJobs` table in the model snapshot), or csproj package references. No business-code consumer exists.
- No `Volo.Abp.BackgroundJobs.Hangfire`, `Volo.Abp.BackgroundJobs.Quartz`, or raw `Hangfire.*` packages are referenced in any csproj across `src/` or `test/`.
- Test project `test/HealthcareSupport.CaseEvaluation.TestBase/HealthcareSupport.CaseEvaluation.TestBase.csproj:31` pulls `Volo.Abp.BackgroundJobs.Abstractions` v10.0.2 (standard ABP test-host transitive).
- Redis is already wired in `HttpApi.Host` for distributed cache + Medallion distributed locks (per track 06 Caching section at `06-cross-cutting-backend.md:118-122`), so a Redis-backed Hangfire option is available but not required.
- DbMigrator project exists at `src/HealthcareSupport.CaseEvaluation.DbMigrator/` and is the canonical place to run migrations + seeding; it is a console host, not a long-running service, and therefore must also suppress job execution (parity with AuthServer).

## Live probes

- `GET https://localhost:44327/hangfire` -- expected HTTP 404 (proves no Hangfire dashboard is wired and no middleware mounted under `/hangfire`).
- `GET https://localhost:44327/api/abp/application-configuration` with admin bearer -- expected HTTP 200, confirms the HttpApi.Host process is running ABP modules and the probe session is valid.
- Swagger path scan: read `https://localhost:44327/swagger/v1/swagger.json` and grep `"/api/"` keys for `background|job|hangfire|quartz` -- expected empty set (proves no AppService exposes a background-job endpoint, consistent with the static evidence that zero consumers exist).

Probe log: [../probes/background-jobs-infrastructure-<ISO>.md](../probes/background-jobs-infrastructure-<ISO>.md). The agent executing the probe replaces `<ISO>` with `date -Iseconds` before writing.

## OLD-version reference

- OLD uses a cron-driven HTTP trigger rather than an in-process scheduler. `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Scheduler\SchedulerController.cs` hosts `POST /api/scheduler/postscheduler`. Per track 10 Part 3 (`10-deep-dive-findings.md:166-172`) the endpoint is anonymous and `POST {}` returns `Hello socal` HTTP 200 -- anyone on the network can fire the whole job chain.
- `P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs` calls 9 stored procedures (one per job) in sequence when the endpoint fires. No in-process Hangfire/Quartz.
- Track 10 erratum 2 (`10-deep-dive-findings.md:18-29`): of the 9 jobs, #7 has its email-send commented out; #8 has both SMS and email commented out. Only 7 of 9 are wired end-to-end even in OLD.
- Track 10 erratum 3 (`10-deep-dive-findings.md:31-37`): every stored-proc call uses hardcoded `@AppointmentId = 1` and `@UserId = 1` literals (SchedulerDomain.cs lines 78-79, 122, 155, 179, 207, 237, 264, 338). This is almost certainly a bug in OLD (commented line 78 shows intent was `UserClaim.UserId`). The NEW port MUST re-spec from the stored-proc bodies, not from the caller's fixed arguments.
- OLD has no built-in recurring-job runtime; an external cron (Windows Task Scheduler or a unixland cron) drives the HTTP POST. This is the attack surface flagged by track 10 Part 3.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2 on .NET 10 with OpenIddict and LeptonX. Any package must have a version-matched v10.0.x release or be framework-agnostic.
- Row-level `IMultiTenant` per [ADR-004](../../decisions/004-doctor-per-tenant-model.md). Doctor IS the tenant. ABP does NOT auto-resolve a tenant inside a background-job body (per track 10 Part 4 research note `10-deep-dive-findings.md:195`). Job args must persist the `TenantId` explicitly and the job body must wrap work in `using (_currentTenant.Change(tenantId)) { ... }` or tenant-scoped queries will return either zero rows (for tenant-filtered entities) or cross-tenant rows (host context), either of which is an HIPAA failure.
- HIPAA: job args MUST NOT contain PHI. Patient names, DOB, chart IDs, appointment notes, claim numbers are all forbidden. Only persist GUIDs of `AppointmentId`, `PatientId`, `TenantId` and let the job rehydrate from the DB at execution time. If the ABP built-in `AbpBackgroundJobs.JobArgs` column is used (`nvarchar(max)`), serialization MUST still exclude PHI.
- [ADR-002](../../decisions/002-manual-controllers-not-auto.md) "manual controllers" applies only to AppService-exposed HTTP endpoints. Background-job classes are domain/application-layer workers and do not require a controller.
- AuthServer correctly disables job execution (`CaseEvaluationAuthServerModule.cs:200`). Preserve that. DbMigrator must add the same disable (it is not yet present).
- Existing Redis infra in HttpApi.Host (distributed cache + Medallion locks) can be reused for Hangfire if we elect Redis storage, but SQL Server storage is simpler and already available.
- The NEW port must NOT recreate OLD's anonymous `/api/scheduler/postscheduler` endpoint. That is explicitly flagged by track 10 Part 3 as an exploitable defect.
- ASCII only, no PHI in any code comment or log line. Per `.claude/rules/code-standards.md` and `.claude/rules/hipaa.md`.

## Research sources consulted (accessed 2026-04-24)

- ABP Background Jobs core documentation: `https://abp.io/docs/latest/framework/infrastructure/background-jobs` -- covers `IBackgroundJobManager.EnqueueAsync`, `BackgroundJob<TArgs>`, `IsJobExecutionEnabled`.
- ABP Background Workers documentation: `https://abp.io/docs/latest/framework/infrastructure/background-workers` -- covers `IBackgroundWorker`, `AsyncPeriodicBackgroundWorkerBase`, and the provider-agnostic wiring.
- ABP Background Workers Hangfire integration: `https://abp.io/docs/latest/framework/infrastructure/background-workers/hangfire` -- covers `HangfireBackgroundWorkerBase`, `RecurringJobId`, `CronExpression`, and `UseAbpHangfireDashboard`.
- ABP `IDynamicBackgroundWorkerManager`: `https://abp.io/docs/latest/framework/infrastructure/background-workers/dynamic` -- runtime add/update/remove of recurring workers across provider registries (Default, Hangfire, Quartz). Useful for per-tenant schedules.
- Hangfire SQL Server storage docs: `https://docs.hangfire.io/en/latest/configuration/using-sql-server.html` -- schema tables (`HangFire.Job`, `HangFire.State`, `HangFire.Schema`), migration semantics, `PrepareSchemaIfNecessary`.
- NuGet package `Volo.Abp.BackgroundJobs.Hangfire` v10.0.2: `https://www.nuget.org/packages/Volo.Abp.BackgroundJobs.Hangfire/10.0.2` -- confirms the ABP v10 wrapper exists and targets .NET 10.
- NuGet package `Hangfire.AspNetCore` 1.8.x: `https://www.nuget.org/packages/Hangfire.AspNetCore/` -- the underlying ASP.NET Core integration (dashboard, dependency injection).
- ABP sample blog post "Implementing Recurring Background Jobs with Hangfire": `https://community.abp.io/posts/implementing-recurring-background-jobs-in-abp-with-hangfire-integration` -- step-by-step for modules + dashboard auth filter.

## Alternatives considered

A. **Keep ABP built-in one-shot `IBackgroundJobManager` plus a homegrown `IHostedService` + `System.Threading.Timer` for recurrence.** Rejected. ABP's built-in `IBackgroundJobManager` is explicitly one-shot (per ABP docs and per track 10 Part 4 `10-deep-dive-findings.md:192`); any recurring scheduler would reinvent registry persistence, dedupe, retry backoff, and a dashboard that Hangfire already ships. Violates "integrate don't duplicate".

B. **Volo.Abp.BackgroundJobs.Hangfire 10.0.2 with SQL Server storage.** CHOSEN. Version-matched to ABP 10.0.2 per live NuGet check (2026-04-24). Co-locates the Hangfire schema with the existing SQL Server LocalDB that the NEW codebase already uses. Ships `HangfireBackgroundWorkerBase` for recurring jobs, `UseAbpHangfireDashboard` for ops, and integrates with ABP UoW + ABP logging + ABP DI out of the box.

C. **Volo.Abp.BackgroundWorkers.Quartz.** Rejected. Quartz has no built-in dashboard; the extra Misfire + cluster support is beyond MVP needs; the community Promethean / Silkier dashboards introduce another dependency surface. Heavier than needed for 7 to 9 recurring jobs.

D. **External cron + HTTP webhook (OLD parity).** Rejected. Recreates OLD's anonymous `POST /api/scheduler/postscheduler` attack surface flagged by track 10 Part 3 (`10-deep-dive-findings.md:166-172`). Any "require bearer token" patch is still a second authn plane disjoint from ABP's own.

E. **Redis-backed Hangfire storage (via `Hangfire.Pro.Redis` or the free `Hangfire.Redis.StackExchange`).** Conditional -- can be switched later without changing the job classes. Rejected for MVP to avoid adding a hard dependency on Redis (Redis is currently "IsEnabled = false" in dev per `06-cross-cutting-backend.md:119`). Keep the seam so production can swap storage when Redis is provisioned.

## Recommended solution for this MVP

Add `Volo.Abp.BackgroundJobs.Hangfire` v10.0.2 as a package reference in `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj` (and `AbpBackgroundJobsHangfireModule` as a DependsOn entry in `CaseEvaluationHttpApiHostModule`). Configure Hangfire with SQL Server storage pointed at the existing `Default` connection string; let Hangfire create its own schema tables on first run. Mount the Hangfire dashboard at `/hangfire` in the HttpApi.Host pipeline, but gate it with a `DashboardAuthorizationFilter` that requires an authenticated admin identity (`CaseEvaluationPermissions.Dashboard.Host` or equivalent) so the panel is never anonymous, unlike OLD. Do NOT register the module in AuthServer or DbMigrator; both should carry `Configure<AbpBackgroundJobOptions>(o => o.IsJobExecutionEnabled = false)` so only HttpApi.Host drains the queue. Per-job pattern: one `HangfireBackgroundWorkerBase` subclass per recurring job (lives under `src/HealthcareSupport.CaseEvaluation.Application/BackgroundWorkers/`), sets `RecurringJobId` and `CronExpression` in the constructor, and the body wraps tenant-scoped work in `using (_currentTenant.Change(tenantId))` for every tenant in `SaasTenant`. Per-tenant custom schedules can later be added via `IDynamicBackgroundWorkerManager.AddAsync(...)` from a settings-driven bootstrap. The 9 notification jobs themselves (scope of CC-03's sibling capability `scheduler-notifications`) plug into this infrastructure but are out of scope for this brief. Persist only GUID IDs in job args; rehydrate aggregates inside the job body.

## Why this solution beats the alternatives

- Matches ABP 10.0.2 out of the box (NuGet `Volo.Abp.BackgroundJobs.Hangfire` v10.0.2 verified 2026-04-24), so the wrapper is maintained in lockstep with the framework; alternatives B, C, E either require version pinning across repos or custom glue code.
- Ships a built-in ops dashboard behind admin auth; alternative C (Quartz) does not and alternative A would force a custom admin UI.
- Reuses the existing SQL Server LocalDB / SQL Server deployment -- no new infra dependency; alternative E requires Redis to be promoted from "IsEnabled = false" to primary.
- Avoids recreating OLD's anonymous HTTP scheduler trigger; alternative D would leak the `track 10 Part 3` defect forward into NEW.

## Effort (sanity-check vs inventory estimate)

The track 06 inventory rolls infrastructure + 9 jobs under CC-03 together and implies L. This brief scopes infrastructure only -- package add + module wiring + dashboard auth filter + tenant-change plumbing -- so **S to M (2 to 4 days)** is the correct scope. The 9 recurring jobs themselves live in the sibling capability [scheduler-notifications](./scheduler-notifications.md) and are L in aggregate.

## Dependencies

- Blocks: [scheduler-notifications](./scheduler-notifications.md) -- cannot schedule the 9 recurring jobs without a runtime for them.
- Blocks: [email-sender-consumer](./email-sender-consumer.md) if any email is ever sent via a recurring job (scheduler-notifications will be the first such consumer).
- Blocked by: none.
- Blocked by open question: verbatim Q18 from `docs/gap-analysis/README.md:251`: "Background jobs: Hangfire/Quartz add-on vs ABP's one-shot IAsyncBackgroundJob? (Tracks 2, 3, 6)". This brief recommends Hangfire; Adrian must confirm before implementation starts so the purchase/license decision is unambiguous (Hangfire OSS is LGPL; Hangfire Pro requires a paid license; the OSS build is sufficient for MVP).

## Risk and rollback

- Blast radius: the Hangfire schema adds tables under its own `HangFire.*` schema namespace; it does NOT modify `AbpBackgroundJobs`. If the module wiring is wrong, HttpApi.Host either fails at startup (easy to catch) or silently runs both the ABP default provider and the Hangfire provider (duplicate job executions). Mitigation: set `Configure<AbpBackgroundJobWorkerOptions>(o => o.DefaultTimeout = ...)` so the ABP default provider is disabled once Hangfire takes over, per the ABP Hangfire-integration docs.
- Rollback: remove the `Volo.Abp.BackgroundJobs.Hangfire` package + the `AbpBackgroundJobsHangfireModule` DependsOn entry; remove the dashboard mount. Hangfire's tables can stay (empty they cost nothing) or can be dropped with a single `DROP SCHEMA Hangfire` migration. The `AbpBackgroundJobs` table stays intact -- no risk to existing persisted jobs (of which there are none today, confirmed by the zero-consumer grep).

## Open sub-questions surfaced by research

- Which admin permission should gate `/hangfire`? Proposal: `CaseEvaluationPermissions.Dashboard.Host` or a new `CaseEvaluationPermissions.BackgroundJobs.Manage`. Decide during [scheduler-notifications](./scheduler-notifications.md) design.
- Should Hangfire storage be switched to Redis when the production Redis is provisioned, or is SQL Server storage permanent? Parking for post-MVP; the switch is a connection-string change, not a code change.
- Per-tenant schedule overrides via `IDynamicBackgroundWorkerManager` are NOT in MVP scope -- revisit once multi-tenant operations actually diverge.
