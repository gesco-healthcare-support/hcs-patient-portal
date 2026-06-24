---
id: OBS-26
title: Slot-gen UI rejects overlapping time windows at same location across different AppointmentTypes
severity: observation
status: open
found: 2026-05-23 hardening HRD-P0 (combo 2)
flow: slot-generation
component: src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs GeneratePreviewAsync (conflict check ~lines 320-363)
---

# OBS-26 - Slot conflict check is location-scoped, not type-scoped

## Symptom

In Phase 0 of the hardening run, the staff-supervisor `Generate Slot` form
at `/doctor-management/doctor-availabilities/generate` rejects a request
when a slot already exists at the same `(location, date, time)` window,
REGARDLESS of the `AppointmentType` chosen for the new slot.

Sequence:
1. As `stafsuper1`, generated 58 daily 09:00-09:15 slots for AME at
   Demo Clinic North. Success.
2. Same form, switched type to QME, kept location + window. Clicked
   `Generate Slot` -> preview rendered with 58 rows.
3. Clicked `Submit`. Response toast: **"Some generated slots already
   exist. Please remove them before submitting."** Submit button became
   disabled.

This matches the implementation note (per code-read of
`DoctorAvailabilitiesAppService.cs`): the conflict-flagging logic is
location-scoped, not type-scoped. A single (location, date, time) tuple
can hold only one slot regardless of which AppointmentType it serves.

## Why this matters

The runbook's Phase 0 description and the prior `state.slots.*` example
both assumed slots are per-type-per-location, so 6 types could share the
same 09:00-09:15 window at Demo Clinic North. They cannot. Phase 0 must
stagger the 6 types across 6 non-overlapping 15-minute windows per
location (e.g., 09:00, 09:15, 09:30, 09:45, 10:00, 10:15).

The hardening run worked around this by using staggered windows.

## Question for the design

Is the location-scoped conflict intentional or a bug?

- **Intentional**: one doctor (the implicit single doctor per location)
  can only do one thing at one time, regardless of type. Makes scheduling
  sense.
- **Bug**: clinics with multiple doctors per location might want
  per-type parallel slots. The current single-doctor SEED-2 state
  doesn't expose this.

If intentional: update the runbook Phase 0 wording + the
`AppointmentDoctorAvailability` ER documentation to make this clear.
If a bug: needs a type-aware conflict check.

## Recommended next step

Open a design question with Adrian about per-location vs per-type slot
conflict semantics. Update docs to match the chosen answer.

## Related

- HRD-P0.1 (the runbook scenario this surfaced under).
- SEED-2 (single doctor per location); SEED-3 (slots-per-type).
- Project Overview slot-generation rework plan.
