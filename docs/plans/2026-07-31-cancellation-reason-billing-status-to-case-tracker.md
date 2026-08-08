---
feature: Send cancellation reason + explicit billing status to the Case Tracker
date: 2026-07-31
status: draft
base-branch: main
related-issues: []
---

# Phase 2: cancellation reason + billing status to the Case Tracker

Phase 2 of `2026-07-31-reschedule-cancel-calendar-integration-epic.md`. Independent of
phases 1 and 3.

DO NOT START BUILDING: Adrian is running another session in this worktree and will give an
explicit go. See "Concurrency" below -- three of the files here are being edited right now.

## Goal

A cancelled appointment reaches the Case Tracker carrying the cancellation reason and an
explicit billing status, and `Appointment.CancellationReason` stops being a dead column so the
patient cancellation email stops rendering a blank reason.

## Context & decisions

Already working, so NOT in scope: cancellation is already pushed at both stages
(`CancellationRequested`, then `CancelledNoBill` / `CancelledLate`) because
`CaseTrackerPublishPolicy.IsPublished` is a deny-list that excludes only Pending / Rejected /
InfoRequested, and any status change on a published appointment re-pushes via
`AppointmentChangedHandler`. The identifiers Adrian asked for are already in the payload:
`appointmentId` (the dedup key), `confirmationNumber`, `tenant.tenantId`, `tenant.facilityId`.

Root cause this phase fixes (VERIFIED by grep, not assumed): `Appointment.CancellationReason`
and `Appointment.ReScheduleReason` are NEVER assigned by any code -- `grep -rnE
"\.(CancellationReason|ReScheduleReason)\s*="` over `src` returns nothing. Both columns are
permanently NULL. The reason exists only on `AppointmentChangeRequest.CancellationReason`.

Live bug this phase also fixes: `PatientAppointmentCancelledNoBill.html:10` renders
`Cancellation reason: ##CancellationReason##`, and `StatusChangeEmailHandler.cs:566` fills that
token from `appointment.CancellationReason ?? string.Empty` -- so the patient's cancellation
email currently always shows a BLANK reason. The two staff-facing templates
(`ClinicalStaffCancellation.html`, `AppointmentCancelledRequest.html`) read from the change
request instead and are already correct.

Resolved decisions:

- Decision: PERSIST the reason onto `Appointment.CancellationReason` at cancel time AND audit
  every reader so the appointment and the change request cannot disagree (Adrian chose "both"),
  because a change-request join cannot cover the auto-cancel path (which has no change request
  at all) and would leave the patient email blank.
- Decision: add an EXPLICIT `billingStatus` wire field rather than leaving billing intent
  implicit in the `status` enum name, because the receiver should not have to string-match
  `CancelledNoBill`, and an explicit field survives a future enum rename.
- Decision: send the reason AS TYPED and document it in the contract as user-authored free
  text, because the payload already carries ePHI (patient name, DOB, injuries, claim details)
  so the reason adds no new data class; no truncation, so staff-visible content is not silently
  altered.
- Decision: NoShow is NOT in this phase. Adrian's NoShow flow needs a NEW INBOUND Case Tracker
  -> portal endpoint plus a pre-approved replacement appointment; that is its own phase and it
  depends on phase 4d's create-new-appointment machinery. Recorded in the roadmap instead.
- Decision: `ReScheduleReason` stays dead for now and is handled in phase 4e, because reschedule
  semantics are being redesigned there and wiring it twice would be wasted work.

## Concurrency (read before touching anything)

Another session has UNCOMMITTED edits adding party `Id`s in exactly these files:
- `src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/Payload/IntakePayload.cs`
- `src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/Payload/PartyResolver.cs`
- `docs/integration/case-tracker-api-contract.md`
- `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Integration/CaseTracker/IntakePayloadBuilderTests.cs`

Consequences for the builder:
1. Anchors below are deliberately STABLE IDENTIFIERS (class / property / method names), not line
   numbers, because their edits shift the lines.
