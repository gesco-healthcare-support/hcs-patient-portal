# Background Jobs

> Purpose: reference for every recurring background job, what it does, when it runs, and the
> switches that stop it. Audience: whoever operates the deployed stack.

[Home](../INDEX.md) > [DevOps](./) > Background Jobs

## Read this first

**Every cron expression below is PACIFIC time, not UTC.** Registration passes
`new RecurringJobOptions { TimeZone = PacificTime.Zone }`, so `0 7 * * *` means 07:00 Pacific.
Reading these as UTC will put every schedule seven or eight hours out depending on daylight
saving, and the error is silent.

**Two job IDs do not describe what the job does.** Hangfire keys a persisted recurring
registration by its id string, so renaming the id would orphan the existing entry rather than
rename it. Both kept their original ids deliberately:

- `appt-jdf-auto-cancel` **does not cancel anything.** It was `JointDeclarationAutoCancelJob`
  and was renamed to `JointDeclarationOverdueJob` on 2026-08-08 when it stopped cancelling. It
  now flags overdue joint declarations.
- `appt-duedate-approaching` is **one consolidated reminder job**, not just a due-date warning.
  Group F (2026-06-09) replaced three jobs (DueDateApproaching, DueDateDocumentIncomplete,
  PackageDocumentReminder) with a single job that emits one combined due-date and
  outstanding-documents email per appointment. It kept the old id so the existing entry updated
  in place; the two retired ids are explicitly purged so they stop invoking deleted types.

Registration lives in one method: `CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs()`.
That method is the authority for what is registered; the constants live on each job class.

## The jobs

Ordered by how often they run, because that is how you read this page during an incident.

| Job id | Cron (Pacific) | Class | What it does |
| --- | --- | --- | --- |
| `case-tracker-drain-kick` | `*/5 * * * *` | CaseTrackerDrainKickJob | Kicks the Case Tracker outbox drain |
| `case-tracker-feed-health` | `*/5 * * * *` | CaseTrackerFeedHealthJob | Watches the changes-feed delivery mode |
| `case-tracker-failure-alert` | `*/15 * * * *` | CaseTrackerFailureAlertJob | Alerts on integration failures |
| `case-tracker-reconciliation` | `*/15 * * * *` | CaseTrackerReconciliationJob | Reconciles portal and Case Tracker state |
| `approval-reconciliation` | `*/15 * * * *` | ApprovalReconciliationJob | Reconciles approval state |
| `case-tracker-completeness-sweep` | `0 * * * *` | CaseTrackerCompletenessSweepJob | Hourly completeness check |
| `change-request-consent-expiry-sweep` | `30 * * * *` | ChangeRequestConsentExpirySweepJob | Expires consent tokens past their TTL |
| `appt-draft-cleanup` | `0 3 * * *` | DraftCleanupJob | Removes abandoned booking drafts |
| `appt-jdf-auto-cancel` | `0 6 * * *` | JointDeclarationOverdueJob | Flags overdue joint declarations (does NOT cancel) |
| `appt-day-reminder` | `0 7 * * *` | AppointmentDayReminderJob | Day-of-appointment reminders |
| `appt-cancellation-reschedule-reminder` | `0 8 * * *` | CancellationRescheduleReminderJob | Chases open change requests |
| `appt-request-scheduling-reminder` | `0 8 * * *` | RequestSchedulingReminderJob | Chases unscheduled requests |
| `appt-duedate-approaching` | `15 8 * * *` | AppointmentReminderJob | Consolidated due-date + outstanding-documents email |
| `appt-pending-daily-digest` | `0 9 * * *` | PendingDailyDigestJob | Digest to the intake-staff inbox |
| `appt-internal-staff-queue-digest` | `15 9 * * *` | InternalStaffQueueDigestJob | Per-staff queue counts |
| `case-tracker-missing-intake-report` | `0 8 * * 1` | CaseTrackerMissingIntakeReportJob | Weekly, Mondays |

The ordering of the morning jobs is deliberate: the JDF overdue sweep runs at 06:00, ahead of
the 07:00 day reminders, so staff see the flag before that day's reminders go out.

## The two switches that stop mail and integration

These are the levers to reach for first when something is sending or pushing when it should not.
Both are read per run in the office's own tenant scope, so a per-office override beats the host
default and a change takes effect on the next pass without a restart.

| Setting | Effect when off |
| --- | --- |
| `CaseEvaluationSettings.Notifications.EmailEnabled` | The email outbox drain claims nothing. Due rows stay `Pending` and resume when re-enabled, with **no failed-attempt cost** |
| `CaseEvaluationSettings.Integration.CaseTrackerPushEnabled` | The Case Tracker drain sends nothing. Rows stay `Pending` the same way |

