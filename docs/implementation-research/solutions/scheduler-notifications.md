# Scheduler + recurring notification-dispatch jobs

## Source gap IDs

- G2-11 (track 02 -- 9 recurring email / SMS jobs driven by
  `SchedulerDomain.cs` in OLD). See
  `../gap-analysis/02-domain-entities-services.md`.
- 03-G09 (track 03 -- application-service layer for the 9 jobs + the
  external POST trigger endpoint). See
  `../gap-analysis/03-application-services-dtos.md`.
- G-API-15 (track 04 -- REST endpoint surface for scheduler triggers). See
  `../gap-analysis/04-rest-api-endpoints.md`.

The three gap IDs describe the same capability at three layers
(domain / app service / API) and resolve to one implementation unit.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:33`
  depends on `AbpBackgroundJobsDomainModule`. The module is wired; no consumer
  exists.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/` migration
  `20260131164316_Initial` provisions `AbpBackgroundJobs` table per ABP
  convention. Rows accumulate only when someone calls
  `IBackgroundJobManager.EnqueueAsync`. Currently empty.
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:198-201`
  sets `AbpBackgroundJobsOptions.IsJobExecutionEnabled = false` so the
  AuthServer process does not pull the job queue. `HttpApi.Host` does not
  override -- the default is `true`, so the API process will be the worker host.
- Zero files under `src/` match the grep
  `IAsyncBackgroundJob|HangfireBackgroundWorkerBase|BackgroundWorkerBase|IDynamicBackgroundWorkerManager|RecurringJob`.
  Confirms no recurring-job consumer exists.
- Zero files under `src/HealthcareSupport.CaseEvaluation.HttpApi/` match
  `scheduler | notification | reminder` in a `Route` attribute (case-insensitive).
  Confirms no external HTTP trigger endpoint exists.
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:61`
  replaces `IEmailSender` with `NullEmailSender` in `#if DEBUG`. No business
  code currently calls `IEmailSender`. The scheduler capability depends on
  email-sender-consumer landing first.
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs`
  depends on `TextTemplateManagementDomainModule`, which is required for
  `ITextTemplateRenderer`. Templates themselves (definition provider +
  contents) are not yet declared.
- No `Volo.Abp.BackgroundJobs.Hangfire`, `Volo.Abp.BlobStoring.Aws`, or
  `Volo.Abp.Sms.*` package is referenced in any `.csproj` under `src/`.
- The 7-entity multi-tenant boundary is fixed: Appointment, Doctor,
  DoctorAvailability, ApplicantAttorney, AppointmentAccessor,
  AppointmentApplicantAttorney, AppointmentEmployerDetail all implement
  `IMultiTenant`. Every job body that enumerates appointments must either run
  under an explicit tenant or disable the multi-tenant filter on purpose --
  see `Doctors/DoctorsAppService.cs` for the pattern.

## Live probes

- `curl -sk https://localhost:44327/swagger/v1/swagger.json` ->
  HTTP 200; JSON parse counts 317 path keys; `scheduler | notification |
  reminder | background | job` substring scan returns 0 matches. Timestamp
  2026-04-24T13:15 local. Proves zero scheduler-related REST surface in the
  current build. Full log at
  `../probes/scheduler-notifications-2026-04-24T1315.md`.
