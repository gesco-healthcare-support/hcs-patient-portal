# Notifications -- DB-template email dispatch via Hangfire

Event-driven email pipeline: local events trigger handlers in Application/Notifications/Handlers/;
handlers call INotificationDispatcher; dispatcher renders once then fans out to Hangfire.
No inline SMTP. No HTTP endpoint. SMS leg is wired (BodySms populated) but delivery is
deferred until Twilio creds land (Phase 18 open item).

## What lives here

| Path | Purpose |
|---|---|
| `TemplateVariableSubstitutor.cs` | Pure `##Var##` placeholder substitution; no IO |
| `Jobs/PendingDailyDigestJob.cs` | 09:00 PT -- digest of Pending appointments to intake-staff inbox |
| `Jobs/InternalStaffQueueDigestJob.cs` | 09:15 PT -- per-staff queue counts (Staff Supervisor + Intake Staff only) |
| `Jobs/DueDateApproachingJob.cs` | 08:15 PT -- T-14/T-7/T-3 day due-date reminder |
| `Jobs/DueDateDocumentIncompleteJob.cs` | 08:45 PT -- T-7 + docs outstanding reminder |
| `Jobs/PackageDocumentReminderJob.cs` | 08:30 PT -- packet document upload reminder |
| `Jobs/JointDeclarationAutoCancelJob.cs` | 06:00 PT -- auto-cancel JDF-expired appointments |
| `../Appointments/Notifications/AppointmentRecipientResolver.cs` | Builds per-appointment recipient list |
| `../Appointments/Notifications/RecipientRoleResolver.cs` | Classifies an email vs. an expected role (registered or not) |
| `../Appointments/Handlers/SlotCascadeHandler.cs` | Log-only stub; subscribes to AppointmentStatusChangedEto |

Application layer counterparts (Application/Notifications/):
NotificationDispatcher, NotificationTemplateRenderer, CcRecipientAppender, EmailSubjectBuilder,
TenantUrlComposer, AccountUrlBuilder, and 18+ event handlers.

## Conventions

### Dispatcher fan-out

`NotificationDispatcher.DispatchAsync` renders the template ONCE regardless of recipient
count, then enqueues one `SendAppointmentEmailJob` Hangfire job per recipient. Template
render cost is O(1); enqueue cost is O(n-recipients). Zero recipients -> early return,
no render, no log noise.

`INotificationDispatcher` is an IN-PROCESS facade (ABP `ILocalEventBus` + `IBackgroundJobManager`).
It is NOT an HTTP endpoint. Do not add a controller for it.

### Template variable syntax

`TemplateVariableSubstitutor` replaces `##Key##` tokens. This mirrors OLD
`ApplicationUtility.GetEmailTemplateFromHTML` (kept for seed-body compatibility -- switching
to Razor requires Roslyn dynamic compilation per render and a rewrite of every seeded body).
DateTime/DateTimeOffset format: `MM/dd/yyyy` (matches OLD's explicit format string). Unknown
placeholders are left in place, not blanked.

### Recipient resolution

`AppointmentRecipientResolver.ResolveAsync` resolves recipients in this order (first-wins dedup by email):
1. ApplicantAttorney via join table
2. DefenseAttorney via join table
3. ClaimExaminer via AppointmentInjuryDetail
4. Four appointment-level email columns (PatientEmail / AA / DA / CE) -- classified via
   `IRecipientRoleResolver.ClassifyAsync` against the EXPECTED role, not a bare email existence
   check (bare check caused off-role dashboard routing bug B13)
5. Booker (IdentityUser) and Patient row
6. OfficeEmail setting (last, so a shared address keeps its party role)

`NotificationKind` is passed to `ResolveAsync` but the current resolver applies the same logic
for all kinds; future handlers may fork behavior per kind.

### Recurring job classes live in Domain; registration lives in HttpApi.Host

Job classes are `ITransientDependency` and live under Domain/Notifications/Jobs/ and
Domain/Appointments/Notifications/Jobs/. `RecurringJob.AddOrUpdate` calls live exclusively in
`CaseEvaluationHttpApiHostModule.cs` -- do NOT add Hangfire registration inside Domain.
All recurring jobs run Pacific Time (timezone injected via `TryGetPacificTimeZone()`).

### SlotCascadeHandler is a log-only stub

`SlotCascadeHandler` subscribes to `AppointmentStatusChangedEto` but performs no mutation.
The slot-status -> appointment-status mapping from the pre-2026-05-15 design was removed;
capacity is now the authoritative fullness probe. The subscription is kept so future
side-effects can be re-introduced without re-wiring DI.

### Missing-template fault tolerance

Handlers catch `BusinessException(NotificationTemplateNotFound)` and log a Warning rather
than propagating. A missing template must NOT roll back the appointment write -- email is
a side effect, the transaction already committed. If a template is missing, fix the seed;
do not add a silent fallback body.

### Tenant scope in jobs

Jobs that fan across tenants disable `IMultiTenant` filter to collect distinct `TenantId`
values, then re-enter each tenant via `_currentTenant.Change(tenantId)` before querying.
This is the correct pattern; do not add cross-tenant queries without the scope change.

## Gotchas

- `TenantId` must be captured and passed to `SendAppointmentEmailArgs.TenantId` before
  enqueuing. The Hangfire worker re-enters the tenant at execution time using that field;
  without it the `IMultiTenant` filter at host level excludes the packet row and the
  packet-attachment path silently skips ("is not Generated").
- `TemplateVariableSubstitutor` lives in Domain (not Application) so the AuthServer's
  `IAccountEmailer` override can use it without a cross-layer reference.
- `CcRecipientAppender` (Application layer) appends the per-tenant `SystemParameter.CcEmailIds`
  (semicolon-separated) list to a recipient collection. It is NOT called on the
  AppointmentRequested fan-out (Decision 2.1, 2026-05-08) -- only on the ApproveReject blast.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- docs/business-domain/USER-ROLES-AND-ACTORS.md
- src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CLAUDE.md
