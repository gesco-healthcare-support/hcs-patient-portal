---
id: BUG-043
title: Booking form submits with zero Claim Information; no form-level rule requires at least one injury/claim
severity: medium
status: open
found: 2026-05-27 (userflow audit; A00001 + A00002 both submitted with no claim info)
flow: appointment-booking
component: angular/src/app/appointments/appointment-add.component.ts (injuryDrafts, submit path); angular/src/app/appointments/sections/appointment-add-claim-information.component.ts
parity: regression -- OLD blocked submit when no injury detail existed
---

# BUG-043 - Claim Information not required at booking

## Symptom

A00001 and A00002 were both booked and submitted successfully with the
"Claim Information" section showing "No claim information added yet."
The resulting appointments have zero `AppAppointmentInjuryDetails` rows
(DB-verified: Injury count = 0). No warning, no block.

## Root cause (confirmed: code + live)

- The Claim Information section is **not** a validated reactive-form
  control. It is an in-memory `injuryDrafts: AppointmentInjuryDraft[]`
  array (`appointment-add.component.ts:175`) managed by a modal. The
  submit path calls `persistInjuryDraftsIfProvided(appointmentId)`
  (`:2396`) which **no-ops when `injuryDrafts.length === 0`** -- it is
  explicitly optional.
- The booking `FormGroup` has no validator (and no submit-time guard)
  requiring `injuryDrafts.length >= 1`. All other required fields
  (appointmentDate/time, patient first/last/email/DOB, employer
  name/occupation) carry `Validators.required`, but claim info has none.
- The per-claim modal itself **does** validate correctly: clicking
  "Add" with empty fields shows "Please complete the required fields
  highlighted below" and red-flags Date Of Injury, Claim Number, Body
  Parts, Insurance Company Name, and the Claim Examiner sub-fields
  (live-verified `claim-modal-validation.png`). So the gap is strictly
  at the parent form level: the modal stops you from adding an
  *incomplete* claim, but nothing forces you to add *any* claim.

## Parity regression (OLD enforced it)

OLD's `checkInjuryDetailFormGroupValidation()`
(`P:\PatientPortalOld\...\appointment-add.component.ts:1375-1392`): when
`appointmentInjuryDetails.length == 0`, it validated the inline injury
form group and, if invalid/empty, pushed an "Injury" validation summary
entry that blocked submit (the submit guard at :402 requires
`appointmentFormGroupValidationSummaryViewModel.length == 0`). Net: OLD
required at least one injury detail. NEW dropped that enforcement.

## Functional impact

- Workers-comp appointments can be created with no claim number, date of
  injury, body parts, insurance, or claim examiner -- the core medical-
  legal context of the evaluation. Downstream packet generation and CE
  scoping ([[OBS-35]]) silently get nothing to work with.
- Ties to [[BUG-042]] item: the user's observation that "Claim Examiner
  and Claim Information are missing in the view" is explained here --
  they are empty because nothing was entered, and nothing required it.

## Recommended fix (high level -- see plan)

1. Client: block submit when `injuryDrafts.length === 0` with an inline
   message anchored on the Claim Information card (mirror OLD's "Injury"
   validation summary). Decide whether >= 1 claim is mandatory for all
   appointment types or only some (confirm against OLD's per-type
   behavior + AppointmentTypeFieldConfigs).
2. Server: add a guard in the create flow (or `AppointmentManager`) so
   the API also rejects an appointment with no injury detail when the
   type requires it -- defense in depth, since the SPA is not the only
   caller ([[BUG-038]] shows the route guarding gaps).

## Related

- [[OBS-35]] ce-scope-misses-injuryless-appointment -- downstream effect
  of injuryless appointments existing.
- [[BUG-040]] cumulative-trauma-not-persisting -- adjacent injury-detail
  persistence bug.
- [[BUG-024]] reject-accepts-empty-reason -- same "missing required
  validation" family.
