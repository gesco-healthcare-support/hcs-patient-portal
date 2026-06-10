---
feature: reminder-consolidation
date: 2026-06-09
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Replace the three overlapping due-date / document reminder jobs with ONE
anchor-based per-appointment reminder email (To the booker, CC the other
parties + the office; lists outstanding required documents by label, JDF
folded in), and add a per-row "Decision due" date (request + 5 days) to the
daily pending digest.

## Context

Group F of the 2026-06-09 email review. Today an Approved appointment with
outstanding documents can receive up to FIVE reminder emails on the same day
(1 due-date + 1 incomplete + N package-doc, one per document), and the
package-doc job re-fires EVERY day inside its window. Adrian wants one
non-redundant reminder.

Confirmed source facts (all read this session, HIGH confidence):

- Three cron jobs produce the F3/F4/F5/F6 emails. `DueDateApproachingJob`
  (08:15, anchors 14/7/3, no docs), `DueDateDocumentIncompleteJob` (08:45,
  anchor 7 + docs), `PackageDocumentReminderJob` (08:30, Approved + at/past
  cutoff, one event PER document, daily). F5 (JDF) is NOT a separate job --
  Group L folded it into the package job via the `IsJointDeclaration` flag.
- Reusable: the Group C ex-parte addressing
  (`StatusChangeEmailHandler.DispatchToBookerWithCcAsync` + `PartitionToBookerCc`
  + `INotificationDispatcher.DispatchToWithCcAsync`); `DocumentEmailContext`
  already carries `BookerEmail`/`BookerFullName`/`DueDate`;
  `MissingRequiredDocumentsResolver.ResolveAsync(apptId)` returns
  `MissingRequiredDocument(Id, Name, State)` = required-by-active-package but
  not Accepted.
- `Appointment : FullAuditedAggregateRoot<Guid>` -> has `CreationTime` (the
  booking timestamp) for the digest "Decision due" column.
- The JDF is stored as a standalone `AppointmentDocument`
  (`IsJointDeclaration=true`, no `SourceDocumentId`), so it is NOT in the
  package model and `MissingRequiredDocumentsResolver` will NOT surface it --
  it needs a separate append.

Locked decisions (Adrian, this session):

- Cadence: anchor-based, fire ONCE per anchor (reuse `DueDateApproachingAnchors`,
  default 14/7/3). No daily repeats.
- Outstanding-docs source: `MissingRequiredDocumentsResolver` (required-but-not-
  accepted, by name/label).
- A near-due appointment with NO outstanding docs STILL gets the due-date
  nudge (docs are additive content).
- JDF is just another missing document, named by its label -- no separate
  template.
- F1 decision-due interval = request date + 5 days.

## Approach

**Chosen: one anchor-based job, one handler, one template.** Keep the existing
`DueDateApproachingJob`'s anchor-based per-appointment scan (it already fires
for every active appointment on the configured anchors with no doc gate -- the
exact "nudge even when no docs" behavior wanted), and move ALL document logic
into the (Application-layer) handler, which the Domain job cannot do. Delete
the other two jobs + their events + handlers. The handler resolves the booker +
parties, gathers outstanding docs (Approved appointments only -- see rule
below) via `MissingRequiredDocumentsResolver`, appends the JDF when one is
required-but-not-accepted, and sends ONE consolidated email To the booker with
parties + office CC'd, using the surviving `AppointmentDueDateReminder` template.

Rename the job/event/handler to reveal intent (`AppointmentReminderJob` /
`AppointmentReminderEto` / `AppointmentReminderEmailHandler`) but KEEP
`RecurringJobId = "appt-duedate-approaching"` stable so Hangfire does not orphan
the recurring entry. Class renames are compile-checked; only the RecurringJobId
string matters to Hangfire.

**Design rules:**

- R1 (Adrian 2026-06-09): The outstanding-docs list is included for ALL
  eligible statuses -- `Pending`, `Approved`, AND `RescheduleRequested`. A
  Pending appointment lists its full required set (nothing Accepted yet) as
  outstanding; that is the intended "remaining documents" reminder.
  `MissingRequiredDocumentsResolver` derives "required" from the active package
  template (not from queued rows), so it works the same for every status.
- R2: The consolidated email is addressed To the booker, CC parties + office --
  same ex-parte rule as Group C status emails.
- R3: One email per appointment per anchor day. No per-document fan-out, no
  daily repeats.

Rejected alternatives:

- *Daily-while-in-window* cadence (rejected by Adrian) -- still a daily email
  per appointment.
- *Keep three jobs, only merge templates* -- does not remove the multi-email
  redundancy, which is the core ask.
