---
id: BUG-031
title: Clinic Staff role gets 403 on POST /api/app/appointment-injury-details during booking flow
severity: medium
status: fixed
fixed: 2026-05-22 (Clinic Staff seed grant added; live-verified both routes 200)
found: 2026-05-21 hardening HRD-P3.5
flow: internal-staff-booking
component: src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs (ClinicStaffGrants)
---

# BUG-031 - 403 on /api/app/appointment-injury-details for Clinic Staff role

## Symptom

During HRD-P3.5 (clistaff1 books Record Review for existing patient1 via `/appointments/add` deep-link), after submitting the booking form, the Angular SPA fired an additional POST to `/api/app/appointment-injury-details` with the injury payload from the Claim Information modal. The response was 403 Forbidden.

API container log:
```
[18:13:37 INF] Request starting HTTP/1.1 POST http://falkinstein.localhost:44327/api/app/appointment-injury-details - application/json 232
[18:13:37 INF] Request finished HTTP/1.1 POST http://falkinstein.localhost:44327/api/app/appointment-injury-details - 403 0 null 212.5665ms
```

Browser console:
```
ERROR: Failed to load resource: the server responded with a status of 403 (Forbidden) @ http://falkinstein.localhost:44327/api/app/appointment-injury-details
```

Despite the 403, the booking itself succeeded -- `AppAppointments` row A00005 was created with all four party-email columns populated, indicating the injury data was inlined into the main `POST /api/app/appointments` payload. So the extra POST to `/appointment-injury-details` is a redundant follow-up (perhaps the Angular form re-submits standalone for an extra injury row, OR it's a legacy code path).

But the 403 itself is a permission gap: Clinic Staff is the canonical role for booking management; if the SPA fires that endpoint during the booking flow, the role must allow it.

## Hypothesis

1. **Permission gate is too tight.** The `AppointmentInjuryDetailsAppService.CreateAsync` (or equivalent) is decorated with `[Authorize(CaseEvaluationPermissions.Appointments.Edit)]` but Clinic Staff does NOT have that grant. The permission seeded for clinic staff covers approve/reject and view, but not create-injury. Fix: add the grant OR widen the permission scope.

2. **Endpoint is dead code.** The SPA POSTs to `/appointment-injury-details` redundantly; the main `POST /api/app/appointments` already created the injury inline. Server's 403 is correct (the endpoint is not meant for SPA traffic) but the SPA should not be firing it. Fix: remove the SPA call.

3. **Race condition.** The SPA fires the second POST BEFORE the first appointment-create completes, so the injury hasn't been linked to the appointment yet. The endpoint then can't find the appointment context and returns 403 (as a stand-in for "context missing"). Fix: chain the two requests, not parallel.

Most likely (1). The "ALL form sections work but server rejects the auxiliary call" pattern matches a permission-grant oversight when the new endpoint was added.

## Reproduction

1. Log in as `clistaff1@gesco.com`.
2. Navigate to `/appointments/add` (deep-link).
3. Pick existing patient or fill new patient.
4. Fill all sections including the Claim Information modal.
5. Click "Book an appointment".
6. DevTools Network tab shows the 403 on `/api/app/appointment-injury-details`.

## Recommended fix

Step 1: Inspect the endpoint authorization:
```bash
grep -rn "appointment-injury-details" src/
grep -rn "AppointmentInjuryDetail" src/HealthcareSupport.CaseEvaluation.Application/
```
Locate the `[Authorize(...)]` attribute on the controller action or AppService method.

Step 2: Map the permission name to roles in `CaseEvaluationPermissionDefinitionProvider.cs` AND in the role seed (likely `RolePermissionsDefinitionProvider` or similar). Confirm whether Clinic Staff has the grant.

Step 3: If the permission is missing -- add Clinic Staff to the seed OR widen the gate.

Step 4: If the endpoint is dead code -- remove the SPA call in the relevant `*.component.ts` (look for the booking submit handler).

## Functional impact

- Internal-staff booking via deep-link still works (the main `appointments` POST succeeds) but DevTools shows a confusing 403. Users may report "I clicked Book and got an error" even though the row was created.
- If the redundant POST was supposed to add a SECOND injury (multi-injury bookings), Clinic Staff cannot record those without the grant. Multi-injury bookings would fail.
- If a code reviewer looks at this 403 cold, they'd interpret it as a security gap (clinic staff lacks permission for routine booking ops).

## Related

- [[BUG-030]] -- same HRD-P3.5 scenario; the auto-approve-without-date issue. Both findings surfaced from the single clistaff1 booking attempt.
- [[BUG-023]] -- 403-vs-400 pattern (different endpoints but similar "wrong HTTP code for the actual condition"). If endpoint is dead, prefer 404 over 403.
- [[OBS-18]] -- `/appointments/add` route has no permission gate; server-side checks are authoritative. This BUG is the server-side check kicking in for a sub-resource of the main endpoint.

## Corrected root cause (2026-05-22)

The doc above offered three hypotheses; the second proposed
"endpoint is dead code -- the main `POST /api/app/appointments`
already created the injury inline." That hypothesis is **wrong**:

- `AppointmentCreateDto` (`Application.Contracts/Appointments/AppointmentCreateDto.cs`)
  carries zero injury fields.
- `AppointmentsAppService.CreateAsync` never writes an
  `AppointmentInjuryDetail` row.
- `angular/src/app/appointments/appointment-add.component.ts:2438-2440`
  deliberately POSTs each injury draft as a separate request after
  the main appointment POST. Lines 154-158 document the multi-injury
  workflow as the OLD-parity design.
- `appointment-view.component.ts:178-181` reads injuries via the
  separate `GET /api/app/appointment-injury-details/by-appointment/<id>` path.

So hypothesis 1 (permission gate too tight) is the actual cause. The
seed at
`Domain/Identity/InternalUserRoleDataSeedContributor.cs:367-426`
explicitly scoped Clinic Staff mutations to "Appointments + Patients"
when the seed was written, and the multi-injury workflow's separate
POST design never made it into that scope decision.

Role-grant matrix for `CaseEvaluation.AppointmentInjuryDetails.Create`
in `AbpPermissionGrants` before the fix:

| Role | Has Create? |
|---|---|
| admin (host + tenant) | yes |
| IT Admin | yes |
| Staff Supervisor | yes |
| **Clinic Staff** | **no** -- the gap |
| Patient | yes |
| Claim Examiner | yes |
| Applicant Attorney | yes |
| Defense Attorney | yes |

Every booking-flow role except Clinic Staff had the grant.

## Fix verified (2026-05-22)

Fix lives at
`src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs`,
inside `ClinicStaffGrants()`, immediately after the existing
`Create("Patients")` yield:

```csharp
yield return Create("Patients");
yield return Edit("Patients");

// W2-8 -- the booking-add SPA fires a separate POST per injury draft
// (multi-injury support per OLD parity). Clinic Staff is the canonical
// phone-in booker so it needs the mutation grant alongside the
// existing Appointments / Patients mutations. Without this, the
// booking-add flow succeeds on the main appointment row but returns
// 403 on every auxiliary injury POST -- silently breaking
// multi-injury bookings.
yield return Create("AppointmentInjuryDetails");
```

One yield. Idempotent at seed time via the existing
`GrantAllAsync(...)` helper (`IPermissionManager.SetAsync` overwrites
with the same `IsGranted = true` value on re-seed).

### DB grant landed

After `docker compose down -v && docker compose up -d --build` and a
clean migrator run, `AbpPermissionGrants` for Clinic Staff in the
Falkinstein tenant now shows both `Default` and `Create` rows:

| ProviderKey | Permission |
|---|---|
| Clinic Staff | CaseEvaluation.AppointmentInjuryDetails |
| Clinic Staff | CaseEvaluation.AppointmentInjuryDetails.Create |

### Live verification matrix

Two POSTs against
`http://falkinstein.localhost:44357/api/app/appointment-injury-details`
on the freshly-rebuilt stack, after seeding a Pending appointment for
both bookers:

| Test | Booker | Pre-fix | Post-fix actual |
|---|---|---|---|
| A | `clistaff1@gesco.com` (Clinic Staff) | 403 (the bug) | **200** + SQL row `CLM-BUG031-TestA / Lower back` persisted |
| B | `patient@falkinstein.test` (Patient -- had grant before) | 200 | **200** + SQL row `CLM-BUG031-TestB / Neck` persisted (**no regression**) |

### Unit-test suite

Application.Tests: 550/550 pass (the freshly-rebuilt container picked
up the BUG-025 test files that weren't visible in the prior
bind-mounted runs). Domain.Tests: 13/13 pass (+ 4 pre-existing
Skipped). No regressions.

### Out of scope (captured here for follow-up)

- **`.Edit` / `.Delete` for Clinic Staff** on `AppointmentInjuryDetails`.
  The booking-add SPA edits injury drafts in-memory and only fires
  Create POSTs at submit time; the appointment-view page is read-only
  per current MVP scope. If post-booking injury editing becomes a
  Clinic Staff workflow later, add `.Edit` / `.Delete` to the same
  yield block.
- **Related sibling endpoints**: the booking-add SPA also POSTs to
  `/api/app/insurances` and `/api/app/appointment-claim-examiners`
  per `appointment-add.component.ts:2464-2494`. If those endpoints
  have the same Clinic Staff seed gap, file as separate findings
  (not exercised in this BUG-031 reproduction).
