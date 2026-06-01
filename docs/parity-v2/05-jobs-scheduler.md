# 05. Jobs & scheduler -- OLD vs NEW behavioral parity

## Coverage

Scope: recurring background jobs and the scheduler-driven notification
engine. OLD anchor is `PatientAppointment.Domain\Core\SchedulerDomain.cs`
(the 9 `ReminderTypes`) invoked through
`PatientAppointment.Api\Controllers\Api\Core\SchedulerController.cs`
(`POST /api/Scheduler/postscheduler`). NEW anchor is the 9 Hangfire
recurring jobs registered in
`src\HealthcareSupport.CaseEvaluation.HttpApi.Host\CaseEvaluationHttpApiHostModule.cs`
(`ConfigureHangfireRecurringJobs`, lines 1035-1108) plus the
`SendAppointmentEmailJob` and `GenerateAppointmentPacketJob` worker jobs.

Files read in full (OLD):
- `PatientAppointment.Domain\Core\SchedulerDomain.cs` (all 9 reminder
  methods + the `ConfigureNotificaion` dispatch switch).
- `PatientAppointment.Api\Controllers\Api\Core\SchedulerController.cs`.
- `PatientAppointment.Models\Enums\ReminderTypes.cs`.
- `PatientAppointment.Models\Models\SchedulerParameters.cs`.
- `_local\stub-procs.sql` (the 9 `spm.*` notification procs -- stubs in
  this local copy).

Files read in full (NEW):
- `Domain\Notifications\Jobs\DueDateApproachingJob.cs`
- `Domain\Notifications\Jobs\DueDateDocumentIncompleteJob.cs`
- `Domain\Notifications\Jobs\PackageDocumentReminderJob.cs`
- `Domain\Notifications\Jobs\JointDeclarationAutoCancelJob.cs`
- `Domain\Notifications\Jobs\InternalStaffQueueDigestJob.cs`
- `Domain\Notifications\Jobs\PendingDailyDigestJob.cs`
- `Domain\Appointments\Notifications\Jobs\AppointmentDayReminderJob.cs`
- `Domain\Appointments\Notifications\Jobs\CancellationRescheduleReminderJob.cs`
- `Domain\Appointments\Notifications\Jobs\RequestSchedulingReminderJob.cs`
- `Domain\Appointments\Jobs\SendAppointmentEmailJob.cs`
- `HttpApi.Host\CaseEvaluationHttpApiHostModule.cs` (Hangfire wiring).

### Key structural finding -- OLD cadence is NOT in source

OLD `SchedulerDomain` does NOT schedule anything. Each reminder method
is a one-shot fire that runs once per inbound HTTP `POST
/api/Scheduler/postscheduler` carrying a `ScheduleTypeId`
(`SchedulerParameters.ScheduleTypeId`). The actual cadence (daily?
T-7/T-3/T-1? hourly?) lived OUTSIDE the C# source -- in a host-side
trigger (SQL Agent job / AWS scheduled task / external cron) that POSTed
to that endpoint on whatever schedule was configured on the server. That
trigger config is not in the repo, and the 9 `spm.*` stored procs in
`_local\stub-procs.sql` are empty stubs (`WHERE 1=0`), so the row-
selection windows the procs encoded are also not in source.

Consequence for this audit: "single-fire vs multi-stage cadence" cannot
be judged against OLD source -- OLD source has no cadence at all. The
NEW jobs' cron expressions and T-minus windows are therefore design
choices, not literal ports. They are scored on whether the right job
runs the right selection logic, with cadence flagged as a parity
UNKNOWN (open question) rather than a gap.

### Trigger model -- equivalent, different implementation

OLD: external POST -> `SchedulerController` -> `ConfigureNotificaion`
switch -> one reminder method runs once, queries a stored proc, loops
stakeholders, sends email (+ Twilio SMS on some).

NEW: Hangfire `RecurringJob.AddOrUpdate` (America/Los_Angeles timezone,
`CaseEvaluationHttpApiHostModule.cs:1035-1108`) -> each job's
`ExecuteAsync` iterates tenants (`ICurrentTenant.Change` +
`IDataFilter.Disable<IMultiTenant>()` for discovery), queries via
`IRepository`, publishes a local event (`ILocalEventBus`) OR enqueues a
per-recipient `SendAppointmentEmailJob` via `IBackgroundJobManager`. A
`*EmailHandler` subscriber renders the template and fans out.

