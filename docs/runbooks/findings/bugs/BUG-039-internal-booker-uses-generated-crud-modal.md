---
id: BUG-039
title: Internal-staff "+ New Appointment Request" opens generated CRUD modal that bypasses BookingPolicyValidator + AppointmentManager
severity: medium
status: resolved
resolved: 2026-06-06
found: 2026-05-14 hardening Phase 3.15
promoted-from: OBS-19 (2026-05-22)
flow: booking-internal-vs-external
component: angular/src/app/appointments/appointment/components/appointment.component.ts (+ abstract base) vs angular/src/app/appointments/appointment-add.component.ts
---

# BUG-039 - Internal-staff booking flow bypasses booking-policy invariants

> **RESOLVED by AP1 (2026-06-06) -- Edit-half.** The Actions dropdown's "Edit" item
> (`(click)="update(row)"` -> the generated ABP CRUD detail modal) was removed, closing the
> Edit-half of this bug (and the folded-in OBS-19). The dropdown now offers Review / Reschedule
> / Cancel / Delete; reschedule/cancel route through the capacity-safe change-request workflow.
> The abstract base file + `update()` are retained for ABP-Suite regen parity (the modal is
> simply no longer reachable). See `docs/plans/2026-06-06-appointment-change-request-ui.md`.

> **Promoted from OBS-19 on 2026-05-22 after code verification.**
>
> **Confirmed code state (2026-05-22):**
> - The internal-staff list page lives at `angular/src/app/appointments/appointment/components/appointment.component.ts`. It extends `AbstractAppointmentComponent` (the ABP Suite generated base) and its template at `appointment.component.html:4-8` carries a `<button *abpPermission="'CaseEvaluation.Appointments.Create'" (click)="create()">` -- where `create()` lives on the abstract base and opens the auto-generated modal whose form fields match `CreateUpdateAppointmentDto` (flat columns: panelNumber, appointmentDate, requestConfirmationNumber, dueDate, appointmentStatus, ...).
> - The external booker route `/appointments/add` (BUG-038's subject) lives at `angular/src/app/appointments/appointment-add.component.ts` and renders the hand-built multi-section form wired through `AppointmentsAppService.CreateAsync` -> `AppointmentManager` -> `BookingPolicyValidator`.
>
> **The two paths are not interchangeable for data quality.** The internal CRUD modal writes via the auto-generated repository path, which skips `AppointmentManager` invariants and `BookingPolicyValidator`. Internal-staff bookings can therefore land in states the external flow would reject (out-of-horizon, inside-lead-time, missing claim information, etc.).
>
> **Suggested fix shapes (decide in the fix session):**
>
> | Option | What it does | Effort | Trade-off |
> |---|---|---|---|
> | **A. Unify on the external form** | Delete the abstract-base modal; rewire the "+ New" button to `router.navigate(['/appointments/add'])`. Internal staff use the same multi-section form externals do. | small | UX is heavier for internal quick-entry; matches OLD where internal + external used the same form. |
> | **B. Keep the modal, wire through AppointmentManager** | Replace the auto-generated `CreateAsync` proxy call with a new internal-only `AppointmentManager.QuickCreateAsync` that enforces the same invariants but accepts a flatter DTO. | medium | Internal quick-entry preserved, but two creation paths to maintain. |
> | **C. Delete the modal entirely** | Internal staff create via API only. Forces them to use the external form via deep-link. | small | Worst UX; not recommended. |
>
> **Recommended:** Option A unless internal-staff feedback says the quick-entry modal is essential. The OLD app (per `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\`) used a single form for both internal and external; that's the parity target. Worth confirming explicitly during the fix session.
>
> **Related:** [[BUG-038]] (`/appointments/add` route missing permissionGuard) -- complementary; both touch the same internal-vs-external split.

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
