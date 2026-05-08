---
stage: 7
title: Recurring jobs and notifications -- 5 missing OLD jobs
date: 2026-05-04
status: research
scope: N1 -- bring NEW from 5 jobs to OLD's 9
related-parity-doc: docs/parity/scheduler-background-jobs.md
old-source-root: P:\PatientPortalOld
new-source-root: W:\patient-portal\replicate-old-app
---

# Stage 7 -- Recurring jobs and notifications

## 1. Cross-cutting context (read once, reused per job)

### 1.1 OLD scheduler architecture (for all 9 reminder types)

- **Switch / dispatch:** `P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs:37-70` -- `ConfigureNotificaion(SchedulerParameters schedulerParameters)` switches on `ScheduleTypeId` (an `int` matching `ReminderTypes`).
- **Trigger:** OS-level scheduler (Windows Task Scheduler) hits `POST /api/Scheduler/postscheduler` with `{ ScheduleTypeId = 1..9 }`. Postman collection rows confirm this at `P:\PatientPortalOld\Documents_and_Diagrams\Postman Collection\Patient Appointment API.postman_collection.json:29926-30031`. There is no in-app cron in OLD; cadence is external config.
- **Per-type pattern:** every reminder method calls a stored proc, deserializes the JSON `Result` column into a per-type view-model list, then issues SMS via `TwilioSmsService.SendSms` and email via `SendMail.SendSMTPMail`. Templates are pulled by `ApplicationUtility.GetEmailTemplateFromHTML(EmailTemplate.<X>, vEmailSenderViewModel, "")`.
- **No "last sent" log.** OLD does not record reminder dispatches anywhere. If the OS scheduler runs a job twice, the same recipients get a second email. NEW closes this gap.
- **`UserClaim.UserId`** is referenced inside every reminder (`SchedulerDomain.cs:94, 123, 156, 180, 208, 238, 265, 339`). The endpoint is unauthenticated in practice, so `UserClaim.UserId` is whatever ambient context the OS scheduler runs as -- effectively meaningless for the proc parameter. Treat as `// PARITY-FLAG` no-op when porting.
- **Reminder enum (verbatim) -- `P:\PatientPortalOld\PatientAppointment.Models\Enums\ReminderTypes.cs:9-20`:**

```csharp
public enum ReminderTypes
{
    AppointmentApproveRejectInternalUser = 1,
    AppointmentPackageDocumentPending = 2,
    AppointmentDueDateApproaching = 3,
    AppointmentDueDateDocumentApproaching = 4,
    AppointmentJointDeclarationDocumentUpload = 5,
    AppointmentAutoCancelled = 6,
    AppointmentPendingReminderStaffUsers = 7,
    AppointmentPendingDocumentSendToResponsibleUser = 8,
    PendingAppointmentDailyNotification = 9
}
```

### 1.2 OLD config knobs (`SystemParameter` row -- `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\SystemParameter.cs`)

| Field | Lines | Used by |
|---|---|---|
| `AppointmentCancelTime` | 18-20 | `AppointmentMaxTimeAME/PQME/OTHER` -- not a reminder field |
| `AppointmentDueDays` | 23-25 | Pending appointment overdue threshold |
| `AutoCancelCutoffTime` | 49-50 | `JdfAutoCancel` (already mapped to `JointDeclarationUploadCutoffDays` in NEW) |
| `JointDeclarationUploadCutoffDays` | 69-70 | JDF reminder (type 5) AND auto-cancel (type 6) |
| `PendingAppointmentOverDueNotificationDays` | 82-83 | Daily digest cadence + overdue staff reminders (types 7, 9) |
| `ReminderCutoffTime` | 87-88 | Package-doc reminder window (type 2) |
| `CcEmailIds` | 90-91 | All-parties CC list |

The cron / interval cadence itself is NOT stored in `SystemParameter` -- it lives in the OS scheduler config. NEW must invent cron values that match the OLD ops convention "once daily" plus run-order constraints.

### 1.3 OLD `clinicStaffEmail` (server-setting)

- **Source:** `P:\PatientPortalOld\PatientAppointment.Api\server-settings.json:55-57`:
  ```json
  // If you need to add multiple email address please separate using ';'
  "clinicStaffEmail": "pooja.fithani@radixweb.com",
  ```
- **Read by:** `SchedulerDomain.AppointmentPendingDailyNotification()` at `SchedulerDomain.cs:76` -- `var email = ServerSetting.Get<string>("clinicStaffEmail");`
- **NEW mapping:** existing `CaseEvaluationSettings.NotificationsPolicy.OfficeEmail` (`src\HealthcareSupport.CaseEvaluation.Domain\Settings\CaseEvaluationSettings.cs:80`). Reuse for the daily digest recipient. Fallback to `CcEmailAddresses` is NOT correct -- the digest goes to a single inbox, not the CC list.

### 1.4 NEW patterns to copy

- **Hangfire `RecurringJob.AddOrUpdate<T>`** in `src\HealthcareSupport.CaseEvaluation.HttpApi.Host\CaseEvaluationHttpApiHostModule.cs:585-627` (`ConfigureHangfireRecurringJobs`). All NEW jobs use Hangfire (NOT ABP `IBackgroundWorker`) so the dashboard at `/hangfire` shows them and ad-hoc retriggers are possible.
- **Time zone:** `TryGetPacificTimeZone()` at `CaseEvaluationHttpApiHostModule.cs:629-648` -- IANA `America/Los_Angeles` first, Windows `Pacific Standard Time` second, `TimeZoneInfo.Utc` last. All cron strings are interpreted in PT via `RecurringJobOptions { TimeZone = pacificTime }`.
- **Per-tenant pass:** every job iterates `_dataFilter.Disable<IMultiTenant>()` over `Appointment` to discover distinct `TenantId`s, then re-enters `_currentTenant.Change(tenantId)` for each. Verbatim pattern in `JointDeclarationAutoCancelJob.cs:207-220` and `PackageDocumentReminderJob.cs:163-173`.
- **Two dispatch shapes:**
  1. **Direct enqueue via `IBackgroundJobManager` + `SendAppointmentEmailArgs`** -- used by the three Appointments jobs (`AppointmentDayReminderJob`, `CancellationRescheduleReminderJob`, `RequestSchedulingReminderJob`) which build subject/body inline from `IAppointmentRecipientResolver`. Subject + body are hand-rolled HTML strings. No `NotificationDispatcher` involvement.
  2. **Event-bus + `INotificationDispatcher`** -- used by `JointDeclarationAutoCancelJob` and `PackageDocumentReminderJob`. The job publishes a typed `*Eto`; a sibling email handler in `Application/Notifications/Handlers/` calls the dispatcher with a templated `NotificationTemplateConsts.Codes.<X>`. This is the newer pattern (Phase 14/14b/18) and is preferred for any job whose body should be authored in seed data and rendered at dispatch time.