This is the expected SQL-Agent/stored-proc -> Hangfire/recurring-job
translation and is NOT a gap.

## Summary counts

| Class | Count |
|---|---|
| Missing behavior | 1 |
| Partial behavior | 3 |
| Intent deviation | 1 |
| Equivalent (different implementation) | 6 |
| OLD-bug (do not port) | 3 |

## Job map

OLD reminder (ReminderTypes id) | OLD trigger / row selection | OLD cadence | NEW job | Status

| OLD reminder | Trigger / selection | Cadence (OLD) | NEW job | Status |
|---|---|---|---|---|
| 1. AppointmentApproveRejectInternalUser (`SchedulerDomain.cs:87`) | `spAppointmentApproveRejectInternalUserNotification @AppointmentStatusId=1,@UserId`; per-staff pending+approved counts; email + SMS | external (unknown) | `InternalStaffQueueDigestJob` (`InternalStaffQueueDigestJob.cs`, cron `15 9 * * *`) | Partial (email only; SMS dropped; recipient role-resolved not proc-resolved) |
| 2. AppointmentPackageDocumentPending (`SchedulerDomain.cs:119`) | `spAppointmentPackageDocumentPendingNotification`; pending package docs; email (+ primary CC) + SMS; `UploadPendingDocuments` template | external (unknown) | `PackageDocumentReminderJob` (`PackageDocumentReminderJob.cs`, cron `30 8 * * *`) | Partial (single cutoff fire vs likely multi-cadence; SMS dropped) |
| 3. AppointmentDueDateApproaching (`SchedulerDomain.cs:152`) | `spAppointmentDueDateApproachingNotification`; due-date approaching; email; `AppointmentDueDateReminder` template; NO SMS in OLD | external (unknown) | `DueDateApproachingJob` (`DueDateApproachingJob.cs`, cron `15 8 * * *`, windows T-14/7/3) | Equivalent (different implementation) |
| 4. AppointmentDueDateDocumentApproaching (`SchedulerDomain.cs:176`) | `spAppointmentDueDateDocumentApproachingNotification`; incomplete docs near due date; email + SMS; `AppointmentDocumentIncomplete` template | external (unknown) | `DueDateDocumentIncompleteJob` (`DueDateDocumentIncompleteJob.cs`, cron `45 8 * * *`, window T-7) | Partial (single T-7 fire; SMS dropped) |
| 5. AppointmentJointDeclarationDocumentUpload (`SchedulerDomain.cs:204`) | `spJointDeclarationDocumentUploadNotification`; JDF not uploaded; email + SMS; `UploadPendingDocuments` template | external (unknown) | (no dedicated JDF-upload reminder job) -- nearest is `PackageDocumentReminderJob` which fires on `IsJointDeclaration` rows | Partial (folded into package-doc reminder; not a distinct JDF reminder; SMS dropped) |
| 6. AppointmentAutoCancelled (`SchedulerDomain.cs:234`) | `spAppointmentAutoCancelledNotification`; appointments auto-cancelled for missing docs; email + SMS; `AppointmentCancelledDueDate` template. OLD ONLY notifies -- the cancel happened elsewhere | external (unknown) | `JointDeclarationAutoCancelJob` (`JointDeclarationAutoCancelJob.cs`, cron `0 6 * * *`) does BOTH the cancel transition AND publishes `AppointmentAutoCancelledEto` -> `JdfAutoCancelledEmailHandler` | Equivalent (different implementation; NEW also performs the cancel, which is an improvement) |
| 7. AppointmentPendingReminderStaffUsers (`SchedulerDomain.cs:261`) | `spAppointmentPendingReminderStaffUsersNotification`; "next day pending appointments" to staff; `AppointmentPendingNextDay` template. SendMail commented out (`:309`) -- OLD never sent it | external (unknown) | none | OLD-bug (do not port -- dead code; OLD send line commented out) |
| 8. AppointmentPendingDocumentSendToResponsibleUser (`SchedulerDomain.cs:336`) | `spAppointmentPendingDocumentSendToResponsibleUser`; pending docs to the appointment's ResponsibleUser. Both SMS (`:352`) and SendMail (`:361`) commented out -- OLD never sent it | external (unknown) | none | Missing (no ResponsibleUser pending-doc reminder in NEW; flagged because intent is plausible even though OLD code was dead) |
| 9. PendingAppointmentDailyNotification (`SchedulerDomain.cs:72`) | `spPendingAppointmentNotification @UserId`; daily fan-in digest of all pending requests to `clinicStaffEmail`; `PendingAppointmentDailyNotification` template | external (unknown) | `PendingDailyDigestJob` (`PendingDailyDigestJob.cs`, cron `0 9 * * *`) | Equivalent (different implementation) |
| -- (no OLD equivalent) | -- | -- | `RequestSchedulingReminderJob` (`0 8 * * *`, CCR Sec. 31.5, T+30/60/75/85/90) | NEW-only (CCR-driven; out of OLD scope) |
| -- (no OLD equivalent) | -- | -- | `CancellationRescheduleReminderJob` (`0 8 * * *`, CCR Sec. 34(e), 45/55 elapsed days) | NEW-only (CCR-driven; out of OLD scope) |
| -- (no OLD equivalent) | -- | -- | `AppointmentDayReminderJob` (`0 7 * * *`, T-7/T-1 before AppointmentDate) | NEW-only (appointment-day reminder; OLD had no equivalent in `SchedulerDomain`) |
| OLD `SendMail.SendSMTPMail` inline | event-driven send | n/a | `SendAppointmentEmailJob` (`AsyncBackgroundJob`, on-demand worker) | Equivalent (out-of-band email delivery worker) |

