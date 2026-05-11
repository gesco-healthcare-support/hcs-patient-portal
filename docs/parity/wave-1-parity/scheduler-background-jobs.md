---
feature: scheduler-background-jobs
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\SchedulerController.cs
old-docs:
  - socal-project-overview.md (lines 569-593)
  - data-dictionary-table.md (SystemParameters cutoff fields)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: system (no UI; backend scheduler triggers)
depends-on:
  - it-admin-system-parameters
  - it-admin-notification-templates
required-by:
  - external-user-appointment-package-documents   # multi-step reminder cadence
  - external-user-appointment-joint-declaration   # JDF auto-cancel + reminder
  - clinic-staff-appointment-approval             # staff approval reminder
---

# Scheduler / background jobs

## Purpose

Background scheduler runs scheduled tasks for notifications, reminders, and auto-cancellation. OLD has 9 scheduled job types covering the multi-step reminder cadence from the spec.

**Strict parity with OLD.** Replace OLD's custom scheduler with Hangfire (ABP-integrated).

## OLD behavior (binding)

### Reminder types (per `SchedulerDomain.ConfigureNotificaion` switch)

| ID | Name | What it does |
|----|------|--------------|
| 1 | AppointmentApproveRejectInternalUser | Staff reminder for pending approval/rejection of appointments |
| 2 | AppointmentPackageDocumentPending | Patient reminder for incomplete package documents |
| 3 | AppointmentDueDateApproaching | Patient reminder for due date approaching |
| 4 | AppointmentDueDateDocumentApproaching | Patient reminder for due date + docs still pending |
| 5 | AppointmentJointDeclarationDocumentUpload | Attorney reminder for missing JDF |
| 6 | AppointmentAutoCancelled | All-stakeholder notification when AME auto-cancelled due to missing JDF |
| 7 | AppointmentPendingReminderStaffUsers | Daily reminder to internal staff users about pending tasks |
| 8 | AppointmentPendingDocumentSendToResponsibleUser | Reminder to PrimaryResponsibleUserId about pending document review |
| 9 | PendingAppointmentDailyNotification | Daily digest to clinic staff email of all pending appointments |

### Trigger pattern

`POST /api/Scheduler/postscheduler` with `SchedulerParameters { ScheduleTypeId }` -- invoked by the OS-level scheduler (Windows Task Scheduler typically) at configured intervals. Each reminder type queries via stored proc, builds emails/SMS, sends.

### Config

Cutoff days configured in `SystemParameters`:
- `ReminderCutoffTime` -- when to start sending package-doc reminders
- `JointDeclarationUploadCutoffDays` -- when JDF reminder fires + auto-cancel
- `PendingAppointmentOverDueNotificationDays` -- pending overdue cadence
- `AutoCancelCutoffTime` -- JDF auto-cancel cutoff

### Daily digest pattern

`AppointmentPendingDailyNotification`: stored proc `spPendingAppointmentNotification` returns HTML-formatted list; emails to `clinicStaffEmail` (config setting).

### Critical OLD behaviors