- **Decision for the 5 missing jobs:** prefer pattern 2 (templated dispatch) so that admins can edit copy via the notification-templates UI. Reasons: every OLD reminder used `EmailTemplate.<X>` HTML files; pattern 2 is the strict-parity equivalent. Pattern 1 was acceptable only for the W2-10 CCR jobs because their copy is law-anchored boilerplate that does not need to be edited.
- **Recipient resolution:** `IAppointmentRecipientResolver.ResolveAsync(appointmentId, NotificationKind kind)` at `src\HealthcareSupport.CaseEvaluation.Domain\Appointments\Notifications\IAppointmentRecipientResolver.cs:21-24`. Returns `List<SendAppointmentEmailArgs>` (one per addressable recipient, skipping null/empty emails). Reuse for jobs that need the standard "all parties" fan-out. Internal-staff jobs need a different resolver (see N1.1, N1.4).

### 1.5 NEW template codes (`src\HealthcareSupport.CaseEvaluation.Domain.Shared\NotificationTemplates\NotificationTemplateConsts.cs`)

The current set is 23 codes (lines 13-35). The 5 missing jobs map to these existing codes:

| Missing job | Template code in NEW | Status |
|---|---|---|
| ApproveRejectInternalUserReminder | (none -- needs new code `ApproveRejectInternalUserReminder`) | ADD |
| DueDateDocumentApproaching | `DueDateApproachingReminder` (line 28) | reuse but consider adding `DueDateDocumentReminder` for the doc-pending variant |
| JdfReminder | `JDFReminder` (line 26) | reuse |
| PendingDocumentSendToResponsibleUser | (none -- needs new code `PendingDocumentResponsibleUserReminder`) | ADD |
| PendingAppointmentDailyNotification | (none -- needs new code `PendingAppointmentDailyDigest`) | ADD |

Three new codes must be added to `NotificationTemplateConsts.Codes`, the seed contributor (`NotificationTemplateDataSeedContributor.cs`), and the `en.json` localization for subject/body. The 59-code OLD-parity merge mentioned in the brief is NOT yet applied to this file -- it currently shows 23 codes only. Treat the three new codes as additive Phase 19 entries; do not block on the wider merge.

### 1.6 NEW `Appointment` entity gap -- `PrimaryResponsibleUserId`

OLD `Appointment.PrimaryResponsibleUserId` (`P:\PatientPortalOld\PatientAppointment.DbEntities\Models\Appointment.cs:82-83`) is `Nullable<int>`. NEW does NOT have this column. References to "PrimaryResponsibleUserId" in NEW source live only in error codes, doc handlers, EF migrations, and the EF snapshot (per the Grep result), but the Domain `Appointment.cs` shape (per the Appointments CLAUDE.md file map) shows no such property. **Blocker for N1.4 (PendingDocumentSendToResponsibleUserJob)** -- the job has nothing to address. Two options:

1. **Strict parity:** add `PrimaryResponsibleUserId : Guid?` to `Appointment` (FK -> `IdentityUser`) via migration before implementing N1.4.
2. **Tactical defer:** route N1.4 to the same recipient set as N1.1 (clinic-staff role) until the field exists.

Recommendation: option 1, but file the migration in the same task as N1.4 so the audit doc has one feature slice.

---

## 2. Per-job research (5 missing jobs)

### N1.1  ApproveRejectInternalUserReminderJob (OLD type 1)

#### OLD source

- **Job runner:** `P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs:87-117` -- `AppointmentApproveRejectInternalUserNotification()`.
- **Stored proc:** `spm.spAppointmentApproveRejectInternalUserNotification @AppointmentStatusId, @UserId` (proc body NOT in repo -- lives in DB).
- **Template:** `EmailTemplate.AppointmentApproveRejectInternal` (`SchedulerDomain.cs:112`).

#### OLD query (predicate as documented; proc body unavailable)

OLD passes `AppointmentStatusId = 1` (Pending) hard-coded (`SchedulerDomain.cs:93`). The proc is named "ApproveRejectInternalUser" and returns a list with `PendingAppointmentCount`, `ApprovedAppointmentCount`, `EmailList`, `PhoneList`. From the call shape and usage:

> Predicate: appointments with `AppointmentStatusId = Pending` whose age is past `SystemParameter.AppointmentDueDays` (default OLD: see `SystemParameter.AppointmentDueDays`), grouped to internal-staff recipients (the proc returns one row per recipient with their pending+approved counts).

The proc name "ApproveReject" indicates it surfaces both Pending (action needed: approve OR reject) and recently-approved counts so staff see a workload summary, not per-appointment rows.

#### OLD cadence

- **Configurable** via `SystemParameter.AppointmentDueDays` (P:`PatientPortalOld\PatientAppointment.DbEntities\Models\SystemParameter.cs:23-25`).
- **Trigger interval:** OS scheduler -- typical OLD ops convention is daily, but the gating is per-row `AppointmentDueDays` past creation. Effective cadence: "fires daily; only emits if a recipient has at least one Pending row >= AppointmentDueDays old".