## Behavioral gaps

### G-05-01 -- ResponsibleUser pending-document reminder not implemented

- **Class:** Missing behavior
- **OLD:** `SchedulerDomain.AppointmentPendingDocumentSendToResponsibleUser()`
  (`SchedulerDomain.cs:336-364`); ReminderTypes id 8.
- **NEW:** No job targets the appointment's ResponsibleUser with a
  pending-document reminder. `PackageDocumentReminderJob` publishes a
  `PackageDocumentReminderEto` keyed by document/appointment, and the
  recipient set is resolved by the handler -- it is not the dedicated
  "send the outstanding-doc list to the one ResponsibleUser" path.
- **What it is:** A reminder addressed specifically to the internal
  ResponsibleUser (the staff member who owns the appointment) listing
  the request number, due date, and outstanding document names.
- **Why it existed:** Gives the owning staff member a personal nudge to
  chase down documents, distinct from the package-doc reminder that goes
  to external stakeholders.
- **What it does + user impact:** In OLD this code path is DEAD -- both
  the SMS send (`:352`) and the email send (`:361`) are commented out,
  so OLD never actually delivered it. NEW therefore loses nothing a real
  OLD user ever received. Listed as Missing (not OLD-bug) because the
  intent is coherent and a "remind the responsible owner" reminder is a
  reasonable parity target if staff expect it; but it is low priority.
- **Plain-English:** OLD had a half-built "remind the staff owner about
  missing paperwork" feature that was switched off in the code. NEW
  doesn't have it either. No live behavior was lost.
- **Keep in NEW?** Optional. Build only if staff testing shows they want
  a personal owner-targeted doc-chase reminder. Otherwise the external
  package-doc reminder covers the visible behavior.

### G-05-02 -- Joint-declaration upload reminder folded into package-doc reminder

- **Class:** Partial behavior
- **OLD:** `SchedulerDomain.AppointmentJointDeclarationDocumentUploadNotification()`
  (`SchedulerDomain.cs:204-232`); ReminderTypes id 5; distinct
  `spJointDeclarationDocumentUploadNotification` proc; `UploadPendingDocuments`
  template; sent email + SMS.
- **NEW:** No dedicated JDF-upload reminder. `PackageDocumentReminderJob`
  (`PackageDocumentReminderJob.cs:147-154`) publishes a
  `PackageDocumentReminderEto` with an `IsJointDeclaration` flag for JDF
  rows, so JDF-not-uploaded reminders ride the same daily package-doc
  pass rather than running as a separate reminder with its own cadence.
- **What it is:** OLD had two separate reminders -- generic package docs
  (id 2) and the joint-declaration document specifically (id 5) -- each
  with its own stored proc and trigger.