**Holding is not failing.** Neither switch burns a retry attempt, so turning mail off for an hour
does not push anything toward its dead-letter cap. That is the important property: it is safe to
stop delivery while investigating.

## The two outboxes

Both follow the same shape: claim a row under a lease, do the work with no transaction held
across it, then record the outcome. A crash between doing and recording leaves the row leased
and Pending, and lease expiry hands it to a later pass, so a row is never lost and at most
re-sent once.

### Email outbox

| Constant | Value | Meaning |
| --- | --- | --- |
| `DefaultMaxAttempts` | 5 | Attempts before a row dead-letters |
| `LeaseDurationSeconds` | 120 | Visibility timeout on a claimed row |
| `RetryBackoffSeconds` | 300 | Wait before a failed row is retried |
| `DrainBatchSize` | 50 | Rows claimed per office per pass |

A send failure never aborts the batch: the exception is caught per row deliberately, so one bad
recipient cannot stall everyone else's mail. Idempotency is a SHA-256 key, so a logical send
collapses to one row.

### Case Tracker outbox

| Constant | Value | Meaning |
| --- | --- | --- |
| `MaxAttempts` | 100 | Attempts before dead-letter |
| `RetryWindowHours` | 24 | Retry window |
| `SteadyRetryWaitMinutes` | 30 | Steady-state wait between retries |
| `EarlyWarningAfterAttempts` | 2 | When the failure alert starts caring |
| `LeaseDurationSeconds` | 120 | Visibility timeout |
| `DrainBatchSize` | 50 | Rows per pass |
| `AppointmentLockTimeoutMilliseconds` | 30,000 | `sp_getapplock` timeout |
| `VolumeThresholdPerWindow` / `VolumeWindowMinutes` | 100 per 60 min | Volume guard |

Three things about this one are worth knowing before you debug it.

**Push versus feed.** An office switched to the changes feed is delivered by the Case Tracker
PULLING, and the drain then sends nothing for that office. This is checked once per pass AND
again before every single row is claimed, so a feed switched on mid-pass stops the pass at the
next row. At most the one row already in flight is pushed after the switch: delivered once by
push, or served once by the feed, never both and never lost. **If an office appears to have
stopped pushing, check the delivery mode before looking at the outbox.**

**The volume guard is a business limit, not a rate limit.** 100 sends per rolling 60 minutes,
measured from `SentAt`, deliberately not a trip flag so it clears itself as the window slides.
The reason is that each intake becomes a CASE their staff must handle, so flooding them is a
business problem rather than a technical one. Hitting it logs a warning, not an error, because
hitting it on a large backlog is legitimate.

**One short transaction per row, none across the HTTP call.** Changed 2026-09-23 after the
previous single-transaction-per-pass behaviour held row locks for about 25 minutes during a slow
outage, blocking the per-appointment lookups behind staff actions, holding back the feed
position, and re-sending the whole batch after a crash. A caller must NOT wrap this service in
its own unit of work, or that behaviour returns.

Fatal outcomes (a bad token, a malformed request) dead-letter immediately rather than burning
the 24-hour window, so a human sees them in minutes.

## How a job reaches every office

`TenantWorkRunner` reads the office id list from the tenant registry in HOST context, so the
tenant filter does not hide rows, then runs the work inside `ICurrentTenant.Change(officeId)`
for each. It mirrors the per-office loop in `CaseEvaluationDbMigrationService`, which is the
pattern proven to route repository calls to each office's own database.

**Trap:** `ICurrentTenant.Change(id)` sets the id and leaves `Name` null. Anything inside the
delegate that needs the office NAME (a UI column, an outbound email) must resolve it from the
store. This has already produced a blank office name in both a UI column and an email.

## Hangfire itself

SQL Server storage, 6 workers, 5-attempt retry to dead letter. Dashboard and health endpoints at
`/health-status` and `/health-ui`. Configured in `CaseEvaluationHttpApiHostModule`.

`DbMigrator` explicitly disables background-job execution: it is a one-shot console host and must
never drain the queue.

## Source reference

- Registration: `src/.../HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`,
  `ConfigureHangfireRecurringJobs()`
- Email outbox: `src/.../Domain/Notifications/Outbox/`
- Case Tracker outbox: `src/.../Domain/Integration/CaseTracker/`
- Per-office iteration: `src/.../Domain/MultiTenancy/TenantWorkRunner.cs`
- Settings: `src/.../Domain/Settings/CaseEvaluationSettings.cs`