2. Re-read each file and re-verify the anchor before editing. Do not trust a line number here.
3. Confirm the current branch with `git rev-parse --abbrev-ref HEAD` IMMEDIATELY before every
   commit -- a phase-1 commit landed on the other session's branch when it moved mid-build.
4. Commit BY PATHSPEC only.

## All needed context

- Payload DTO: `IntakePayload` in
  `src/.../Domain/Integration/CaseTracker/Payload/IntakePayload.cs`. Add the two new members
  next to the existing `Status` / `EvaluationKind` scalars, before the `Tenant` section.
- Builder: `IntakePayloadBuilder.ComposePayload` in the same folder assigns
  `Status = appointment.AppointmentStatus.ToString()` and
  `EvaluationKind = EvaluationKindWire.ToWire(appointment.EvaluationKind)`. The new fields are
  assigned in the same object initializer.
- Pattern to mirror EXACTLY for the wire mapping:
  `src/.../Domain/Integration/CaseTracker/Payload/EvaluationKindWire.cs` -- a static class with
  string consts + a `ToWire` switch that throws `ArgumentOutOfRangeException` on an unmapped
  value, chosen over `ToString()` so a future enum rename cannot silently change the wire format.
  The new mapper follows that reasoning and must NOT use `ToString()`.
- Staff cancel-approval path: `AppointmentChangeRequestsAppService.ApproveCancellationAsync` in
  `src/.../Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs`.
  It already sets `appointment.AppointmentStatus = input.CancellationOutcome` and
  `appointment.CancelledById = CurrentUser.Id` before `UpdateAsync`, and passes
  `reason: changeRequest.CancellationReason` into `AppointmentStatusChangedEto`. The reason
  assignment belongs beside `CancelledById`, inside the same update.
- Auto-cancel path: `JointDeclarationAutoCancelJob` sets
  `entity.AppointmentStatus = AppointmentStatusType.CancelledNoBill` and publishes
  `reason: "JDF-not-uploaded"` in the Eto WITHOUT persisting it. Reuse that exact string as a
  named constant rather than inventing a second spelling.
- Reason origin: `AppointmentChangeRequest.CancellationReason`; required at submit
  (`AppointmentChangeRequestsAppService` rejects a blank `input.Reason`), so a user-initiated
  cancel always has one.
- Billing outcomes available today: `AppointmentStatusType.CancelledNoBill` (5) and
  `CancelledLate` (6); the validator accepts only those two. `NoShow` (4) exists in the enum but
  has no API surface.
- Readers to audit (the "both" decision): `StatusChangeEmailHandler` (reads the appointment
  column -- the blank-email bug), `ClinicalStaffCancellationEmailHandler`,
  `ChangeRequestSubmittedEmailHandler`, `ChangeRequestConsentRequestEmailHandler`,
  `PublicChangeRequestConsentAppService`, `AppointmentChangeRequestDto` (all read the change
  request).
- Contract doc section to amend: `docs/integration/case-tracker-api-contract.md` section A
  (`data` top level table) and the STATUS VALUES table beneath it.

## Tasks

### Task 1 - map billing status to a wire value

- what: CREATE
  `src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/Payload/BillingStatusWire.cs`
  with a static `BillingStatusWire` exposing consts `NoBill = "NO_BILL"`, `Late = "LATE"`,
  `None = "NONE"` and `ToWire(AppointmentStatusType status)` mapping `CancelledNoBill` and
  `RescheduledNoBill` -> `NoBill`, `CancelledLate` and `RescheduledLate` -> `Late`, and every
  other status -> `None`. Unmapped is impossible by construction, so no throw; document why the
  default is `None` rather than an exception (most appointments are not in a billing-bearing
  state, and the field must always serialize).
- pattern: `EvaluationKindWire.cs` (same folder) -- consts + switch, explicit mapping, never
  `ToString()`.
- approach: tdd (pure function on the wire contract; a wrong value misbills a case)
- acceptance (EARS):
  - WHEN `ToWire` receives `CancelledNoBill` or `RescheduledNoBill`, THE SYSTEM SHALL return
    `NO_BILL`.
  - WHEN `ToWire` receives `CancelledLate` or `RescheduledLate`, THE SYSTEM SHALL return `LATE`.
  - WHEN `ToWire` receives any other `AppointmentStatusType`, THE SYSTEM SHALL return `NONE`.
  - THE SYSTEM SHALL NOT derive the wire value from `AppointmentStatusType.ToString()`.

