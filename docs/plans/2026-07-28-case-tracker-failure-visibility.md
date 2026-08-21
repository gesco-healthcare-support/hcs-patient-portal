---
feature: Case Tracker failure visibility (integration Part 5)
date: 2026-07-28
status: draft
base-branch: main
related-issues: []
---

## Goal

Make a failed push to the Case Tracker impossible to miss: alert internal staff, show the failures on an
admin screen they can act on, and detect appointments that never produced a push at all.

## Context & decisions

Parts 1, 2+3, 4 and 6 are merged (#393, #395, #397, #398 -> main `5e076d89`). This is the LAST code part
of the epic. Today a dead-lettered push is visible only in the server logs, which nobody reads -- and
contract §I2 calls that the worst failure mode of the integration, because a case silently never reaches
the Case Tracker and NOTHING in either UI shows it.

Resolved decisions (no open questions remain):

- Decision: the screen is HOST-scoped and aggregates every office, with the office as a column, because
  internal staff work on the host surface (`admin.<base>`) -- the same reasoning that made staff notices
  link there -- and a failure someone must chase is the last thing that should have to be hunted for
  office by office. Cost accepted: the list queries N office databases per load via
  `ITenantWorkRunner`.
- Decision: retry RE-ENQUEUES a fresh push from current data via the existing manual-push path and marks
  the dead-lettered row `Resolved`, rather than resetting it to `Pending`. Replaying the stored payload
  would re-send a snapshot taken when it failed, and a row that failed hours ago is exactly the case
  where the appointment has since changed. Marking the old row also keeps the list to things still
  outstanding, which is the point of having a list.
- Decision: alerting is a THROTTLED DIGEST -- at most one email per office per run, listing the affected
  appointments -- not one email per failure as §I2 literally says. The most likely cause of a
  dead-letter is systemic (a wrong token, their service down), which fails every row at once; fifty
  emails would be muted or filtered, and one email saying fifty failed is strictly more useful. The
  contract must be updated to match.
- Decision: include the completeness sweep for approved appointments with NO outbox row. Every other
  safety net assumes a row exists; if the enqueue itself threw and was swallowed (which it is, so a
  staff action never fails), there is no row, no retry, no dead-letter and no alert. That case is
  invisible in both systems today.
- Decision: throttling state lives in a new `AlertedAt` column rather than a time-window calculation,
  because "has this row been alerted?" is a fact about the row and survives restarts, whereas a
  last-run timestamp is extra state that can drift.
- Decision: `Resolved = 4` is added to `IntegrationOutboxStatus`. No migration is needed for the enum
  itself (the column is already an int) but `AlertedAt` IS a new column -- see T2.
- Decision: alerts and the screen carry NO PHI. Appointment id, confirmation number, office, message
  type, target path, attempt count and last error only -- never a patient name. Per §I2.

## All needed context

| Piece | Anchor |
|---|---|
| Where a push becomes terminal | `Domain/Integration/CaseTracker/IntegrationOutboxDrainService.cs:81` -- the single `MarkFatal` call site |
| Outbox entity + its transitions | `Domain/Integration/CaseTracker/IntegrationOutboxItem.cs:34-70` (columns), `:127-190` (`MarkSent`/`MarkFailed`/`MarkFatal`) |
| Status enum | `Domain.Shared/Integration/CaseTracker/IntegrationOutboxStatus.cs` -- `Pending=1, Sent=2, Failed=3` |
| Email dispatch pattern to mirror | `Application/Notifications/Handlers/InternalStaffQueueDigestEmailHandler.cs:78-84` -- `INotificationDispatcher.DispatchAsync(templateCode, recipients, variables, contextTag)` |
| Internal staff resolution + roles | `Domain/Notifications/Jobs/InternalStaffQueueDigestJob.cs:39-42` -- roles are exactly `Staff Supervisor` and `Intake Staff`; `ResolveInternalStaffAsync` is the pattern |
| Host-portal URL for staff links | `IAccountUrlBuilder.BuildPortalRootUrlAsync(null)` -- null means the HOST surface, as used at `InternalStaffQueueDigestEmailHandler.cs:60` |
| Template plumbing (THREE places) | `Domain/NotificationTemplates/` -- a code in `NotificationTemplateConsts.Codes`, a subject in `EmailSubjects.cs` (see `:183,:355`), a body in `NotificationTemplateSeedDefaults.cs`; picked up by `NotificationTemplateDataSeedContributor` |
| Cross-office aggregation | `Domain/MultiTenancy/ITenantWorkRunner.cs` -- `AggregateAcrossOfficesAsync<TResult>` returns one result per office |
| Sweep host to extend | `Domain/Integration/CaseTracker/Jobs/CaseTrackerReconciliationJob.cs` -- already `ForEachOfficeAsync` with per-office try/catch and a batch cap |
| Existing manual push to reuse for retry | `Application/Integration/CaseTracker/CaseTrackerPushAppService.cs` -- already permission-gated |
| Permission pattern | `Application.Contracts/Permissions/CaseEvaluationPermissions.cs` + its provider + `en.json` (Part 1 added `PushToCaseTracker` this way) |
| Angular admin surface | `angular/src/app/admin/` -- `internal-admin-hub.component.ts`, `admin-section.gateway.ts`, `admin-hub.util.ts`. Sections are REGISTERED; read these before adding one |
| Dual-context migration guide | `docs/database/MIGRATION-GUIDE.md` |

Gotchas:

- **`IntegrationOutboxItem` is mapped in BOTH `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext`.**
  A new column therefore needs migrations in BOTH `Migrations/` and `TenantMigrations/`, or office
  databases silently lack it and fail at runtime with a SqlException. This has bitten this repo before.
- EF generates `defaultValue: 0` for new non-nullable enum columns; `AlertedAt` is nullable so this does
  not apply, but do not add a non-nullable one without hand-correcting.
- `ForEachOfficeAsync` aborts the WHOLE run if the delegate throws -- keep the per-office try/catch.
- Hangfire workers have no ambient tenant; `ITenantWorkRunner` handles the scoping, do not assume it.
- `ControllerNameAttribute` comes from `Asp.Versioning`, not ABP. Do NOT add `[IgnoreAntiforgeryToken]`.
- The PHI scanner rejects 8+ consecutive digits; keep fixtures free of long digit runs.
- The email template is DB-SEEDED. The alert will not send until seeding runs, which is a DEPLOY step --
  do not run DbMigrator against any database without asking Adrian.

## Tasks (implementation blueprint)

### T1 - Resolved status, AlertedAt column, and the two transitions

- what: MODIFY `Domain.Shared/Integration/CaseTracker/IntegrationOutboxStatus.cs` adding `Resolved = 4`;
  MODIFY `Domain/Integration/CaseTracker/IntegrationOutboxItem.cs` adding a nullable `AlertedAt` plus
  `MarkAlerted(DateTime nowUtc)` and `MarkResolved(DateTime nowUtc)`. `MarkResolved` must only act on a
  `Failed` row and must be idempotent.
- pattern: the existing `MarkSent` at `:127` -- idempotent, guards on current status
- approach: tdd
- acceptance: WHEN a Failed row is marked resolved, THE SYSTEM SHALL set its status to `Resolved`. IF the
  row is not Failed, THEN THE SYSTEM SHALL leave it unchanged. WHEN a row is marked alerted twice, THE
  SYSTEM SHALL keep the first timestamp. THE SYSTEM SHALL never make a `Resolved` row leasable by a drain.

### T2 - Dual-context EF configuration and migrations

- what: MODIFY both `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext` to map `AlertedAt`;
  generate a migration in BOTH `Migrations/` and `TenantMigrations/`.
- pattern: Part 1's `20260727201528_Added_CaseTrackerIntegrationOutbox` pair; `docs/database/MIGRATION-GUIDE.md`
- approach: code
- acceptance: THE SYSTEM SHALL add the column in both migration sets. THE SYSTEM SHALL apply cleanly to a
  database created by the other set. Migration files SHALL be reviewed by hand before commit.

### T3 - Terminal-failure alert digest

- what: CREATE `Domain/Integration/CaseTracker/Jobs/CaseTrackerFailureAlertJob.cs` (recurring, host):
  per office, find `Failed` rows with `AlertedAt == null`; if any, dispatch ONE email to that office's
  internal staff listing appointment ids, confirmation numbers, message types and last errors; then mark
  each row alerted. Cap the listed rows and say so in the body when truncated.
- pattern: `InternalStaffQueueDigestJob` for staff resolution and the recurring-job shape;
  `CaseTrackerReconciliationJob` for `ForEachOfficeAsync` with per-office try/catch
- approach: tdd
- acceptance: WHEN three rows dead-letter in one office, THE SYSTEM SHALL send exactly ONE email listing
  all three. WHEN the job runs again with no new failures, THE SYSTEM SHALL send nothing. WHEN a fourth
  row then fails, THE SYSTEM SHALL send one email covering only the fourth. IF an office has no internal
  staff, THEN THE SYSTEM SHALL log and continue to the next office without throwing. THE SYSTEM SHALL NOT
  include any patient name, date of birth or document content in the body.

### T4 - Alert email template

- what: MODIFY `Domain/NotificationTemplates/` in three places: a `CaseTrackerPushFailed` code in
  `NotificationTemplateConsts.Codes`, a subject in `EmailSubjects.cs`, and a body in
  `NotificationTemplateSeedDefaults.cs`. Body variables: office name, failure count, a table of
  appointment/confirmation/error, and the host portal URL from `BuildPortalRootUrlAsync(null)`.
- pattern: `EmailSubjects.cs:183,355` for the code-to-subject wiring
- approach: code
- acceptance: THE SYSTEM SHALL render with no PHI beyond appointment id and confirmation number. THE
  SYSTEM SHALL link to the host portal, not an office subdomain.

### T5 - Completeness sweep for appointments with no outbox row

- what: MODIFY `Jobs/CaseTrackerReconciliationJob.cs` adding a pass that finds appointments in a published
  state (per `CaseTrackerPublishPolicy`) with NO `IntegrationOutboxItem` of type `Intake`, enqueues an
  intake via `CaseTrackerIntakeQueue`, and logs each one at warning level. Reuse the existing per-office
  batch cap and log truncation.
- pattern: the existing `ReleaseStalledPacketSetsAsync` in the same file -- same cap, log and try/catch shape
- approach: tdd
- acceptance: WHEN an approved appointment has no intake row, THE SYSTEM SHALL enqueue one and log it.
  WHEN every approved appointment already has a row, THE SYSTEM SHALL enqueue nothing. WHILE an
  appointment is unpublished, THE SYSTEM SHALL ignore it. THE SYSTEM SHALL cap the work per office per
  sweep and log when it truncates.

### T6 - Dead-letter query and retry app service

- what: CREATE `Application.Contracts/Integration/CaseTracker/ICaseTrackerDeadLetterAppService.cs` and its
  implementation: `GetListAsync` aggregating `Failed` rows across offices via
  `AggregateAcrossOfficesAsync` (returning office id and name alongside each row), and
  `RetryAsync(Guid officeId, Guid rowId)` which re-enqueues a fresh intake through the existing manual
  push path and marks the original row `Resolved`. Both gated by the new permission.
- pattern: `CaseTrackerPushAppService` for the permission-gated shape and the reuse of the queue
- approach: tdd
- acceptance: THE SYSTEM SHALL return failures from every office with the office identified. WHEN retry is
  called, THE SYSTEM SHALL enqueue a push built from CURRENT appointment data and mark the original row
  Resolved. IF the original row is not Failed, THEN THE SYSTEM SHALL reject the retry. WHERE the caller
  lacks the permission, THE SYSTEM SHALL refuse both operations.

### T7 - Permission

- what: MODIFY `Application.Contracts/Permissions/CaseEvaluationPermissions.cs` adding
  `ViewIntegrationDeadLetters` under the integration group, its provider registration, and the `en.json`
  display name.
- pattern: Part 1's `PushToCaseTracker`
- approach: code
- acceptance: THE SYSTEM SHALL define the permission with a localised name and SHALL grant it to no role
  by default.

### T8 - Admin dead-letter screen

- what: CREATE an Angular admin section listing failed pushes with office, appointment, confirmation
  number, message type, target, attempt count and last error, plus a Retry action per row and a refresh.
  FIRST read `angular/src/app/admin/admin-section.gateway.ts` and `admin-hub.util.ts` and register the
  section the way existing sections are registered rather than inventing a route.
- pattern: `internal-admin-hub.component.ts` and the existing registered sections
- approach: test-after
- acceptance: WHERE the user lacks `ViewIntegrationDeadLetters`, THE SYSTEM SHALL not show the section.
  WHEN Retry succeeds, THE SYSTEM SHALL remove the row from the list without a full page reload. WHEN
  there are no failures, THE SYSTEM SHALL show an explicit empty state rather than a blank table. THE
  SYSTEM SHALL display no patient name anywhere on the screen.

### T9 - Tests

- what: CREATE `test/.../Domain.Tests/Integration/CaseTracker/` tests for the two new transitions, the
  alert digest (including the alerted-once guard and the no-staff case) and the completeness sweep;
  CREATE an Application test for the retry path including the not-Failed rejection. Synthetic data only.
- pattern: `IntegrationOutboxItemTests`, `IntegrationOutboxDrainServiceTests`
- approach: tdd
- acceptance: The system shall cover Resolved and Alerted transitions, one-email-per-batch, no-new-failure
  silence, missing-row detection, the retry happy path and its rejection, and the absence of PHI in the
  rendered alert variables.

### T10 - Contract and docs

- what: MODIFY `docs/integration/case-tracker-api-contract.md`: update §I2 to say alerting is a throttled
  digest rather than one email per failure, record the completeness sweep, and move failure visibility out
  of §J's not-built list. ALSO fix the stale `Delete propagation (still needed; TO BE BUILT)` heading at
  §C -- that shipped in #395.
- approach: code
- acceptance: THE SYSTEM SHALL describe the digest behaviour actually built, and §J SHALL list nothing
  that is built.

## Validation loop

From the repo root, in order:

```bash
dotnet format HealthcareSupport.CaseEvaluation.slnx --verify-no-changes
```
```bash
dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
```
```bash
dotnet test HealthcareSupport.CaseEvaluation.slnx
```
```bash
python .claude/scripts/verify_structure.py
```
```bash
cd angular && npx ng build --configuration development
```

Done-bar: all five green (structure check 0 FAIL), no fixture contains real-looking patient data, and both
migration files reviewed by hand. The EF test project takes 8-10 minutes; that is normal. The Angular build
is new to this epic's validation loop because this is its first front-end change.

## Risk / rollback

Blast radius: the largest of the epic. Unlike Parts 2-6 this one adds a MIGRATION, a recurring job that
sends email, and a net-new front-end surface.

- The migration touches a table Parts 1-6 already write. It is an additive nullable column, so an old
  binary against a new schema still works, but it must exist in BOTH context sets or office databases
  break at runtime.
- The alert job sends email. A bug here mails internal staff repeatedly. The `AlertedAt` marker is the
  guard, and its once-only behaviour is covered by test.
- The completeness sweep ENQUEUES pushes. If its "has no row" query is wrong it could enqueue duplicates
  for every approved appointment in every office. The idempotency key makes a duplicate collapse rather
  than double-send, which is the safety net, but the query still needs review.
- Nothing reaches the Case Tracker while `CaseTrackerPushEnabled` is false, and the alert job has nothing
  to alert on until pushes actually run.

Rollback: revert the PR, then revert the migration in both contexts if it has been applied. The column is
nullable and unused by older code, so leaving it in place is also safe.