#### OLD recipients

- Internal staff users with permission to approve/reject (the `ClinicStaffSupervisor` / `ClinicStaffSupport` role group).
- Computation: proc joins `Users` to `UserRoles` filtered to internal roles, returns one row per user with `EmailList` (their email) and `PhoneList` (their phone).

#### OLD email template code

- `EmailTemplate.AppointmentApproveRejectInternal` (`SchedulerDomain.cs:112`).
- ViewModel: `vEmailSenderViewModel { PendingAppointmentCount, ApprovedAppointmentCount }`.
- Subject literal: `"Updated Appointment Request"` (`SchedulerDomain.cs:113`).
- SMS body literal: `"Hi, Please check few detail of appointment:<br/> New Appointment Request :{PendingCount}  <br/>  Pending Appointment Request :{ApprovedCount}"` (`SchedulerDomain.cs:102`).

#### OLD state changes

Pure-notification. No `Update` / `SaveChanges` calls in the method. Idempotency: none; rerun = duplicate emails.

#### NEW current state

ABSENT. No `ApproveRejectInternalUserReminderJob` in `src\HealthcareSupport.CaseEvaluation.Domain\**\Jobs\` (Glob result confirmed -- only the 5 existing jobs live there).

#### Implementation plan

- **File:** `src\HealthcareSupport.CaseEvaluation.Domain\Notifications\Jobs\ApproveRejectInternalUserReminderJob.cs`.
- **Pattern:** event-bus + `INotificationDispatcher` (pattern 2, section 1.4). Publish one `ApproveRejectInternalUserReminderEto` per recipient.
- **Eto:** `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Notifications\Events\ApproveRejectInternalUserReminderEto.cs` -- fields `RecipientUserId : Guid`, `TenantId : Guid?`, `PendingCount : int`, `RecentlyApprovedCount : int`, `OccurredAt : DateTime`.
- **Handler:** `src\HealthcareSupport.CaseEvaluation.Application\Notifications\Handlers\ApproveRejectInternalUserReminderEmailHandler.cs` -- subscribes to the Eto, calls dispatcher with template code `ApproveRejectInternalUserReminder`.
- **New template code:** add `ApproveRejectInternalUserReminder` constant + seed body + `en.json` keys.
- **Recipient resolver (new):** `IInternalStaffRecipientResolver.ResolveAsync(NotificationKind kind)` -- returns identity users in the `ClinicStaffSupervisor` + `ClinicStaffSupport` role groups for the current tenant, with their `UserName + Email + PhoneNumber`. Counts are computed from `IRepository<Appointment, Guid>` filtered by `AppointmentStatus == Pending` and `CreationTime <= now - AppointmentDueDays`.
- **Setting:** reuse `CaseEvaluationSettings.BookingPolicy.AppointmentDueDays` (`CaseEvaluationSettings.cs:38`).
- **Cron:** `0 9 * * *` (09:00 PT daily -- after the 06:00/07:00/08:00/08:30 jobs). Constants on the class: `RecurringJobId = "appt-approve-reject-internal-reminder"`, `CronExpression = "0 9 * * *"`.
- **Registration:** add to `CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs` after the `PackageDocumentReminderJob` block (line 626).
- **Tests:** unit-test the count-window predicate (`pendingCount = q.Where(a => a.Status == Pending && a.CreationTime <= now.AddDays(-dueDays)).Count()`) and the empty-recipient short-circuit. Pattern: in-process `ICurrentTenant.Change` + Shouldly. There are NO existing job tests in `test\` (verified by Glob -- no `*JobTests.cs` or `*ReminderTests.cs`). This job seeds the test pattern.

#### Acceptance criteria

- 1 internal staff user, 1 Pending appointment older than `AppointmentDueDays` -> 1 email sent with correct counts; 0 SMS unless phone present.
- 0 Pending older than threshold -> 0 emails sent (per-tenant short-circuit logged at Information level).
- Multi-tenant: 2 tenants, each with own staff -- one job execution iterates both, counts are tenant-scoped.
- Run job twice in same day -> no dedup (matches OLD); de-spam handled by once-per-day cadence + idempotent count message.

#### Gotchas

- **Time zone**: cron in PT; `CreationTime.AddDays(-dueDays)` math is UTC since ABP `CreationTime` is UTC. Consequence: a Pending appointment created at 23:30 PT (07:30 UTC next day) "ages" by UTC date, which can be off-by-one against the staff's PT calendar. Match OLD behavior: use UTC math; document the off-by-one as parity.
- **Dedup of multi-day reminders**: OLD has none. NEW has none either (parity). Optional improvement (post-parity): write a `ReminderHistory` row on dispatch keyed by `(TenantId, RecipientUserId, TemplateCode, RecipientLocalDate)` and short-circuit on duplicate. Out of scope for this task.
- **Idempotency**: same as OLD; rerunning the Hangfire job in-day double-fires. Hangfire's job-execution log is not persisted long enough to dedup. Acceptable -- daily cadence + idempotent message body keeps spam minimal.
- **`UserClaim.UserId` parity flag**: OLD passes the ambient user-id into the proc but the proc body almost certainly ignores it (the workload report is staff-wide). Skip in NEW; flag as `// PARITY-FLAG` in the job class header.

---

### N1.2  DueDateDocumentApproachingJob (OLD type 4)

#### OLD source

- **Job runner:** `P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs:176-202` -- `AppointmentDueDateDocumentApproachingNotification()`.
- **Stored proc:** `spm.spAppointmentDueDateDocumentApproachingNotification @AppointmentId, @UserId` (`SchedulerDomain.cs:181`).
- **Template:** `EmailTemplate.AppointmentDocumentIncomplete` (`SchedulerDomain.cs:198`).

#### OLD query (predicate)

The proc name + the view-model `InCompleteDocumentNotificationViewModel` reveal the predicate:

> Approved appointments where `DueDate` is approaching AND at least one `AppointmentDocument` is still in (Pending, Rejected) status. Window = within `SystemParameter.ReminderCutoffTime` days of `DueDate`. Returns one row per appointment with `DocumentList` (semicolon-joined names of pending docs), `EmailList` (patient + booker emails), `PhoneList`, `RequestConfirmationNumber`.

This is the **conjunctive** version of OLD type 3 (DueDateApproaching, pure date) -- type 4 fires only when docs are still missing.

#### OLD cadence

- **Configurable** via `SystemParameter.ReminderCutoffTime` (`SystemParameter.cs:87-88`).
- **Trigger interval:** daily by OS scheduler. Effective cadence: per-row, fires every day the appointment is within the cutoff AND has pending docs (OLD has no last-sent log -> daily spam until docs uploaded; this is by design).

#### OLD recipients

- Patient + booker (email and SMS).
- Computation: proc returns `EmailList` (patient + booker email), `PhoneList` (patient phone). Internal staff are NOT recipients of this type -- type 8 covers staff.

#### OLD email template code

- `EmailTemplate.AppointmentDocumentIncomplete` (`SchedulerDomain.cs:198`).
- ViewModel: `vEmailSenderViewModel { AppointmentRequestConfirmationNumber, PendingDocList }`.
- Subject: `"Appointment Document Incomplete"` (`SchedulerDomain.cs:199`).
- SMS body: `"Appointment Request Number :<b style='font-size:17px'>{Number}</b><br/> Document Name :{DocumentList}"` (`SchedulerDomain.cs:187-188`).

#### OLD state changes

Pure-notification.

#### NEW current state

ABSENT. NEW has `PackageDocumentReminderJob` (`src\HealthcareSupport.CaseEvaluation.Domain\Notifications\Jobs\PackageDocumentReminderJob.cs`) which is the **superset** of OLD types 2 + 4 (it queries any Pending/Rejected doc whose appointment is at or past `PackageDocumentReminderDays` from due date). The Phase 14b job header comment (lines 31-36) says it ships ONE reminder cadence vs OLD's multi-cadence.

**Decision option A (recommended):** treat type 4 as ALREADY covered by `PackageDocumentReminderJob`. The OLD distinction between type 2 (any-time package-doc pending) and type 4 (pending AND due-date approaching) collapses in NEW because `PackageDocumentReminderJob` already gates on the cutoff window. Verify by reading the existing job; if confirmed, mark type 4 "covered" and stop.

**Decision option B (strict parity):** add a separate `DueDateDocumentApproachingJob` that fires the same cutoff with a distinct subject/body ("Appointment Document Incomplete" vs "Please Upload Pending Documents") so the two OLD email templates remain distinct in admin UI.

#### Implementation plan (option B if pursued)

- **File:** `src\HealthcareSupport.CaseEvaluation.Domain\Notifications\Jobs\DueDateDocumentApproachingReminderJob.cs`.
- **Pattern:** mirror `PackageDocumentReminderJob` exactly but use a tighter cutoff (e.g. `Documents.DueDateDocumentReminderDays` -- new setting, default 3 days vs the wider `PackageDocumentReminderDays = 7`).
- **Eto:** `DueDateDocumentApproachingReminderEto` -- same shape as `PackageDocumentReminderEto`.
- **Template:** add `DueDateDocumentReminder` constant; seed copy.
- **Cron:** `0 10 * * *` (10:00 PT daily, after the package-doc reminder so the message order is wide -> narrow).
- **Tests:** parameterize the cutoff predicate; verify the narrow window selects a strict subset of the wide window.

#### Acceptance criteria (option B)

- Approved appointment, due in 2 days, 1 pending doc -> reminder fires.
- Approved appointment, due in 5 days, 1 pending doc -> NO reminder (outside narrow window) but the wider `PackageDocumentReminderJob` will fire its own reminder if 5 < `PackageDocumentReminderDays`.
- Approved appointment, due in 2 days, ALL docs accepted -> no reminder.

#### Gotchas

- **Risk of duplicate emails** if both jobs fire (the wide and narrow windows overlap on the last 3 days). Either: (a) make the new job MORE selective (e.g. only fire when ALL docs are pending, vs the broad job firing when any are pending); (b) add `LastReminderSentTemplate` per (appointment, template) and skip if the broad job already covered today. Recommendation: choose option A (skip the new job entirely) unless Adrian wants the OLD copy distinction.
- **Time zone**: same as N1.1 -- cron in PT, math in UTC.
- **PARITY-FLAG**: option B doubles the email volume vs OLD's one-template-per-fire pattern. Adrian should confirm before shipping.

---

### N1.3  JdfReminderJob (OLD type 5)

#### OLD source

- **Job runner:** `P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs:204-232` -- `AppointmentJointDeclarationDocumentUploadNotification()`.
- **Stored proc:** `spm.spJointDeclarationDocumentUploadNotification @AppointmentId, @UserId` (`SchedulerDomain.cs:209`).
- **Template:** `EmailTemplate.UploadPendingDocuments` (`SchedulerDomain.cs:228`).

#### OLD query (predicate)

> Approved AME appointments where the JDF document is still missing (no `AppointmentDocument` row with `IsJointDeclaration = true` AND `Status != Rejected`) AND `DueDate` is within `SystemParameter.JointDeclarationUploadCutoffDays` of today.

Same window as the auto-cancel job (type 6) but fires BEFORE the cutoff: if cutoff = 3 days, reminder fires at 5/4/3 days from due (each daily run inside the window emits). Type 6 then auto-cancels at exactly cutoff.

#### OLD cadence

- **Configurable:** `SystemParameter.JointDeclarationUploadCutoffDays` (line 69-70).
- **Trigger:** daily.

#### OLD recipients

- Applicant attorney + defense attorney (the parties responsible for JDF).
- Computation: proc returns `EmailList` (attorney emails joined by `;`) and `PhoneList`.

