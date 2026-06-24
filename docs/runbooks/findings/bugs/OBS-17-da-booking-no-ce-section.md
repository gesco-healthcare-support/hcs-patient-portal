---
id: OBS-17
title: CE / Insurance fields live inside the Claim Information modal (not a top-level section)
severity: observation
found: 2026-05-14 hardening Phase 3.13
flow: booking-form-structure
status: resolved-not-a-bug
---

# OBS-17 - CE/Insurance is per-injury (inside Claim Information modal)

## Symptom
Logged in as `SoftwareFive@gesco.com` (Defense Attorney role) and navigated to `/appointments/add`. Section headings rendered:
- Appointment Details
- Patient Demographics
- Employer Details
- Applicant Attorney Details
- Defense Attorney Details
- Claim Information
- Additional Authorized User

**Missing:** Claim Examiner / Insurance section. Previous bookings by Patient and AA roles showed a CE/Insurance section as a sibling of the AA/DA sections.

## Hypothesis
Three competing theories:
1. The form template hides the CE section by role (`@if (currentRole !== 'DefenseAttorney')`). Reason might be that the DA is the adversary to CE; design says DA cannot grant CE access.
2. The CE inputs moved inside the Claim Information modal (added on injury, alongside carrier + policy details). Less likely - the previous AA booking showed CE as a top-level section.
3. The CE section is hidden when DA toggle = OFF by default and only appears after some other action.

## To check
- `angular/src/app/appointments/sections/appointment-add-claim-examiner-insurance.component.html` for `*ngIf` conditions.
- `angular/src/app/appointments/appointment/appointment-add.component.ts` for role-gated section rendering.
- OLD app parity: `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\` for the analogous role-view.

## Functional impact
DA cannot grant Claim Examiner access via booking form. Workaround: AA / Patient / Staff book and include CE; or CE self-registers and gets scoped access via two-hop AppointmentInjuryDetails match.

## Related
- [[OBS-16]] (Authorized User picker subset filter) - same flow family, same role-view question.