- **OS-scheduler triggered** -- not in-app cron; external scheduler hits the API.
- **Stored proc heavy** -- every reminder type calls a per-type stored proc that returns the list of recipients + content as JSON or HTML.
- **Per-type single endpoint** -- `postscheduler` switch on ScheduleTypeId.
- **No tracking of sent reminders** beyond the email itself (no "last sent" log -- could result in double-sends if scheduler runs twice).

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/Core/SchedulerDomain.cs` (379 lines) | All 9 reminder type implementations |
| `PatientAppointment.Api/Controllers/.../SchedulerController.cs` | POST /postscheduler endpoint |
| `Models.Enums.ReminderTypes` | Enum with 9 values |
| `Models.Enums.EmailTemplate.PendingAppointmentDailyNotification` etc. | Templates per reminder |
| Stored procs `spm.sp{Reminder}Notification` | DB queries for each type |

## NEW current state

Per `Appointments/CLAUDE.md`:

- `Appointments/Notifications/Jobs/AppointmentDayReminderJob.cs` exists.
- `Appointments/Notifications/Jobs/CancellationRescheduleReminderJob.cs` exists.
- `Appointments/Notifications/Jobs/RequestSchedulingReminderJob.cs` exists.
- `Appointments/Notifications/AppointmentRecipientResolver.cs` + `IAppointmentRecipientResolver.cs` exist.
- `Appointments/Jobs/SendAppointmentEmailJob.cs` exists.
- ABP integrates Hangfire for background jobs.

NEW has the foundation; not all 9 reminder types are necessarily implemented yet.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Hangfire integration | -- | NEW: present (jobs exist) | None | -- |
| Reminder type 1: ApproveRejectInternalUser | OLD | TO VERIFY | **Add `PendingApprovalReminderJob`** -- daily; queries Pending appointments older than configurable threshold | I |
| Reminder type 2: PackageDocumentPending | OLD | TO VERIFY | **Add `PackageDocumentsReminderJob`** -- daily; queries Approved appointments where any package doc is Pending or Rejected; uses `ReminderCutoffTime` | B |
| Reminder type 3: DueDateApproaching | OLD | NEW: `AppointmentDayReminderJob` partial | **Verify covers due-date approaching cadence** | I |
| Reminder type 4: DueDateDocumentApproaching | OLD | TO VERIFY | **Add combined reminder** -- due date approaching + docs still pending | I |
| Reminder type 5: JDFUpload | OLD | TO VERIFY | **Add `JdfReminderJob`** -- AME appointments where JDF not uploaded; uses `JointDeclarationUploadCutoffDays` | I |
| Reminder type 6: AutoCancelled (JDF) | OLD | TO VERIFY | **Add `JdfAutoCancelJob`** -- triggers AppointmentAutoCancelledEto event when cutoff passes | B |
| Reminder type 7: StaffUsers daily | OLD | TO VERIFY | **Add `StaffDailyReminderJob`** -- summary to staff | I |
| Reminder type 8: ResponsibleUser docs | OLD | TO VERIFY | **Add `ResponsibleUserDocReminderJob`** -- alert PrimaryResponsibleUserId | I |
| Reminder type 9: PendingDailyNotification | OLD | TO VERIFY | **Add `PendingAppointmentsDailyDigestJob`** -- daily HTML digest to `clinicStaffEmail` | I |
| Stored procs replaced | -- | -- | **Use LINQ-to-EF** in each job | -- |
| Idempotency / dedup of sent reminders | OLD: none | -- | **Add `LastReminderSentAt` field** on relevant entities OR a `ReminderLog` table to dedup; recommended improvement (strict-parity-friendly: OLD has the gap, NEW closes it) | I |
| Trigger | OLD: external scheduler | NEW: Hangfire `RecurringJob` | None -- Hangfire is in-process | -- |
| Permission: scheduler endpoint | OLD: any caller can hit POST /postscheduler (no auth?) | -- | NEW: no public endpoint; jobs scheduled at startup | -- |

## Internal dependencies surfaced

- All notification templates (covered).
- SystemParameters (covered).

## Branding/theming touchpoints

- Email templates per reminder type.
- Daily digest HTML template (heavy formatting -- patient list, appointment dates).

## Replication notes

### ABP wiring

- Each reminder = a Hangfire `RecurringJob` registered at startup via `IBackgroundJobManager` or `RecurringJob.AddOrUpdate(...)`.
- Cron expressions: most run daily (e.g., 6 AM); JDF auto-cancel runs hourly to catch close-to-cutoff cases promptly.
- Each job:
  1. Query relevant entities via repository.
  2. Build per-recipient list.
  3. Publish per-recipient `*ReminderEto` event.
  4. `*ReminderEmailHandler` + `*ReminderSmsHandler` send the actual notifications.
- Idempotency: track `LastReminderSentAt` field on the entity; skip recipients reminded within 24h.

### Things NOT to port

- External-scheduler trigger -- Hangfire is self-managed.
- Stored procs -- LINQ-to-EF.
- Direct `ISendMail` / `ITwilioSmsService` calls in the job -- decouple via events.
- `clinicStaffEmail` ServerSetting -- per-tenant config or system parameter.

### Verification

1. Pending appointment >7 days old -> staff reminder sent
2. Approved appointment 7 days from due, package docs still pending -> patient reminder
3. AME approved, no JDF, 3 days from due -> attorney reminder; 1 day from due -> auto-cancel
4. Daily digest to clinic staff email -> arrives at scheduled time
5. Reminder rerun within 24h -> deduped (no double email)
