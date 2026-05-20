---
id: OBS-24
title: Cluster of OLD booking-time validators not ported to NEW server-side
severity: low-to-medium (UI enforces; server-side defense-in-depth missing)
status: open
found: 2026-05-20 (triage of `_remaining-from-old-audit-2026-05-15.md` booking-validator rows)
flow: appointment-booking, appointment-reschedule, injury-details
component:
  - src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs (CreateAsync, UpdateAsync)
  - src/HealthcareSupport.CaseEvaluation.Domain/AppointmentInjuryDetails/AppointmentInjuryDetailManager.cs
  - src/HealthcareSupport.CaseEvaluation.Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.cs
related:
  - BUG-027 (same shape: UI enforces, server-side missing)
  - OBS-23 (also a server-side gate missing)
---

# OBS-24 - Six OLD booking-time validators have no NEW server-side equivalent

## Symptom

OLD enforced six validation rules at the AppService / domain layer during
booking and reschedule. NEW only enforces them in the Angular reactive
form (UI side). Direct API callers bypass all six.

## The six missing validators

### V1 -- Booking date < DOB

**OLD source:** `AppointmentDomain.CommonValidation` (P:\PatientPortalOld)

**OLD rule:** reject the booking if `input.AppointmentDate < patient.DateOfBirth`.

**NEW state:** no such check anywhere. `grep` for any cross-comparison of
appointment date with patient DOB returns nothing.

**Practical impact:** very low. A real user would have to deliberately
set their patient DOB after the appointment date to trigger this. The
UI date picker doesn't allow it.

### V2 -- Injury date <= today

**OLD source:** `AppointmentInjuryDetailDomain.AddValidation`

**OLD rule:** reject if `input.DateOfInjury > DateTime.Today`.

**NEW state:** `AppointmentInjuryDetail` entity accepts any DateTime;
`AppointmentInjuryDetailManager.CreateAsync` / `UpdateAsync` validate
ClaimNumber + BodyPartsSummary length but NOT DateOfInjury.

**Practical impact:** low. UI date picker enforces it. Direct API caller
could create an injury "happening tomorrow", which is logically wrong but
not security-relevant.

### V3 -- Injury date >= patient DOB

**OLD source:** Same.

**OLD rule:** reject if `input.DateOfInjury < patient.DateOfBirth`
(can't be injured before you were born).

**NEW state:** not implemented.

**Practical impact:** low. Same as V1.

### V4 -- Cumulative trauma date range (From + To)

**OLD source:** `AppointmentInjuryDetailDomain`

**OLD rule:** if `IsCumulativeInjury` is true, `ToDateOfInjury` must be
set AND `ToDateOfInjury > DateOfInjury`.

**NEW state:** entity has `IsCumulativeInjury` + `ToDateOfInjury` fields,
manager accepts both as parameters, but NO enforcement that the two
fields are consistent. A cumulative injury with null ToDateOfInjury
passes the manager today.

**Practical impact:** medium. Cumulative-trauma cases have legal
implications -- the date range is binding for the claim. Inconsistent
data here would surface in reports.

### V5 -- Stakeholder email uniqueness

**OLD source:** `AppointmentDomain.CommonValidation`

**OLD rule:** the four email fields on the appointment
(`PatientEmail`, `ApplicantAttorneyEmail`, `DefenseAttorneyEmail`,
`ClaimExaminerEmail`) must all be distinct. No "patient is also their
own attorney" cases.

**NEW state:** all four emails persist directly to the entity in
`AppointmentsAppService.cs` (lines 728-731 for Create, 906-909 for
Update). No cross-comparison check.

**Practical impact:** medium. Self-dealing is a real concern in workers'
comp. The UI form likely doesn't allow duplicate entry but direct API
callers can.

### V6 -- Accessor role consistency

**OLD source:** `AppointmentAccessorDomain.AddValidation`

**OLD rule:** when adding an accessor by email, look up the user
account for that email; the user's role must match the accessor's
declared role (the AppointmentAccessor row's intended role context).

**NEW state:** `AppointmentAccessor` entity has `IdentityUserId` +
`AccessTypeId` (View/Edit) but no role consistency check. The accessor
is stored as a Guid pointer to an IdentityUser; whether that user is
a Patient or Attorney or anyone else isn't validated against the
appointment's context.

**Practical impact:** medium. Could allow weird accessor setups (a
Patient role assigned as an Attorney's accessor) that confuse the UI.

### V7 -- Same-day reschedule time gate

**OLD source:** `AppointmentDomain.UpdateValidation` (reschedule path)

**OLD rule:** if rescheduling on the same day as the new slot, the
current wall-clock time must be earlier than the slot's `FromTime`.
You can't reschedule into a slot that already started.

**NEW state:** `RescheduleRequestValidators` checks
`CanRequestReschedule` (appointment status) + time-window from system
parameter. No same-day "slot already started" check.

**Practical impact:** medium. Could let an admin reschedule into a
4 PM slot at 5 PM, which is non-sensical scheduling.

## Why this matters (in aggregate)

None of the six gaps are critical individually. But:

- They're all the same SHAPE as BUG-027 (UI enforces, server-side
  doesn't). Direct API callers and automation hit each one.
- Workers'-comp evaluation is a regulated domain. Data-integrity gates
  are not just UX -- they shape the legal record.
- The OLD app had all six. Strict parity per CLAUDE.md prescribes
  porting them.

## Recommended action

Three options:

- **A** -- File 7 separate small fixes (1 PR per validator). Maximum
  audit traceability. Slow.

- **B** -- Build a single `BookingDomainValidators` static class with
  all 7 rules + 7 facts. One PR. Cleanest. ~50 lines of pure logic
  + 7 tests + 1 wiring change per call site (CreateAsync, UpdateAsync,
  InjuryDetailManager, ChangeRequestManager).

- **C** -- Defer all 7 until alpha testing exposes which ones actually
  matter. Wrap each call site in a `// PARITY-FLAG-NEW-007: missing
  V<n>` comment per CLAUDE.md's bug-and-deviation policy. Plan a
  bulk port after first-round feedback.

**Recommendation:** **B**. One PR + one plan + one set of tests. The
validators are pure logic (no DI, no DB on most of them; V5 + V6 need
a lookup but the rest are formula-on-input).

## Audit doc cross-reference

`docs/parity/_remaining-from-old-audit-2026-05-15.md` rows to flip:

- Line 116 (Booking date < DOB) -- V1
- Line 117 (Injury date <= today; >= DOB) -- V2 + V3
- Line 118 (Per-injury duplicate check) -- ALREADY IMPLEMENTED (verified
  in manager lines 41-48); flip to Implemented
- Line 119 (Cumulative trauma range) -- V4
- Line 120 (Stakeholder email uniqueness) -- V5
- Line 121 (Accessor role consistency) -- V6
- Line 122 (Same-day reschedule time gate) -- V7

Flip lines 116, 117, 119, 120, 121, 122 to "Not implemented -- see
OBS-24". Flip line 118 to Implemented.

## Test plan (when fix lands)

Each rule gets one happy-path + one rejection fact. 14 facts total.
Pure unit tests on the validator class; no integration tests needed
since the validators are functions on input.
