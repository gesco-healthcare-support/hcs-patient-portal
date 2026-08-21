---
status: draft
date: 2026-07-29
---

# Sweep lookback window + per-office push toggle

## Goal

Make the first live contact with the Case Tracker (their redeploy is 2026-07-30) safe to attempt.

Two things block it, both found while answering Levon's questions:

1. **The completeness sweep backfills all history.** `FindAppointmentsWithoutIntakeAsync` has no date
   floor, so every appointment predating the integration gets an intake row at 50/office/hour. They
   sit `Pending` while push is off, then flush the moment an office is enabled -- sending that
   office's entire appointment history, including long-cancelled appointments, as fresh intakes that
   their side turns into cases.
2. **There is no way to enable one office.** The setting is per-office and correct, but nothing in
   Angular references it and no app service exposes it, so flipping it means raw SQL into
   `AbpSettings` in a production tenant database, against a Redis-cached read.

## Context

- Sweep: `CaseTrackerCompletenessSweepJob.cs:132`. Filters on absence of an Intake row and
  `CaseTrackerPublishPolicy.IsPublished`, nothing else. `IsPublished` excludes only Pending /
  Rejected / InfoRequested, so Cancelled and Completed history counts as publishable.
- The job's own doc comment states its purpose is catching an enqueue that THREW -- a
  minutes-to-hours-old event. Auditing all history was never the intent; a window matches the intent
  and removes the hazard.
- Setting definition: `CaseEvaluationSettingDefinitionProvider.cs:81`, default `"false"`.
  Read per drain in office scope at `IntegrationOutboxDrainService.cs:52`.
- Pattern to mirror for read/write: `SystemParametersAppService.cs:59-65` and `:100-107` use
  `ISettingManager.GetOrNullForCurrentTenantAsync` / `SetForCurrentTenantAsync`.
- Host-scoped cross-office aggregation pattern: `CaseTrackerDeadLetterAppService.GetListAsync` via
  `ITenantWorkRunner.AggregateAcrossOfficesAsync`, office names from `ITenantStore.FindAsync`.
- Existing UI section: `angular/src/app/admin/integration-failures.component.ts`, already wired into
  the admin hub, host-scoped, `RestService` with literal URLs.

## Decisions taken

**Window = 7 days, on `LastModificationTime ?? CreationTime`.** Approval stamps
`LastModificationTime`, so a recently-approved appointment qualifies while untouched history does
not. Seven days is generous for an hourly job whose target is a same-day lost enqueue.

**Reuse the `PushToCaseTracker` permission rather than adding one.** The dead-letter retry already
uses it (`CaseTrackerDeadLetterAppService.cs:120`), so the account that can retry a push can also
enable an office -- both are "cause the portal to send ePHI to the Case Tracker". A NEW permission
would need granting, and the IT Admin role is locked in the UI, so the control could be invisible
during a live test. Accepted trade-off: slightly coarser than least-privilege, on a host-only
internal-staff screen.

**Write inside `ICurrentTenant.Change(officeId)` + `SetForCurrentTenantAsync`, NOT
`SetForTenantAsync`.** Under database-per-office the setting store follows the current tenant's
connection. Entering the office scope makes the write symmetric with how the drain reads it, which is
what makes it correct; `SetForTenantAsync` alone may write the row to the host database where the
drain would never see it.

**Surface the pending count next to each toggle.** Directly answers "is this office safe to enable?"
-- it is the guard rail for hazard 1 above, not a nice-to-have. Same office loop, one extra count.

## Tasks

### T1 -- sweep lookback window (approach: code)

`CaseTrackerCompletenessSweepJob`: add `public const int LookbackDays = 7;`, inject `IClock`, pass a
cutoff into `FindAppointmentsWithoutIntakeAsync`, and add
`.Where(a => (a.LastModificationTime ?? a.CreationTime) >= cutoffUtc)` to the queryable chain. Log
the window alongside the counts so a narrowed pass is never silent.

### T2 -- sweep tests (approach: test-after)

`CaseTrackerCompletenessSweepJobTests`: existing fixtures do not set `CreationTime`, so they default
to `0001-01-01` and every current test would start failing -- set it explicitly. ADD: an old
published appointment with no row is IGNORED; a recent one is still enqueued.

### T3 -- contracts (approach: code)

`ICaseTrackerPushSettingsAppService` with `GetOfficesAsync()` and
`SetPushEnabledAsync(Guid officeId, bool enabled)`; `CaseTrackerOfficePushStateDto` carrying
`OfficeId`, `OfficeName`, `PushEnabled`, `PendingCount`.

### T4 -- app service (approach: code)

`CaseTrackerPushSettingsAppService`, `[Authorize(PushToCaseTracker)]` on both methods. Read via
`AggregateAcrossOfficesAsync`; names from `ITenantStore.FindAsync`; write inside
`_currentTenant.Change(officeId)`.

### T5 -- controller (approach: code)

`CaseTrackerOfficesController` in HttpApi, `[Route("api/app/case-tracker")]`, `GET offices` and
`PUT offices/{officeId}/push`. Mirrors `CaseTrackerDeadLetterController`.

### T6 -- UI (approach: test-after)

Extend `integration-failures.component.ts` with an offices table above the failures table: office
name, pending count, and an enable/disable control. Deliberately NOT a new admin-hub section --
that would mean touching `admin-hub.util.ts`, the else-terminated dispatch chain in
`internal-admin-hub.component.ts`, and `admin-hub.util.spec.ts` which pins the section list. One
component change is the smaller, lower-risk edit.

Show the pending count as a warning when non-zero, since enabling flushes it.

## Acceptance (EARS)

- WHEN the sweep runs, THE SYSTEM SHALL enqueue intake rows only for published appointments changed
  within `LookbackDays`, and SHALL leave older appointments without a row untouched.
- WHEN the sweep completes, THE SYSTEM SHALL log the lookback window it applied.
- WHEN a user with `PushToCaseTracker` opens the Case Tracker admin screen, THE SYSTEM SHALL list
  every office with its current push state and its count of pending outbox rows.
- WHEN that user enables an office, THE SYSTEM SHALL persist the setting in that office's scope such
  that the next drain in that office reads it as true.
- WHEN a user without `PushToCaseTracker` calls either endpoint, THE SYSTEM SHALL reject the request.

## Validation loop

    dotnet format HealthcareSupport.CaseEvaluation.slnx --verify-no-changes
    dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
    dotnet test HealthcareSupport.CaseEvaluation.slnx
    python .claude/scripts/verify_structure.py
    cd angular && npx ng build --configuration development
    cd angular && npx ng test --watch=false --browsers=ChromeHeadless --include='**/admin/**/*.spec.ts'

Then LIVE on the docker stack: toggle an office on, confirm the drain reads it (the disabled-hold log
line stops), toggle it off again.

## Out of scope

- No health-check caller for their `GET /api/intake/health`; a `curl` from the box covers Levon's
  step 2 and needs no code.
- No bulk retry on the dead-letter screen. Worth doing if the first contact goes badly, not before.
- No TLS work -- their answer is http for testing with synthetic data only, https before real data,
  and no client certificates.