### Task 2 - persist the cancellation reason on the appointment

- what: MODIFY
  `src/.../Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs`
  -- in `ApproveCancellationAsync`, inside the existing `CancelledNoBill || CancelledLate`
  branch that sets `CancelledById`, also set
  `appointment.CancellationReason = changeRequest.CancellationReason`.
  MODIFY `src/.../Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs` -- set
  `entity.CancellationReason` before `UpdateAsync`, and name the previously-inlined literal.

  CORRECTED DURING BUILD (2026-07-31): the plan said to persist "that exact string"
  (`"JDF-not-uploaded"`). That was WRONG for two reasons found by reading the consumers:
  (1) `JdfAutoCancelledEmailHandler.cs:64` FILTERS on that literal with an ordinal compare, so it
  is a routing DISCRIMINATOR -- changing it would silently stop that email firing; and
  (2) `Appointment.CancellationReason` is rendered to the booker and CC'd parties by
  `StatusChangeEmailHandler.DispatchCancelledNoBillAsync` via
  `PatientAppointmentCancelledNoBill`, and the JDF job also sets `CancelledNoBill` -- so
  persisting the token would have shown patients "JDF-not-uploaded" where they previously saw a
  blank. Resolution: TWO constants with distinct jobs --
  `AutoCancelReason` (unchanged discriminator, events only, never rendered) and
  `AutoCancelReasonText` (a human sentence, persisted + rendered + sent to the Case Tracker).
- pattern: the adjacent `appointment.CancelledById = CurrentUser.Id` assignment in the same
  branch; both writes ride the existing `UpdateAsync(appointment, autoSave: true)` so no new
  save is introduced.
- approach: test-after (DOWNGRADED from tdd during build, 2026-07-31, Adrian approved). Why:
  no integration harness exists for this area -- there is no test whatsoever for
  `ApproveCancellationAsync` or `JointDeclarationAutoCancelJob`, and no
  `AppointmentChangeRequestsTestData` seeder; every change-request test is a PURE unit test of a
  validator/policy. The app service takes 10 ctor dependencies, five of them CONCRETE domain
  classes (`AppointmentChangeRequestManager`, `AppointmentReadAccessGuard`,
  `BookingPolicyValidator`, `ChangeRequestConsentManager`, `ChangeRequestSideResolver`) that
  NSubstitute cannot stand in for without building their dependencies transitively, and the
  method touches ABP ambient members (`CurrentUser`, `L[...]`) 13 times. Building that harness
  dwarfs a two-line assignment and is tracked as its own task (phase 4c needs it). Coverage
  instead: the observable outcome is asserted through the existing `IntakePayloadBuilderTests`
  harness in task 3, and persistence is proven by the live SQL check in the validation loop --
  which is MANDATORY for this task, not optional.
- acceptance (EARS):
  - WHEN a supervisor approves a cancellation, THE SYSTEM SHALL persist the change request's
    reason onto `Appointment.CancellationReason`.
  - WHEN the joint-declaration job auto-cancels an appointment, THE SYSTEM SHALL persist the
    JDF reason constant onto `Appointment.CancellationReason`.
  - THE SYSTEM SHALL publish the same reason value in `AppointmentStatusChangedEto` as it
    persists on the appointment.

### Task 3 - carry both fields in the intake payload

- what: MODIFY `IntakePayload` -- add `public string? CancellationReason { get; set; }` and
  `public string BillingStatus { get; set; } = BillingStatusWire.None;` beside the existing
  `Status` scalar, each with the same XML-doc style as its neighbours.
  MODIFY `IntakePayloadBuilder.ComposePayload` -- assign
  `CancellationReason = appointment.CancellationReason` and
  `BillingStatus = BillingStatusWire.ToWire(appointment.AppointmentStatus)`.