- **Why it existed:** The JDF is the gating document for AME
  appointments (its absence auto-cancels the appointment), so OLD gave
  it a dedicated reminder track separate from ordinary package docs.
- **What it does + user impact:** Functionally a JDF-outstanding row
  still produces a reminder in NEW, so the stakeholder is still nudged.
  The deviation is that NEW cannot remind on a JDF-specific cadence
  independent of the package-doc cadence, and uses one
  template-selection branch instead of two distinct proc-driven sends.
  Lower-fidelity but the user-visible "you still owe the JDF" reminder
  survives.
- **Plain-English:** OLD nagged about the joint-declaration form on its
  own schedule; NEW nags about it as part of the general "missing
  documents" daily nag. The reminder still goes out.
- **Keep in NEW?** Acceptable as-is for parity. Split into a dedicated
  JDF reminder only if the clinic wants JDF-specific timing.

### G-05-03 -- Multi-stage cadence collapsed to single daily fire (package + incomplete-doc reminders)

- **Class:** Partial behavior
- **OLD:** Reminders 2 (`AppointmentPackageDocumentPendingNotification`,
  `:119`) and 4 (`AppointmentDueDateDocumentApproachingNotification`,
  `:176`) each ran once per inbound POST. How OLD spaced repeated nags
  (e.g. T-7 then T-3 then T-1) was decided by the external trigger that
  POSTed `ScheduleTypeId` on a schedule -- not in source, and the
  `spm.*` procs are stubs here.
- **NEW:** `PackageDocumentReminderJob` fires once per day at the single
  `Documents.PackageDocumentReminderDays` cutoff
  (`PackageDocumentReminderJob.cs:91-110`, code comment lines 30-36
  explicitly notes "OLD shipped multiple reminder cadences (T-7, T-3,
  T-1) ... NEW Phase 14b ships ONE reminder at the configured cutoff").
  `DueDateDocumentIncompleteJob` fires once at a single T-7 window
  (`DueDateDocumentIncompleteJob.cs:39`).
- **What it is:** Whether OLD repeated these reminders on a staged
  countdown.
- **Why it existed:** Escalating reminders pressure stakeholders to
  upload before the document-completeness deadline.
- **What it does + user impact:** If OLD did run multi-stage countdowns,
  NEW stakeholders get fewer nudges (one per appointment instead of a
  T-7/T-3/T-1 series), which could reduce on-time document completion.
  Cannot be confirmed against OLD source -- see Open Questions. The
  RIGHT job runs and selects the right rows; only the repeat frequency
  is in question, so this is Partial not Missing.
- **Plain-English:** OLD may have reminded people about missing
  documents several times as a deadline approached; NEW reminds once.
  We can't tell from the old code how many times OLD reminded.
- **Keep in NEW?** Single-fire is fine for MVP. Add staged windows
  post-parity if stakeholder testing shows one reminder is not enough.
  (`DueDateApproachingJob` already uses staged T-14/7/3, so the pattern
  exists to copy.)

### G-05-04 -- Internal-staff approve/reject queue digest: recipient model + SMS leg differ

- **Class:** Partial behavior
- **OLD:** `SchedulerDomain.AppointmentApproveRejectInternalUserNotification()`
  (`SchedulerDomain.cs:87-117`); ReminderTypes id 1. Calls
  `spAppointmentApproveRejectInternalUserNotification @AppointmentStatusId=1,@UserId`;
  the proc returned the recipient `EmailList`/`PhoneList` and the
  PendingAppointmentCount + ApprovedAppointmentCount; OLD sent SMS via
  Twilio (`:105`) AND email (`:113`).
