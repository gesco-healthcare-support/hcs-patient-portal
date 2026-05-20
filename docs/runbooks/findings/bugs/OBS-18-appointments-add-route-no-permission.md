---
id: OBS-18
title: /appointments/add route uses only [authGuard] - no role/permission check; defense-in-depth gap
severity: observation
found: 2026-05-14 hardening Phase 3.15 (raised by Adrian)
flow: booking-route-guard
component: angular/src/app/app.routes.ts:148-151
---

# OBS-18 - /appointments/add reachable by ANY authenticated user

## Symptom
`angular/src/app/app.routes.ts:148-151`:
```ts
{
  path: 'appointments/add',
  loadComponent: () => Promise.resolve(AppointmentAddComponent),
  canActivate: [authGuard],   // <-- no permissionGuard, no permission data
},
```

Every other create/list route in this file uses `[authGuard, permissionGuard]` with a `data: { requiredPolicy: '...' }` clause. The booking route is the only one that opts out. Result: any logged-in user (internal **or** external) can deep-link to `http://falkinstein.localhost:4200/appointments/add` and the SPA will render the full external-facing booking form.

## Important context (per Adrian, 2026-05-14)
The "+ New Appointment Request" button on `/appointments` for **internal** users opens a **different form/modal** than the `/appointments/add` page that external users (Patient/AA/DA/CE) get. Internal users CAN reach `/appointments/add` but the internal button does NOT navigate there - it triggers a separate internal-flow modal.

So `/appointments/add` is the external booker's form; internal staff have their own create flow. The defense-in-depth concern is:
- A Patient/AA who guesses or shares the URL still reaches it (intentional - this IS their flow).
- An internal Clinic Staff / admin can reach it too, even though their normal click-nav goes through the other flow. Not a security bug, but if the internal flow does something different (different defaults, different mapping), routing them through the external flow produces inconsistent data.
- A CE who is meant to be excluded from booking-as-self has nothing stopping them from reaching the form. (Aside - see [[BUG-021]] - the datepicker happens to gate them visually until slots load, but that is incidental.)

## Hypothesis
Open by design - the booking form was originally only reachable by external roles, so dropping the permission guard was acceptable. Once internal users got their own flow on `/appointments`, no one revisited the external route.

## Recommendation
Two non-mutually-exclusive options:
1. **Permission gate the external route** to `CaseEvaluation.Appointments.Create` (or a dedicated `BookExternalAppointment` permission) so only roles intended to be external bookers can hit it. Internal staff using their internal flow do not need to call this route.
2. **Server-side authoritative check**: confirm `AppointmentsAppService.CreateAsync` validates the caller's role + scope - if so, the route guard is informational only and the gap is low-severity.

## To do (next pass)
- Compare data flow: does the internal `/appointments` button trigger the same `POST /api/app/appointments` shape, or a different endpoint?
- Read `angular/src/app/appointments/appointments.component.ts` (or wherever the list page lives) to see the click handler for "+ New Appointment Request".
- Confirm the OLD app's role-by-route matrix - the `Documents_and_Diagrams/` folder may have the answer.

## Related
- [[BUG-021]] - datepicker mass-disable during loading; only visible if you reach this route via URL.
- [[OBS-16]] - Authorized User picker subset (same booking flow).
- [[OBS-17]] - CE/Insurance lives in Claim Information modal (same flow).