#### OLD email template code

- `EmailTemplate.UploadPendingDocuments` (`SchedulerDomain.cs:228`) -- shared with type 2 in OLD; the body distinguishes via `vemailSenderViewModel.DueDate` content.
- Subject: `"Please Upload Pending Documents"` (`SchedulerDomain.cs:229`).
- SMS body (`SchedulerDomain.cs:215-217`):
  ```
  Appointment Request Number :<b style='font-size:17px'>{Number}</b>
  <br/> Due Date :{DueDate}
  <br/> Document Name :{DocumentList}
  ```

#### OLD state changes

Pure-notification. **Distinct from type 6** (which sets `AppointmentStatusId = Cancelled`).

#### NEW current state

ABSENT. NEW has `JointDeclarationAutoCancelJob` (the type-6 equivalent at `src\HealthcareSupport.CaseEvaluation.Domain\Notifications\Jobs\JointDeclarationAutoCancelJob.cs`) but NO type-5 reminder job that fires before the cutoff. The cutoff predicate `JointDeclarationCutoff.IsAtOrPastCutoff` (`src\HealthcareSupport.CaseEvaluation.Domain\AppointmentDocuments\JointDeclarationCutoff.cs`) returns true when `nowUtc >= dueDate - cutoffDays` -- this is the AT-OR-PAST gate, so to get the BEFORE-CUTOFF reminder we need a different predicate: `nowUtc < dueDate - cutoffDays + reminderLeadDays` (e.g., reminder fires `reminderLeadDays` before the auto-cancel cutoff).

#### Implementation plan

- **File:** `src\HealthcareSupport.CaseEvaluation.Domain\Notifications\Jobs\JdfReminderJob.cs`.
- **Pattern:** event-bus + `INotificationDispatcher`. Publish `JdfReminderEto` per appointment.
- **Eto:** `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Notifications\Events\JdfReminderEto.cs` -- `AppointmentId, TenantId, DueDate, OccurredAt`.
- **Handler:** `JdfReminderEmailHandler` -- resolves attorney recipients via `IAppointmentRecipientResolver(kind: JdfReminder)`, dispatches template `JDFReminder` (already exists in `NotificationTemplateConsts.Codes.JDFReminder`).
- **Setting (new):** `CaseEvaluationSettings.DocumentsPolicy.JointDeclarationReminderLeadDays` -- days BEFORE the auto-cancel cutoff at which reminders start; default 4 (so if cutoff = 3 days from due, reminders fire from day 7 through day 4 from due, then auto-cancel at day 3).
- **Predicate:** new helper `JointDeclarationReminderCutoff.IsInReminderWindow(dueDate, cutoffDays, leadDays, nowUtc)` -> `dueDate - (cutoffDays + leadDays) <= nowUtc < dueDate - cutoffDays`. Pure static for unit tests.
- **Query:** Approved AME appointments with `DueDate.HasValue` AND no JDF doc with `Status != Rejected` AND predicate true. Reuses the AME id (`CaseEvaluationSeedIds.AppointmentTypes.Ame`) from `JointDeclarationAutoCancelJob.cs:115`.
- **Cron:** `30 6 * * *` (06:30 PT daily, between 06:00 auto-cancel and 07:00 day-reminder so the order is: auto-cancel removes any newly-cutoff appointments first, then JDF reminder fires for the still-Approved set still inside the lead window).
- **Registration:** add to `ConfigureHangfireRecurringJobs` between the JDF auto-cancel and AppointmentDayReminder blocks (around line 612-616).

#### Acceptance criteria

- AME, Approved, due in 6 days, no JDF -> reminder fires (assuming default cutoff=3, lead=4 -> window is 4..7 days from due).
- AME, Approved, due in 3 days, no JDF -> NO reminder (auto-cancel job handles this case).
- AME, Approved, due in 6 days, JDF uploaded (Pending) -> NO reminder.
- AME, Approved, due in 6 days, JDF uploaded (Rejected) -> reminder fires (Rejected counts as missing per `JointDeclarationAutoCancelJob.cs:139`).
- QME / Other (non-AME) -> NEVER reminder (filter on `AppointmentTypeId == ameId`).

#### Gotchas

- **Race with auto-cancel job:** if the lead window includes the cutoff day itself, both jobs could fire on the same day. Use strict inequality `nowUtc < dueDate - cutoffDays` in the reminder predicate so the auto-cancel job owns the cutoff day.
- **Time zone:** appointment `DueDate` is stored as a date (no time). Both jobs treat `DueDate.AddDays(-cutoffDays)` as a UTC instant of midnight. For PT users near midnight this is off-by-one PT days vs UTC days. Document as parity (OLD has the same off-by-one).
- **Dedup**: rerun fires duplicate emails (matches OLD parity).
- **Idempotency**: pure read + event publish; no row mutations.
- **AME-REVAL parity-flag:** if a re-eval AME appointment-type id is added later (e.g., `AppointmentTypes.AmeReval`), the filter must include it. Currently only `AppointmentTypes.Ame` is in seed. Defer; flag in audit doc.

---

### N1.4  PendingDocumentSendToResponsibleUserJob (OLD type 8)

#### OLD source

- **Job runner:** `P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs:336-364` -- `AppointmentPendingDocumentSendToResponsibleUser()`.
- **Stored proc:** `spm.spAppointmentPendingDocumentSendToResponsibleUser @AppointmentId, @UserId` (`SchedulerDomain.cs:341`).
- **Template:** `EmailTemplate.UploadPendingDocuments` (`SchedulerDomain.cs:360`).
- **NOTE:** the actual `SendMail.SendSMTPMail` call at `SchedulerDomain.cs:361` is COMMENTED OUT and so is the `TwilioSmsService.SendSms` at line 352. **OLD type 8 is dead code** -- it queries the proc but does not actually email anyone. Confirm this is the OLD state before deciding whether to port.