- **NEW:** `InternalStaffQueueDigestJob` (`InternalStaffQueueDigestJob.cs`)
  counts Pending + Approved per tenant (`:92-93`), resolves recipients by
  ABP role membership ("Staff Supervisor", "Clinic Staff" --
  `InternalStaffRoles`, `:40-44`), publishes one
  `InternalStaffQueueDigestEto` per staff user. Email only; no SMS (code
  comment `:29-30` states "OLD's SMS leg (Twilio at OLD :105) is dropped
  for Phase 1").
- **What it is:** The daily queue-counts heads-up to internal staff.
- **Why it existed:** Tells staff how many requests are waiting so the
  queue does not stall.
- **What it does + user impact:** Email digest is delivered correctly;
  the recipient set is now role-driven (a cleaner model than the OLD
  proc-returned list) and per-staff rather than one blast. The lost
  behavior is the SMS copy -- staff who relied on a text get only email.
- **Plain-English:** Staff still get the daily "here's your queue"
  email. They no longer get the same alert as a text message.
- **Keep in NEW?** Email digest: yes, keep. SMS: re-add only when the
  Twilio rollout lands (see G-05-INTENT-01); not a Phase 1 blocker.

### G-05-INTENT-01 -- All SMS legs dropped across every reminder

- **Class:** Intent deviation
- **OLD:** Reminders 1, 2, 4, 5, 6 each sent a Twilio SMS in addition to
  email (`SchedulerDomain.cs:105, 137, 191, 218, 249`) via
  `ITwilioSmsService.SendSms`. Reminders 3 and 9 were email-only; 7 and
  8 sent nothing (commented out).
- **NEW:** No SMS is delivered anywhere in the job/notification pipeline.
  `Volo.Abp.Sms` + Twilio provider modules are not referenced by any
  project; `NotificationDispatcher.cs:20-27` renders a `BodySms` field
  but explicitly notes delivery "is not wired here ... belongs with the
  Twilio creds rollout". Grep for `Twilio|SendSms|SmsService` under
  `src\` returns only comments and a `BodySms` ETO field -- no sender.
- **What it is:** The text-message copy of reminders.
- **Why it existed:** Stakeholders (attorneys, patients) who do not
  check email promptly got an SMS nudge for time-sensitive document and
  due-date reminders.
- **What it does + user impact:** Recipients who depended on SMS get no
  text for ANY reminder. For document/due-date reminders this could
  reduce on-time response. This is a deliberate Phase 1 scope cut
  (decision recorded in code comments), not an oversight, but it changes
  the outcome (fewer channels reach the recipient), so it is classed as
  an intent deviation rather than "equivalent".
- **Plain-English:** OLD texted people about reminders; NEW only emails.
  This was a deliberate decision to defer texting until the texting
  service is set up.
- **Keep in NEW?** Re-add SMS when the Twilio/ACS-SMS credentials roll
  out (master-plan section 18.3 per the dispatcher comment). Until then,
  email-only is the accepted Phase 1 behavior.

## Equivalent -- different implementation

These are correct ports where the implementation differs but the outcome
(right job, right rows, right recipients) matches; NOT gaps.

1. **Trigger mechanism: external POST -> Hangfire recurring job.** OLD
   `SchedulerController.postscheduler` fired one reminder per inbound
   request driven by an external host scheduler; NEW registers nine
   `RecurringJob.AddOrUpdate` crons in America/Los_Angeles time
   (`CaseEvaluationHttpApiHostModule.cs:1035-1108`). Expected SQL-Agent
   -> Hangfire translation.

2. **Stored-proc row selection -> EF/LINQ queries.** OLD selected rows
   via `spm.*` stored procs returning JSON; NEW queries `IRepository`
   with LINQ predicates inside `ProcessTenantAsync`. Same selection
   intent, modern data access.

3. **Reminder #3 (due-date approaching) -> `DueDateApproachingJob`.**
   Email-only in OLD (no SMS leg), email-only in NEW; NEW adds staged
   T-14/7/3 windows (`DueDateApproachingJob.cs:37`) plus a terminal-
   status guard (`:43-48`) so cancelled/completed appointments do not
   fire spurious reminders. Behavior-equivalent + safer.

4. **Reminder #6 (auto-cancelled notification) -> `JointDeclarationAutoCancelJob`.**
   OLD only NOTIFIED about an already-cancelled appointment (the cancel
   itself happened elsewhere, host-side). NEW's job DOES the cancel
   transition (`JointDeclarationAutoCancelJob.cs:168-189`,
   `Approved -> CancelledNoBill`) AND publishes
   `AppointmentAutoCancelledEto` -> `JdfAutoCancelledEmailHandler`. The
   notification outcome matches OLD; NEW additionally owns the cancel
   (an improvement -- OLD's cancel logic was not in this codebase).

5. **Reminder #9 (pending daily digest) -> `PendingDailyDigestJob`.**
   OLD sent one fan-in HTML block of all pending requests to a single
   `clinicStaffEmail` (`SchedulerDomain.cs:72-85`). NEW builds one
   `PendingDailyDigestEto` per tenant with a row list and the handler
   targets the per-tenant `NotificationsPolicy.OfficeEmail` setting.
   Same single-recipient daily digest, tenant-scoped.

6. **Inline `SendMail.SendSMTPMail` -> `SendAppointmentEmailJob`
   worker.** OLD sent email synchronously inside the reminder method;
   NEW enqueues a `SendAppointmentEmailJob` (`AsyncBackgroundJob`) so
   SMTP latency does not block, with packet-attachment support
   (`SendAppointmentEmailJob.cs:110-176`). Out-of-band delivery is a
   correct modernization.

## OLD bugs (do not port)

1. **Hardcoded `UserId = 1` in `AppointmentPendingDailyNotification`
   (`SchedulerDomain.cs:78`).** The daily-pending proc is called with
   `@UserId = 1` (the real `UserClaim.UserId` line is commented out at
   `:79`). Because this is a scheduler job with no logged-in user, OLD
   hardcoded user 1. NEW's `PendingDailyDigestJob` correctly iterates
   tenants and queries all Pending rows per tenant, not "as user 1" --
   do not port the hardcode. (This is the `UserId=1` scheduler bug
   called out in the root CLAUDE.md bug policy.)

2. **`AppointmentPendingReminderStaffUsersNotification` never sends
   (`SchedulerDomain.cs:309`).** The `SendMail.SendSMTPMail(item.EmailList,
   "Next day Pending Appointments", emailBody)` line is commented out, so
   ReminderTypes id 7 builds an email body and then discards it. Dead
   code -- do not port. NEW correctly has no equivalent.

3. **`AppointmentPendingDocumentSendToResponsibleUser` never sends
   (`SchedulerDomain.cs:352, 361`).** Both the Twilio SMS (`:352`) and
   the `SendMail.SendSMTPMail` (`:361`) lines are commented out; the
   method does work (queries the proc, builds the body) and sends
   nothing. Dead code -- see G-05-01. Do not port the dead send; only
   build the feature fresh if staff want it.

Note: the `UserClaim.UserId` used by reminders 1-8 (`:94, :123, :156,
:180, :208, :238, :265, :340`) is itself suspect for a scheduler context
(a background job has no user claim), but reminders 2-8 pass it as the
proc's `@UserId` filter. Whether that silently scoped the proc to a
specific operator's view is unknowable here (procs are stubs). NEW
side-steps this entirely by iterating tenants with the multi-tenant
filter disabled for discovery -- no user-claim dependency. Flag, do not
port the claim dependency.

## Open questions

1. **What was OLD's real cadence per `ScheduleTypeId`?** The external
   trigger that POSTed to `/api/Scheduler/postscheduler` is not in the
   repo. Whether reminders 2/4/5 ran once or on a T-7/T-3/T-1 countdown
   determines whether G-05-03 is a true behavioral loss or a cosmetic
   one. Needs the host-side SQL Agent / scheduled-task config or a
   stakeholder who remembers the OLD schedule.

2. **Did the `spm.*` procs filter by `@UserId`?** The procs are stubs in
   `_local\stub-procs.sql`. If the real procs used `@UserId` to scope
   rows (e.g. only that operator's appointments), the NEW
   tenant-wide selection is BROADER than OLD. Needs the real proc
   bodies. Likely not material in a single-tenant/single-office Phase 1.

3. **Should `AppointmentDayReminderJob` exist in OLD-parity scope?** It
   is NEW-only (no OLD `SchedulerDomain` equivalent) and is justified by
   CCR text rather than OLD behavior. Confirm it belongs in the OLD-
   parity surface or is purely a NEW CCR-compliance addition.

4. **Reminder #1 SMS body format.** OLD's SMS (`SchedulerDomain.cs:102`)
   embedded HTML (`<br/>`) into an SMS body -- itself likely an OLD bug
   (SMS does not render HTML). If SMS is re-added in NEW, send plain
   text, not the OLD HTML body. Flag for the Twilio rollout.