- `curl -sk https://localhost:44327/hangfire` -> HTTP 404.
- `curl -sk https://localhost:44368/hangfire` -> HTTP 404. Proves Hangfire
  dashboard is not mounted on either process.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.Models/Enums/ReminderTypes.cs:9-20`
  defines the 9 enum values driving the switch in SchedulerDomain.
- `P:/PatientPortalOld/PatientAppointment.Domain/Core/SchedulerDomain.cs:37-70`
  dispatches one of 9 methods based on `SchedulerParameters.ScheduleTypeId`.
  Method names verified against the actual source (the legend in a few
  earlier track notes is slightly paraphrased; use these):
  1. `AppointmentApproveRejectInternalUserNotification` (lines 87-117):
     stored proc `spm.spAppointmentApproveRejectInternalUserNotification`;
     dispatches email + SMS; template `AppointmentApproveRejectInternal`.
  2. `AppointmentPackageDocumentPendingNotification` (lines 119-150):
     stored proc `spm.spAppointmentPackageDocumentPendingNotification`;
     email + SMS; template `UploadPendingDocuments`.
  3. `AppointmentDueDateApproachingNotification` (lines 152-174):
     stored proc `spm.spAppointmentDueDateApproachingNotification`;
     email only; template `AppointmentDueDateReminder`.
  4. `AppointmentDueDateDocumentApproachingNotification` (lines 176-202):
     stored proc `spm.spAppointmentDueDateDocumentApproachingNotification`;
     email + SMS; template `AppointmentDocumentIncomplete`.
  5. `AppointmentJointDeclarationDocumentUploadNotification` (lines 204-232):
     stored proc `spm.spJointDeclarationDocumentUploadNotification`;
     email + SMS; template `UploadPendingDocuments`.
  6. `AppointmentAutoCancelledNotification` (lines 234-260):
     stored proc `spm.spAppointmentAutoCancelledNotification`;
     email + SMS; template `AppointmentCancelledDueDate`.
  7. `AppointmentPendingReminderStaffUsersNotification` (lines 261-312):
     stored proc `spm.spAppointmentPendingReminderStaffUsersNotification`;
     email-only -- **SendSMTPMail call is commented out at line 309**.
     Per track-10 erratum 2: this job is effectively disabled in OLD.
  8. `AppointmentPendingDocumentSendToResponsibleUser` (lines 336-364):
     stored proc `spm.spAppointmentPendingDocumentSendToResponsibleUser`;
     **both SMS and email commented out (lines 352, 361)**. Per track-10
     erratum 2: fully disabled in OLD.
  9. `AppointmentPendingDailyNotification` (lines 72-85):
     stored proc `spm.spPendingAppointmentNotification`;
     email only; template `PendingAppointmentDailyNotification`.
- Track-10 erratum 2 (`../gap-analysis/10-deep-dive-findings.md` lines 18-37):
  jobs 7 and 8 are effectively disabled in OLD; SMS across the stack is 100%
  disabled via `isSMSEnable: false` in `server-settings.json`. Port jobs 1,
  2, 3, 4, 5, 6, 9 as the active set; drop 7 and 8 unless Adrian says
  otherwise.
- Track-10 erratum 3: every stored-proc call in SchedulerDomain passes
  hardcoded literal `1` for `@AppointmentId` and in most jobs also for
  `@UserId` (the commented line at
  `P:/PatientPortalOld/PatientAppointment.Domain/Core/SchedulerDomain.cs:79`
  shows the original intent was `UserClaim.UserId`). This is a pre-existing
  bug in OLD. The NEW port should specify each job from what the proc
  **would** do with real IDs -- i.e., from the proc body (or, since the
  procs are LocalDB stubs in the bring-up, from the caller's intent as
  inferred from the email-template view models). Spec from intent; do not
  replicate the `=1` bug.
- OLD triggers each job through an anonymous external POST at
  `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/Core/SchedulerController.cs:41-48`
  (`POST /api/scheduler/postscheduler`). The `AuthenticationByPass()` list
  at `AllowedApis.cs:14-27` explicitly allows it without JWT. External cron
  is assumed. Track-10 Part 3 confirms this endpoint is live-probeable and
  returns `Hello socal` + HTTP 200 with an empty body. The NEW port should
  NOT replicate the anonymous surface (attack surface; denial-of-email
  vector) -- the scheduler replaces the trigger, so no public endpoint is
  required.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20 (Angular is not touched by this
  capability), OpenIddict on port 44368, HttpApi.Host on port 44327 in the
  dev launch profile.
- Row-level `IMultiTenant` on 7 core entities (ADR-004). A recurring job runs
  under no tenant; each job must either `using (_dataFilter.Disable<IMultiTenant>())
  { ... }` to enumerate distinct tenants, then per tenant wrap work in
  `using (_currentTenant.Change(tenantId)) { ... }`, OR accept a TenantId in
  job args and run a separate job per tenant. The enumerate-then-switch
  shape is canonical in ABP recurring jobs (see the community article in
  "Research sources").
- Riok.Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext
  (ADR-003), no `ng serve` (ADR-005). None of these conflict with the
  recommended solution.
- HIPAA: job args carry IDs only (AppointmentId, TenantId, recipient
  IdentityUserId). No names, no emails, no DOBs, no injury narratives in
  job args. Email bodies are rendered inside the job by
  `ITextTemplateRenderer` using IDs to look up data server-side. All
  rendered email bodies must use ABP text templates so PHI never leaks
  through a log channel. Follows the templates-email-sms capability
  contract.
- Depends on: background-jobs-infrastructure (CC-03, Hangfire wired);
  email-sender-consumer (CC-01, `IEmailSender` no longer `NullEmailSender`);
  templates-email-sms (D-12, the 18 template codes seeded with content
  bodies). Optionally sms-sender-consumer (CC-02) if Adrian confirms
  SMS-on for any tenant; per track-10 erratum 2, MVP assumption is
  SMS-off.
- Depends on appointment-accessor-auto-provisioning (G2-05) for the
  responsible-staff / accessor recipient list in jobs 1 and 4.
- Depends on appointment-documents (G2-08) for "pending document" job
  bodies (2, 4, 5); without the document entity, those jobs have no work
  to do, but the class + cron can still land with an empty query and a
  logger warning until G2-08 merges.
- No in-process cron; ABP native `IBackgroundJobManager` is one-shot only.
  Recurring work requires Hangfire (or Quartz) workers. Per track-10 Part 4:
  `HangfireBackgroundWorkerBase` + `IBackgroundWorkerManager` is the
  canonical ABP pattern.
- Q18 from `../gap-analysis/README.md:251-252` asks whether NEW should
  adopt Hangfire + `IDynamicBackgroundWorkerManager` or stay on
  `IAsyncBackgroundJob`; this brief assumes Hangfire (and recommends
  it), but the decision belongs to background-jobs-infrastructure
  (CC-03).
- Q7 asks whether templates live in OLD-style `Template` entity or ABP
  `TextTemplateManagement`. This brief assumes the latter (see
  templates-email-sms).
- Q2 (from track 04 line 185) asks which of the 9 jobs are still
  business-required. This brief defaults to the 7 active jobs (1, 2, 3, 4,
  5, 6, 9); Adrian decides at Phase 4 whether to drop more.

## Research sources consulted

All accessed 2026-04-24.

1. ABP Framework -- Background Jobs:
   https://abp.io/docs/latest/framework/infrastructure/background-jobs --
   `IBackgroundJobManager.EnqueueAsync` is one-shot; recurring requires an
   integration (Hangfire, Quartz) or a custom `BackgroundWorkerBase`
   scheduler.
2. ABP Framework -- Hangfire Background Job Manager:
   https://abp.io/docs/latest/framework/infrastructure/background-jobs/hangfire --
   `HangfireBackgroundWorkerBase` exposes `RecurringJobId` + `CronExpression`
   and is registered via `AbpBackgroundWorkerOptions`.
3. ABP Community -- Dynamic background jobs and workers in ABP:
   https://abp.io/community/articles/dynamic-background-jobs-and-workers-in-abp-wfdkdsq9 --
   `IDynamicBackgroundWorkerManager` adds recurring schedules at runtime
   across providers; useful if per-tenant cron differs.
4. ABP Framework -- Text Template Management:
   https://docs.abp.io/en/commercial/latest/modules/text-template-management --
   templates resolve via `ITextTemplateRenderer`; placeholder values bind
   via anonymous / typed models.
5. ABP Framework -- Multi-Tenancy:
   https://abp.io/docs/latest/framework/architecture/multi-tenancy --
   `using (_dataFilter.Disable<IMultiTenant>()) { ... }` and
   `using (_currentTenant.Change(tenantId)) { ... }` are the two primitives
   for cross-tenant work.
6. Hangfire -- Cron expressions reference:
   https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html --
   ABP's `CronExpression` is passed through; timezone defaults to UTC
   unless a `TimeZoneInfo` is provided.
7. ABP Framework -- IFeatureChecker:
   https://docs.abp.io/en/abp/latest/Features --
   per-tenant feature flags; lets tenants opt individual jobs on or off.
8. Microsoft Learn -- `TimeZoneInfo.FindSystemTimeZoneById`:
   https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid --
   used when OLD-style "Pacific Time" daily schedules must be honoured.

## Alternatives considered

- **A. ABP native `IBackgroundJobManager` only (one-shot) + cron-hit POST
  endpoint.** Rejected. Recreates OLD's anonymous `POST
  /api/scheduler/postscheduler` attack surface (denial-of-email vector).
  Also does not solve recurring scheduling -- still needs an external cron
  owner. Effort savings are illusory.
- **B. Hangfire via `HangfireBackgroundWorkerBase` + `IBackgroundWorkerManager`
  (recurring workers).** Chosen. Canonical ABP Commercial 10.x pattern.
  Persistent job storage via `AbpBackgroundJobs` table already provisioned.
  Dashboard at `/hangfire` can be secured behind an ABP permission for QA
  visibility. `CronExpression` lives in code, per-job.
- **C. Quartz.NET via `AbpBackgroundWorkerQuartzModule`.** Conditional /
  rejected for MVP. Quartz is richer (per-job schedules, misfire policies,
  calendars) but has less community coverage in the ABP ecosystem and is a
  larger dependency. Hangfire covers the requirements and is the default
  recommendation in ABP docs.
- **D. External cron (Windows Task Scheduler / systemd timer) hitting a new
  authenticated HTTP endpoint.** Rejected. Recreates OLD's operational
  coupling to a second scheduler system; forces ops to manage cron entries
  per deployment. Hangfire's scheduler is in-process, survives restarts via
  storage, and is observable.
- **E. Reuse `IDynamicBackgroundWorkerManager` (ABP 9+, add/update/remove
  recurring jobs at runtime across providers).** Conditional. Adds
  flexibility -- per-tenant cron schedules can be set via an admin UI.
  Strictly more than MVP needs; recommended only if Adrian wants tenant-level
  schedule overrides. For MVP the fixed per-job cron in option B is
  sufficient; `IDynamicBackgroundWorkerManager` is a future upgrade path,
  not a replacement.

## Recommended solution for this MVP

Implement seven `HangfireBackgroundWorkerBase` subclasses under
`src/HealthcareSupport.CaseEvaluation.Application/BackgroundJobs/Notifications/`,
one per active OLD job after erratum 2 is applied (drop jobs 7 and 8). Each
class sets a constant `RecurringJobId` (e.g.
`"appt-approve-reject-internal-user-daily"`), a constant `CronExpression`
(default `0 8 * * *` -- 08:00 daily; Adrian confirms per-job at review),
and optionally a `TimeZoneInfo` (default UTC per Hangfire convention; if
OLD deployments ran on America/Los_Angeles, set that explicitly per job).
Each class overrides `DoWorkAsync` with the following shape: inject
`IDataFilter`, `ICurrentTenant`, `IRepository<Appointment, Guid>`,
`IEmailSender`, `ITextTemplateRenderer`, `ILogger`. Inside DoWorkAsync,
wrap work in `using (_dataFilter.Disable<IMultiTenant>()) { ... }`;
enumerate distinct TenantIds from the Appointment repository; for each
tenant, wrap in `using (_currentTenant.Change(tenantId)) { ... }`, query
relevant appointments via LINQ (replacing each OLD `spm.sp*`), resolve
recipients (appointment-accessor-auto-provisioning makes this possible),
render the email body via `ITextTemplateRenderer.RenderAsync(templateName,
model, cultureName, globalContext)` against the template code matching
the OLD `EmailTemplate.*` constant, and call
`IEmailSender.SendAsync(email, subject, body, isBodyHtml: true)`. Feature-flag
each job via `IFeatureChecker` so clinics can opt out.

Register the workers in `CaseEvaluationApplicationModule.OnApplicationInitialization`:

````csharp
var manager = context.ServiceProvider
    .GetRequiredService<IBackgroundWorkerManager>();
await manager.AddAsync(
    context.ServiceProvider.GetRequiredService<AppointmentApproveRejectInternalUserDailyWorker>());
// repeat for the other 6 workers