- *Replicate the booker/CC helper in the new handler* (vs extract) -- duplicates
  the ex-parte addressing knowledge across two handlers. Extracting to a shared
  collaborator is preferred (T2).

## Tasks

- T1: Daily-digest "Decision due" column.
  - Add `DateTime RequestedAt` to `PendingDailyDigestRow`; project
    `a.CreationTime` in `PendingDailyDigestJob.ProcessTenantAsync` and set it on
    the row. In `PendingDailyDigestEmailHandler.BuildDigestHtml` add a "Decision
    due" column = `RequestedAt + 5 days` (`private const int DecisionDueDays = 5;`),
    formatted `MM/dd/yyyy`. Greeting already "Hello Staff,".
  - approach: code
  - files-touched: [Domain.Shared/Notifications/Events/PendingDailyDigestEto.cs,
    Domain/Notifications/Jobs/PendingDailyDigestJob.cs,
    Application/Notifications/Handlers/PendingDailyDigestEmailHandler.cs]
  - acceptance: digest table renders a 5th "Decision due" column showing
    booking-date + 5 days for each pending row; build clean.

- T2: Extract the booker/CC dispatch into a shared collaborator.
  - New `Application/Notifications/BookerCcDispatcher.cs` (`ITransientDependency`)
    holding `PartitionToBookerCc` + the dispatch-To-booker-with-CC logic
    (CcRecipientAppender + `DispatchToWithCcAsync`). Refactor
    `StatusChangeEmailHandler` to delegate to it (behavior-preserving).
  - approach: code
  - files-touched: [Application/Notifications/BookerCcDispatcher.cs (new),
    Application/Notifications/Handlers/StatusChangeEmailHandler.cs]
  - acceptance: build clean; StatusChangeEmailHandler still sends the same
    To/CC partition (no behavioral change to the C-group emails).

- T3: Consolidated reminder handler.
  - Rename `DueDateApproachingEmailHandler` -> `AppointmentReminderEmailHandler`
    and rewrite: resolve `ctx` + parties; call `MissingRequiredDocumentsResolver`
    (for ALL eligible statuses per R1) and build an outstanding-docs HTML block
    by label (+ state); append a "Joint Declaration Form" line when a JDF is
    required-but-not-accepted (lift the "JDF required" predicate from
    `JointDeclarationAutoCancelJob` / the JDF upload gate -- read in build);
    inject an `##OutstandingDocuments##` block (empty when none) plus
    `##BookerFullName##`, `##DueDate##`, `##AppointmentRequestConfirmationNumber##`,
    `##PortalUrl##`; dispatch via the T2 `BookerCcDispatcher` to the
    `AppointmentDueDateReminder` template.
  - approach: code
  - files-touched: [Application/Notifications/Handlers/AppointmentReminderEmailHandler.cs
    (renamed from DueDateApproachingEmailHandler)]
  - acceptance: an appointment on an anchor day with outstanding docs (Pending
    or Approved) produces ONE email To booker, CC parties+office, listing the
    docs by label (JDF included); a no-docs appointment produces the bare
    due-date nudge; build clean.

- T4: Rename the job + event; delete the two redundant jobs/handlers/events.
  - Rename `DueDateApproachingJob` -> `AppointmentReminderJob` and
    `DueDateApproachingEto` -> `AppointmentReminderEto` (keep
    `RecurringJobId = "appt-duedate-approaching"`). Delete
    `DueDateDocumentIncompleteJob`, `PackageDocumentReminderJob`,
    `DueDateDocumentIncompleteEmailHandler`, `PackageDocumentReminderEmailHandler`,
    `DueDateDocumentIncompleteEto`, `PackageDocumentReminderEto`.
  - approach: code
  - files-touched: [Domain/Notifications/Jobs/AppointmentReminderJob.cs (renamed),
    Domain/Notifications/Jobs/DueDateDocumentIncompleteJob.cs (delete),
    Domain/Notifications/Jobs/PackageDocumentReminderJob.cs (delete),
    Domain.Shared/Notifications/Events/AppointmentReminderEto.cs (renamed),
    Domain.Shared/Notifications/Events/DueDateDocumentIncompleteEto.cs (delete),
    Domain.Shared/Notifications/Events/PackageDocumentReminderEto.cs (delete),
    Application/Notifications/Handlers/DueDateDocumentIncompleteEmailHandler.cs (delete),
    Application/Notifications/Handlers/PackageDocumentReminderEmailHandler.cs (delete)]
  - acceptance: solution builds with no references to the deleted types. Also
    deletes the 4 obsolete tests for the removed jobs/handler
    (DueDateApproachingJobTests, DueDateDocumentIncompleteJobTests,
    PackageDocumentReminderJobTests, PackageDocumentReminderEmailHandlerTests)
    so the test project still compiles. Dormant leftovers intentionally kept
    (harmless): the unused `NotificationKind` values and the
    `DueDateDocumentIncompleteAnchors` / `PackageDocumentReminderDays` setting
    consts + definitions.

