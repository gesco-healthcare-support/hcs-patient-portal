---
id: OBS-38
title: Existing-patient dropdown on booking form does not prepopulate Date of Birth, even though the patient row has it
severity: observation
status: open
found: 2026-05-25 (Mon AM hardening, during A00002 QME booking)
flow: appointment-booking-patient-demographics-prepopulate
component: angular/src/app/appointments/sections/appointment-add-patient-demographics.component.ts (onExistingPatientSelected handler)
---

# OBS-38 - Existing Patient selection skips Date of Birth prepopulate

## Symptom

On `/appointments/add` the "Existing Patients" dropdown lists prior
patients in the tenant. Selecting a value should prepopulate the
patient-demographics fields from that patient's stored row.

During the 2026-05-25 hardening run, booking A00002 (QME at Demo
Clinic South) as appatty1, the dropdown was populated with
`patient1@gesco.com` (the patient on A00001 from earlier). On
select:

- **Populated:** Last Name, First Name, Email.
- **NOT populated:** Date of Birth (the input stayed empty + flagged
  `ng-invalid`).

The booking submit failed because DoB is `required`. Manual
reselection via the DoB picker was needed.

## Expected

The booking form's existing-patient handler should pull EVERY
field on the demographics form from the stored Patient row,
including:

- Date of Birth
- Cell phone, phone, SSN, address
- Gender, language, interpreter preference

The Patient row in `AppPatients` has all of these from the first
booking. Re-typing them is friction for re-bookers (the most
common case).

## Reproduction

1. Book one appointment as a new patient with full demographics.
2. On a second `/appointments/add` view, select that patient from
   the "Existing Patients" dropdown.
3. Observe: only Last Name + First Name + Email get populated;
   DoB and the rest stay blank.
4. Submit -> 400 with `dateOfBirth required` validation error.

## Recommended fix

In `appointment-add-patient-demographics.component.ts`, find the
`patientId` ValueChanges subscription that handles existing-patient
selection. It currently sets only a subset of FormControls. Extend
to set ALL fields from the loaded `PatientDto`, including
`dateOfBirth`. Convert the stored ISO date string into the
ngbDateStruct that the picker expects (`{year, month, day}`) before
setting.

## Functional impact

Low severity in absolute terms (user can re-fill DoB manually) but
high severity for **demo UX**: an attorney rebooking a returning
patient currently re-types everything. Hurts the "look how easy
this is" narrative. Fix is small (~10 lines of TS).

## Related

- `[[OBS-32]]` (attorney-booker AA section also prefills only first
  name, not full name -- same pattern of partial prepopulate).