- pattern: the existing `Status` / `EvaluationKind` assignments in the same object initializer;
  serializer settings are unchanged, so camelCase wire names come from the existing policy.
- approach: test-after (payload wiring; the mapping itself is unit-tested in task 1)
- acceptance (EARS):
  - WHEN an appointment is pushed while cancelled with a reason, THE SYSTEM SHALL include that
    reason in `data.cancellationReason`.
  - THE SYSTEM SHALL include `data.billingStatus` on EVERY intake payload, defaulting to
    `NONE` for a non-cancelled appointment, so the field is never absent.
  - WHERE an appointment has no reason recorded, THE SYSTEM SHALL send
    `data.cancellationReason` as null rather than an empty string.

### Task 4 - audit every reader of the reason columns

- what: MODIFY nothing unless a disagreement is found. Read each of
  `StatusChangeEmailHandler`, `ClinicalStaffCancellationEmailHandler`,
  `ChangeRequestSubmittedEmailHandler`, `ChangeRequestConsentRequestEmailHandler`,
  `PublicChangeRequestConsentAppService`, `AppointmentChangeRequestDto` and confirm each now
  resolves the SAME reason text. Report the audit result in the PR body; fix only a genuine
  mismatch.
- pattern: n/a (read-only audit).
- approach: code
- acceptance (EARS):
  - THE SYSTEM SHALL render a non-empty cancellation reason in the patient
    `PatientAppointmentCancelledNoBill` email for a staff-approved cancellation.
  - THE SYSTEM SHALL NOT present a different reason on the appointment than on its change
    request for the same cancellation.

### Task 5 - amend the Case Tracker contract

- what: MODIFY `docs/integration/case-tracker-api-contract.md` -- add `cancellationReason` and
  `billingStatus` rows to the section A `data` table (type, format, nullability, source), note
  under the STATUS VALUES table that billing intent is now explicit and that the status string
  remains authoritative for lifecycle, and state that `cancellationReason` is user-authored free
  text of unbounded length to be treated as untrusted display data and never logged.
- pattern: the existing `evaluationKind` / `previousAppointmentId` rows, which cite their source
  property.
- approach: code
- acceptance (EARS):
  - THE SYSTEM SHALL document both new fields in section A with their source properties.
  - THE SYSTEM SHALL record that `billingStatus` is additive and that an existing receiver
    ignoring it stays correct.

## Validation loop

Backend-only diff (C# + one markdown doc); no Angular file is touched, so `ng build` / `ng test`
are not required by `~/.claude/rules/testing.md`.

```bash
cd /c/src/patient-portal/main && dotnet format --verify-no-changes
```

```bash
cd /c/src/patient-portal/main && dotnet build -warnaserror
```

```bash
cd /c/src/patient-portal/main && dotnet test test/HealthcareSupport.CaseEvaluation.Domain.Tests --filter "FullyQualifiedName~BillingStatusWire|FullyQualifiedName~IntakePayloadBuilder"
```

```bash
cd /c/src/patient-portal/main && dotnet test
```

Live check (needs the local stack and an explicit go, since it mutates data): approve a
cancellation on a seeded appointment, then confirm in SQL that
`Appointment.CancellationReason` is non-null and that the queued
`IntegrationOutboxItem` payload contains `cancellationReason` + `billingStatus`.

## Risk / rollback

Blast radius: two additive payload fields, one new pure mapper, and two assignments on the
cancel paths. Additive on the wire -- an existing Case Tracker receiver that ignores unknown
fields is unaffected, which is why this does NOT need to wait for Levon. The behavioural change
is that a previously-blank patient cancellation email now shows real text, and
`Appointment.CancellationReason` starts holding data.

No schema change: both columns already exist on `Appointment`. No migration.

PHI note: `cancellationReason` is free text authored by an external user and now leaves the
portal. The payload already carries ePHI, so this adds no new data class, but the reason must
never be logged and must not appear in the dead-letter DTO (which today carries no PHI --
verify that stays true).

Rollback: revert the commits. Reverting stops NEW reasons being persisted but does not clear
rows already written; that is harmless, since the column is only read by the email and the
payload, both of which tolerate a value.