- T5: Hangfire registration cleanup.
  - In `CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs()`:
    update the renamed job's `AddOrUpdate` to the new type (RecurringJobId
    unchanged); remove the `AddOrUpdate` calls for the two deleted jobs; add
    `RecurringJob.RemoveIfExists("appt-duedate-document-incomplete")` and
    `RemoveIfExists("appt-package-doc-reminder")` so their stale recurring
    entries are purged from the Hangfire store on startup.
  - approach: code
  - files-touched: [HttpApi.Host/CaseEvaluationHttpApiHostModule.cs]
  - acceptance: build clean; only one reminder recurring job registered; the two
    old job IDs are removed.

- T6: Template surface.
  - Rewrite `EmailBodies/AppointmentDueDateReminder.html` into the consolidated
    body (greet `##BookerFullName##`, due date, `##OutstandingDocuments##`
    block, portal CTA). Delete `AppointmentDocumentIncomplete.html`,
    `UploadPendingDocuments.html`, `JointDeclarationUploadReminder.html`. Remove
    the `AppointmentDocumentIncomplete`, `UploadPendingDocuments`,
    `JointDeclarationUploadReminder` consts from `NotificationTemplateConsts.Codes`
    AND from `Codes.All`; remove their `EmailSubjects.ByCode` entries; refresh
    the `AppointmentDueDateReminder` subject for the consolidated content.
  - approach: code
  - files-touched: [Domain/NotificationTemplates/EmailBodies/AppointmentDueDateReminder.html,
    Domain/NotificationTemplates/EmailBodies/AppointmentDocumentIncomplete.html (delete),
    Domain/NotificationTemplates/EmailBodies/UploadPendingDocuments.html (delete),
    Domain/NotificationTemplates/EmailBodies/JointDeclarationUploadReminder.html (delete),
    Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs,
    Domain/NotificationTemplates/EmailSubjects.cs]
  - acceptance: build clean; `Codes.All` no longer contains the 3 retired codes;
    no handler references them.

- T7: Fix the stale template-count test.
  - Update `NotificationTemplatesValidatorUnitTests.cs:208`
    `Codes.All.Length.ShouldBe(64)` to the actual post-removal count (current
    count minus 3; verify by counting `Codes.All`).
  - approach: code
  - files-touched: [test/.../Application.Tests/NotificationTemplates/NotificationTemplatesValidatorUnitTests.cs]
  - acceptance: the count assertion matches `Codes.All.Length`.

## Risk / Rollback

- Blast radius: the reminder email subsystem only (3 cron jobs -> 1) + the daily
  digest body + 3 retired template codes. No change to booking, approval,
  status-change, or document-upload flows. T2 touches `StatusChangeEmailHandler`
  but is behavior-preserving.
- Reverses Group L's "distinct JDF reminder template" (G-05-02 Option B) -- by
  design (Adrian: JDF becomes a line item). The JDF AUTO-CANCEL job
  (`JointDeclarationAutoCancelJob`, 06:00) is untouched.
- Hangfire: the two removed recurring jobs are explicitly purged via
  `RemoveIfExists` (T5); without it they would linger in the store and keep
  firing the now-deleted job types.
- Rollback: revert the commit; re-add the two `AddOrUpdate` calls. The retired
  DB template rows persist (seeder simply stops touching them); restoring the
  codes to `Codes.All` re-seeds them.

## Verification

After all tasks: `dotnet build` clean; deploy via `docker compose up -d --build`
(check `docker compose ps` first for an in-progress build; confirm the compile
actually RAN -- this host caches COPY/publish/ng-build, so watch the build log
or use `--no-cache`; never pipe build output through `| tail`). Then:

1. db-migrator exit 0 (no schema change expected -- no migration in this plan),
   api + authserver healthy, 0 startup errors.
2. Trigger the renamed reminder job from `/hangfire` against a seeded
   appointment (Pending or Approved) near an anchor with >=1 outstanding
   required doc: confirm exactly ONE email is enqueued, To the booker, CC
   parties + office, listing the docs by label. Confirm a no-docs appointment
   yields the bare due-date nudge.
3. Confirm the two old recurring jobs are gone from `/hangfire`.
4. Trigger the pending-daily-digest job: confirm the digest shows the "Decision
   due" column = booking date + 5 days per row.
5. (If JDF predicate wired) confirm an AME appointment missing its JDF lists
   "Joint Declaration Form" among the outstanding docs.
