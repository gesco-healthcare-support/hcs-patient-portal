# Notifications -- DB-template email dispatch via Hangfire

Event-driven email pipeline: local events trigger handlers in Application/Notifications/Handlers/;
handlers call INotificationDispatcher; dispatcher renders once then fans out to Hangfire.
No inline SMTP. No HTTP endpoint. SMS leg is wired (BodySms populated) but delivery is
deferred until Twilio creds land (Phase 18 open item).

## What lives here

| Path | Purpose |
|---|---|
| `TemplateVariableSubstitutor.cs` | Pure `##Var##` placeholder substitution; no IO |
| `AppNotification.cs` | In-app notification row for ONE internal staff user. Per-user fan-out, so read state is per-user. `IMultiTenant` |
| `AppNotificationManager.cs` | Raises those rows for a whole office's Staff Supervisors + Intake Staff. Runs inside the office's tenant scope |
| `ReminderCadence.cs` | Value object turning a comma-separated anchor list (`"14,7,3"`) into a firing predicate. No DI, no IO. Parsing is defensive because the list is admin-editable |
| `Outbox/` | Store-and-forward email: `NotificationOutboxItem`, `NotificationOutboxManager`, `OutboxDrainJob`, `OutboxDrainService`, `OutboxEmailSender` |
| `Jobs/PendingDailyDigestJob.cs` | 09:00 PT -- digest of Pending appointments to intake-staff inbox |
| `Jobs/InternalStaffQueueDigestJob.cs` | 09:15 PT -- per-staff queue counts (Staff Supervisor + Intake Staff only) |
| `Jobs/AppointmentReminderJob.cs` | 08:15 PT -- the CONSOLIDATED due-date reminder. Anchors come from `RemindersPolicy.DueDateApproachingAnchors`; due-date nudge and outstanding documents go out as ONE email |
| `Jobs/JointDeclarationOverdueJob.cs` | 06:00 PT -- FLAGS joint-declaration-overdue appointments. It does NOT cancel |
| `Jobs/ApprovalReconciliationJob.cs` | Approval reconciliation |
| `Jobs/PacketReconciliation.cs` | Packet reconciliation |

### Four job names in this table were wrong until 2026-09-28. Two renames and two retirements

Corrected against the code rather than patched to satisfy a checker, because the old names are
still findable in Hangfire and in comments, and knowing WHICH of the two happened changes what you
do next.

Historical names below are written WITHOUT backticks on purpose. In this file a backticked path
means "this exists now", so a name that no longer resolves must not wear them - that is what lets
the CLAUDE.md drift checker in CI treat every backticked path as a claim it can verify.

| Name the docs used (removed) | What actually happened |
| --- | --- |
| Jobs/DueDateApproachingJob.cs | **Renamed** to `AppointmentReminderJob.cs`, which also absorbed the outstanding-documents email |
| Jobs/JointDeclarationAutoCancelJob.cs | **Renamed** to `JointDeclarationOverdueJob.cs` on 2026-08-08, and it **stopped cancelling** |
| Jobs/DueDateDocumentIncompleteJob.cs | **Retired.** Folded into `AppointmentReminderJob.cs` |
| Jobs/PackageDocumentReminderJob.cs | **Retired** |

**The trap, and it is deliberate.** Both renamed classes **kept their old `RecurringJobId`
strings** -- `"appt-duedate-approaching"` and `"appt-jdf-auto-cancel"`. Hangfire keys
registrations by that string, so changing it without deleting the old registration leaves **two
jobs running against the same appointments** (`JointDeclarationOverdueJob.cs:41-48`). The two
retired jobs' entries are purged by `RecurringJob.RemoveIfExists` in the host module; leave those
calls in place.

So `"appt-jdf-auto-cancel"` names a job that **no longer cancels anything**. It sets
`Appointment.JointDeclarationOverdueAt` and raises `AppointmentJointDeclarationOverdueEto`; the
status is unchanged. Two consequences stated at `JointDeclarationOverdueJob.cs:28-33`: nothing
reaches the Case Tracker from this path any more, because no status changes; and these
appointments accumulate as Approved-but-overdue, which is the point, since the flag makes visible
a backlog that used to be silently absorbed.

**Do not infer a job's behaviour from its `RecurringJobId`.**
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
