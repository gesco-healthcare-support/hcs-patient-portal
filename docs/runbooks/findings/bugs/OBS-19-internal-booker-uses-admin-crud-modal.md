---
id: OBS-19
title: Internal staff "+ New Appointment Request" opens admin CRUD modal, not the external booking form
severity: observation
found: 2026-05-14 hardening Phase 3.15
flow: booking-internal-vs-external
component: angular/src/app/appointments/ (list page click handler) + AppointmentAddComponent
---

# OBS-19 - Internal booker has a different create flow than external roles

## Symptom
On `/appointments` (the list page reachable by internal staff: Clinic Staff, Staff Supervisor, admin), the "+ New Appointment Request" button:
- Does NOT navigate. URL stays at `/appointments`.
- Opens a modal with form controls: `panelNumber`, `appointmentDate`, `requestConfirmationNumber`, `dueDate`, `appointmentStatus` (and likely more below the fold).
- Looks like the ABP Suite generated `CreateUpdateAppointmentDto` modal - a flat admin-CRUD over the AppAppointments entity columns.

In contrast, the external booking route `/appointments/add` (reached by Patient/AA/DA/CE via their own UI flows):
- Renders the full multi-section form (Appointment Details, Patient Demographics, Employer Details, Applicant Attorney Details, Defense Attorney Details, Claim Information modal, Additional Authorized User).
- Wires every section through the production booking pipeline (`AppointmentsAppService.CreateAsync` + `AppointmentManager` invariants, server-side BookingPolicyValidator, packet generation hooks).

The two flows are not interchangeable for testing.

## Hypothesis
Single. The CRUD modal is leftover ABP Suite scaffolding from the bootstrap phase. The OLD app's internal-staff booking flow is presumably the SAME full form used by externals (per the OLD parity audit), just reached by a different UI affordance. The new app hasn't unified them yet.

## Functional impact
- R1 hardening test plan scenario 3.15 ("Clinic Staff books for existing patient") and 3.16 ("admin books") cannot be exercised as a happy path through the click-nav UI - the admin modal skips the multi-section workflow + the booking-policy validator.
- Internal staff who want to record a real booking can deep-link to `/appointments/add` and complete the external form, but that bypasses the intended internal flow and creates inconsistent UX.

## To do
1. **Decide intended internal-staff booking UX**: do they use the full form (then unify the click-nav button to navigate to /appointments/add)? Or is the admin-CRUD modal kept for internal-only quick entry (then it needs to call into AppointmentManager, not raw repo)?
2. Read OLD app `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\` to confirm OLD design.
3. After decision: drop 3.15 + 3.16 from R1 plan OR keep them with a note that they exercise a different path.

## Plan impact
**Removed from R1 happy-path**: 3.15 (Clinic Staff books), 3.16 (admin books). Reason: the only click-reachable internal-booker UI is the admin-CRUD modal which doesn't exercise the booking pipeline. Defer to a dedicated "internal-booker UX" workstream.

## Related
- [[OBS-18]] - /appointments/add has only [authGuard]; internal users can deep-link to the external form. Related but different concern.
- [[OBS-17]] - CE/Insurance lives in Claim Information modal (only present on external form, not the admin modal).
- [[BUG-021]] - datepicker mass-disable during loading (only on external form).