#### OLD query (predicate)

> Appointments where one or more `AppointmentDocument` rows have `Status = Pending` AND `Appointment.PrimaryResponsibleUserId IS NOT NULL`. The proc returns one row per appointment with `EmailList` (the responsible user's email, looked up from `Users` via `PrimaryResponsibleUserId`), `DocumentList`, `RequestConfirmationNumber`, `DueDate`.

The "review" semantic means the docs have been uploaded by the patient and are awaiting staff review (Pending in OLD = uploaded, awaiting accept/reject). Distinct from type 4 (patient-side reminder for docs not yet uploaded).

#### OLD cadence

- Daily by OS scheduler. No SystemParameter knob is referenced in the OLD code -- cadence is purely "every run".

#### OLD recipients

- Single user: `Appointment.PrimaryResponsibleUserId` -- the staff member assigned to review.

#### OLD email template code

- `EmailTemplate.UploadPendingDocuments` -- but **the email is not actually sent** in OLD. Treat this as a `// PARITY-FLAG` ambiguous (per CLAUDE.md "Bug and deviation policy"): OLD code clearly INTENDS to send this email (proc exists, template exists, viewmodel built), but the send line is commented out.

#### OLD state changes

Pure-notification (or rather, pure-noop in current OLD state).

#### NEW current state

ABSENT (and `Appointment.PrimaryResponsibleUserId` is also absent -- see section 1.6).

#### Implementation plan

**Prerequisite migration:** add `PrimaryResponsibleUserId : Guid?` to `Appointment.cs` (NEW Domain) with FK -> `IdentityUser`. Add to `AppointmentUpdateDto` so staff can assign. Add EF Core migration.

After the prerequisite:

- **File:** `src\HealthcareSupport.CaseEvaluation.Domain\Notifications\Jobs\PendingDocumentResponsibleUserReminderJob.cs`.
- **Pattern:** event-bus + `INotificationDispatcher`. Publish `PendingDocumentResponsibleUserReminderEto` per (appointment, recipient).
- **Eto:** `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Notifications\Events\PendingDocumentResponsibleUserReminderEto.cs`.
- **Handler:** `PendingDocumentResponsibleUserReminderEmailHandler` -- resolves the responsible user's email from `IdentityUserManager`, dispatches new template `PendingDocumentResponsibleUserReminder`.
- **New template code:** `PendingDocumentResponsibleUserReminder` -- add to `NotificationTemplateConsts.Codes`, seed contributor, `en.json`.
- **Predicate:** Pending OR Rejected document on an appointment with non-null `PrimaryResponsibleUserId`. Group by `(AppointmentId, PrimaryResponsibleUserId)`; emit one event per appointment (recipient sees all pending doc names in the body).
- **Cron:** `0 11 * * *` (11:00 PT daily, latest of the day so all overnight uploads have a chance to surface).
- **Registration:** add to `ConfigureHangfireRecurringJobs`.

#### Acceptance criteria

- Appointment with `PrimaryResponsibleUserId = U`, 2 Pending docs -> 1 email to U with both doc names.
- Appointment with `PrimaryResponsibleUserId = NULL` -> SKIPPED (no recipient).
- Appointment with `PrimaryResponsibleUserId = U`, 0 Pending docs -> SKIPPED.
- Multiple appointments same responsible user -> one email per appointment (NOT a digest -- matches OLD's per-row pattern).

#### Gotchas

- **Bug-vs-design call:** OLD's send line is commented out. Adrian must rule:
  - "It's a bug -- type 8 was meant to ship": port and uncomment-equivalent (this plan).
  - "It's intentional -- type 8 was deferred in OLD": skip and remove from gap list.
  Recommendation: port it. The proc, template, and viewmodel are all production-ready in OLD; the commented-out send is consistent with "feature was finished but pulled at the last minute". Adding it in NEW closes a clear UX gap (responsible reviewers get no nudge today). Mark with `// PARITY-FLAG` linking the OLD source line for Adrian's manual test.
- **Migration timing:** the `PrimaryResponsibleUserId` column landing must happen before any UI lets staff assign it; otherwise the job has nothing to fire on. Plan migration as Phase 19a, the job as Phase 19b.
- **Time zone**: same as the others.
- **Dedup**: daily rerun = duplicate. Acceptable.
- **Multi-tenant**: scope the responsible-user lookup to the appointment's `TenantId`; the user must be in the same tenant.

---

### N1.5  PendingAppointmentDailyNotificationJob (OLD type 9)

#### OLD source

- **Job runner:** `P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs:72-85` -- `AppointmentPendingDailyNotification()`.
- **Stored proc:** `spm.spPendingAppointmentNotification @UserId` (`SchedulerDomain.cs:80`).
- **Template:** `EmailTemplate.PendingAppointmentDailyNotification` (`SchedulerDomain.cs:83`).

#### OLD query (predicate)

The proc takes a single `@UserId` parameter (hard-coded to `1`, line 78) and returns ONE ROW with a `Result` column containing pre-rendered HTML for the entire pending-appointment list. The predicate inside the proc is presumed:

> All appointments with `AppointmentStatusId IN (Pending, RescheduleRequested, CancellationRequested)` -- everything that needs internal-staff action -- formatted as an HTML table with columns: `RequestConfirmationNumber, PatientName, AppointmentType, DueDate, ResponsibleUser, PrimaryAttorney, Status, AgeInDays`.

This is a **digest** (one HTML email containing the list), not a per-row fan-out.

#### OLD cadence

- Daily by OS scheduler. No SystemParameter knob; the proc decides what to include.

#### OLD recipients

- Single inbox: `ServerSetting.Get<string>("clinicStaffEmail")` -- per `server-settings.json:57`, default `pooja.fithani@radixweb.com`. Multiple addresses supported via `;` separator (per the `server-settings.json:56` comment).

#### OLD email template code

- `EmailTemplate.PendingAppointmentDailyNotification` (`SchedulerDomain.cs:83`).
- ViewModel: `vEmailSenderViewModel { DailyNotificationContent = <pre-rendered HTML from proc> }`.
- Subject: `"Pending Appointment Request"` (`SchedulerDomain.cs:84`).
- The template's body is essentially `{DailyNotificationContent}` -- the proc does the heavy lifting; the email template wraps it in standard header/footer chrome.

#### OLD state changes

Pure-notification.

#### NEW current state

ABSENT.

#### Implementation plan

- **File:** `src\HealthcareSupport.CaseEvaluation.Domain\Notifications\Jobs\PendingAppointmentDailyDigestJob.cs`.
- **Pattern:** event-bus + `INotificationDispatcher`. Job builds the per-tenant pending list in C# (no proc), publishes one `PendingAppointmentDailyDigestEto` per tenant whose `OfficeEmail` setting is non-empty.
- **Eto:** `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Notifications\Events\PendingAppointmentDailyDigestEto.cs` -- `TenantId, RecipientEmails (CSV), DigestRows (List<DigestRowDto>), OccurredAt`. `DigestRowDto` carries the per-row fields needed for the template's HTML table.
- **Handler:** `PendingAppointmentDailyDigestEmailHandler` -- dispatches new template `PendingAppointmentDailyDigest` with `Variables = { Rows, RowCount }`. The template renders the HTML table from a Liquid loop (or an inline `{{Rows | render_table}}` helper if the renderer supports it). If the Phase 18 renderer does NOT support loops, fall back to building the HTML table in the handler and passing it as `BodyHtml` -- matches OLD's pre-rendered approach.
- **New template code:** `PendingAppointmentDailyDigest` -- add to `NotificationTemplateConsts.Codes`, seed, `en.json`.
- **Predicate:** appointments with `AppointmentStatus IN (Pending, RescheduleRequested, CancellationRequested)` for the tenant. Order by `CreationTime DESC`. Limit to (e.g.) 200 rows to keep email size sane; add a "and N more" footer if truncated.
- **Recipient resolution:** read `CaseEvaluationSettings.NotificationsPolicy.OfficeEmail` per-tenant; split on `;` for multi-recipient parity with OLD's `clinicStaffEmail` semantic.
- **Cron:** `0 7 * * 1-5` (07:00 PT Monday-Friday -- weekday-only digest matches OLD ops convention; weekends get no digest unless Adrian wants 7 days).
- **Registration:** add to `ConfigureHangfireRecurringJobs` after the package-doc reminder (line 626).

#### Acceptance criteria

- 1 tenant, `OfficeEmail = "ops@clinic.com"`, 5 Pending + 2 RescheduleRequested + 1 CancellationRequested -> 1 email to ops@clinic.com with 8-row table.
- 1 tenant, `OfficeEmail = ""` (unset) -> SKIPPED, log at Information.
- 1 tenant, 0 actionable rows -> SKIPPED, log at Information ("nothing to digest").
- 2 tenants -> 2 separate emails (one per tenant's office inbox).
- `OfficeEmail = "ops@clinic.com;owner@clinic.com"` -> single email with both as `To` (parity: OLD calls `SendMail.SendSMTPMail(email, subject, body)` with the full `;`-string; SMTP libs split on `;`).

#### Gotchas

- **Time zone:** cron in PT; the "as-of" cutoff inside the body should be the PT date so admins see "Pending as of 2026-05-04" not the UTC date. Build the as-of string from `TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pacificTime).ToString("MMM d, yyyy")`.
- **Dedup**: daily cadence + idempotent body content (it's a snapshot). Two runs same day -> two identical emails. Acceptable.
- **Idempotency**: pure read; safe to retry.
- **Empty-table edge case**: do NOT send an empty digest -- avoid noise.
- **Tenant explosion**: if 50 tenants each with 0 actionable rows, the job logs 50 "skip" messages. Use `LogDebug` for the per-tenant skip and `LogInformation` for the overall run summary.
- **Multi-recipient SMTP semantics**: ABP's `Volo.Abp.Emailing.IEmailSender.SendAsync(to, subject, body)` accepts a single string `to`. To honor `;`-separated multi-recipient parity, either: (a) loop and call `SendAsync` per address (clean but produces N messages); (b) pass the full string and rely on the underlying `SmtpClient` to split (matches OLD bit-for-bit, but driver-dependent). Recommendation: split and loop in the handler to make multi-recipient explicit and audit-friendly. PARITY-FLAG to record the deviation from OLD's single-message-multi-recipient.

---

## 3. Cross-job summary

### 3.1 Cron schedule (proposed final order, all PT)

| Time | Job |
|---|---|
| 06:00 | JointDeclarationAutoCancelJob (existing) |
| 06:30 | JdfReminderJob (NEW -- N1.3) |
| 07:00 | AppointmentDayReminderJob (existing) |
| 07:00 (Mon-Fri) | PendingAppointmentDailyDigestJob (NEW -- N1.5) |
| 08:00 | CancellationRescheduleReminderJob (existing) |
| 08:00 | RequestSchedulingReminderJob (existing) |
| 08:30 | PackageDocumentReminderJob (existing) |
| 09:00 | ApproveRejectInternalUserReminderJob (NEW -- N1.1) |
| 10:00 | DueDateDocumentApproachingReminderJob (NEW -- N1.2, IF option B) |
| 11:00 | PendingDocumentResponsibleUserReminderJob (NEW -- N1.4, gated on prereq migration) |

Net job count: 5 existing + 3 confirmed-new (N1.1, N1.3, N1.5) + 1 conditional (N1.2 option B) + 1 prereq-blocked (N1.4) = 9 (matches OLD if all ship) or 8 (option A on N1.2).

### 3.2 New `NotificationTemplateConsts.Codes` to add

- `ApproveRejectInternalUserReminder` (N1.1)
- `DueDateDocumentReminder` (N1.2 option B only)
- `PendingDocumentResponsibleUserReminder` (N1.4)
- `PendingAppointmentDailyDigest` (N1.5)

`JDFReminder` (N1.3) already exists at `NotificationTemplateConsts.cs:26`.

### 3.3 New / extended settings

| Setting | New? | Section | Default |
|---|---|---|---|
| `BookingPolicy.AppointmentDueDays` | exists (line 38) | reused for N1.1 | 7 |
| `DocumentsPolicy.PackageDocumentReminderDays` | exists (line 67) | reused for N1.2 option A | 7 |
| `DocumentsPolicy.JointDeclarationReminderLeadDays` | NEW | for N1.3 | 4 |
| `DocumentsPolicy.JointDeclarationUploadCutoffDays` | exists (line 62) | reused for N1.3 cutoff | 7 |
| `NotificationsPolicy.OfficeEmail` | exists (line 80) | reused for N1.5 | "" |

### 3.4 New Etos

- `Notifications/Events/ApproveRejectInternalUserReminderEto.cs`
- `Notifications/Events/DueDateDocumentApproachingReminderEto.cs` (option B only)
- `Notifications/Events/JdfReminderEto.cs`
- `Notifications/Events/PendingDocumentResponsibleUserReminderEto.cs`
- `Notifications/Events/PendingAppointmentDailyDigestEto.cs`

All in `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Notifications\Events\` -- mirroring `PackageDocumentReminderEto.cs` shape.

### 3.5 Test pattern (no precedent in repo)

There are zero job tests today. This task seeds the pattern. Recommendation:

- **Pure predicate tests** (cheapest, highest value): unit-test `JointDeclarationReminderCutoff.IsInReminderWindow` and any other date-window helpers as static-class tests in `test\HealthcareSupport.CaseEvaluation.Domain.Tests\Notifications\Jobs\<JobName>PredicateTests.cs`. xUnit `[Theory]` with `[InlineData]` for the boundary cases (at-cutoff, one-day-before-cutoff, one-day-after-cutoff, null due-date, zero cutoff, negative cutoff).
- **Integration tests** (one per job): in `test\HealthcareSupport.CaseEvaluation.Application.Tests\Notifications\Jobs\<JobName>Tests.cs`, seed an appointment via `CaseEvaluationTestDataBuilder`, call `job.ExecuteAsync()`, assert the expected `*Eto` was published via a captured `ILocalEventBus` test double (or alternatively assert `IBackgroundJobManager` was called for the per-recipient pattern). Existing test base: `CaseEvaluationApplicationTestBase`.
- **`DateTime.UtcNow` injection**: existing jobs read `DateTime.UtcNow` directly. For deterministic tests, refactor each job to take an `IClock` (ABP's `Volo.Abp.Timing.IClock`) and call `_clock.Now.UtcDateTime` (or accept a `Func<DateTime>` clock). Phase the refactor across the new jobs and document for retroactive application to the existing 5 jobs (separate task).
- **Time-zone test**: a single `TryGetPacificTimeZone` test asserting the IANA / Windows / UTC fallback chain belongs in `test\HealthcareSupport.CaseEvaluation.Application.Tests\Notifications\Jobs\TimeZoneFallbackTests.cs`.

### 3.6 Idempotency / dedup decision (cross-cutting)

- **OLD**: zero dedup. Daily reruns spam.
- **NEW (this task)**: match OLD parity -- no dedup. Daily cadence + Hangfire's `RecurringJob.AddOrUpdate` (which prevents *parallel* duplicates inside the Hangfire process but NOT cross-day ones) keeps spam to one email per recipient per day, which is acceptable for this workload.
- **Post-parity improvement (out of scope):** introduce `Notifications/ReminderHistory` aggregate keyed by `(TenantId, RecipientUserId, TemplateCode, RecipientLocalDate)`. Job consults this before publishing each Eto; handler writes a row after dispatch. Feature-flag behind `Notifications.Reminders.DedupEnabled` setting. File this as a separate research stage once parity ships.

### 3.7 OLD-doc references consulted

- `P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs` (full read).
- `P:\PatientPortalOld\PatientAppointment.Models\Enums\ReminderTypes.cs` (full read).
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\SystemParameter.cs` (full read).
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\Appointment.cs` (full read).
- `P:\PatientPortalOld\PatientAppointment.Api\server-settings.json` (clinicStaffEmail line).
- `P:\PatientPortalOld\Documents_and_Diagrams\Postman Collection\Patient Appointment API.postman_collection.json` (Scheduler/postscheduler endpoint shape).
- `P:\PatientPortalOld\Documents_and_Diagrams\Architecture\SoCal Project Overview Document.pdf` -- referenced via the existing parity doc; PDF render not available in environment.

### 3.8 Open questions for Adrian

1. **N1.2 option A vs option B**: collapse into the existing `PackageDocumentReminderJob` (option A, recommended) or ship as a distinct narrow-window reminder (option B, strict-parity)?
2. **N1.4 prerequisite**: approve `PrimaryResponsibleUserId : Guid?` migration on `Appointment` before implementing the job? Without it, the job has nothing to fire on.
3. **N1.4 bug-vs-design**: OLD type 8's send call is COMMENTED OUT in `SchedulerDomain.cs:361`. Port (assume bug) or skip (assume intentional defer)?
4. **N1.5 weekend behavior**: PT digest Mon-Fri only (`0 7 * * 1-5`) or 7 days a week?
5. **Clock refactor**: introduce `IClock` injection into the new jobs (and retroactively into the 5 existing) so all date math is testable? Or keep `DateTime.UtcNow` direct calls and rely on integration tests that mutate system time?
6. **Reminder template wider merge**: the `NotificationTemplateConsts.Codes` set is 23 codes; the brief mentioned a 59-code OLD-parity merge. Confirm 4 new codes from this stage (N1.1, N1.4, N1.5, plus N1.2 option B) get added incrementally rather than waiting for the wider merge.
