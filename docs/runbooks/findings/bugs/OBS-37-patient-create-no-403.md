---
id: OBS-37
title: Patient role POST /api/app/appointments returns 400 (validation), not 403 (permission) -- intentional or gap?
severity: observation
status: open
found: 2026-05-25 (Mon AM hardening session, post-#250 merge)
flow: appointments-create-permission-gate
component: src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs + CaseEvaluationPermissions.Appointments.Create
---

# OBS-37 - Patient role passes the Create-appointment permission gate

## Symptom

While probing R2 permission rejections during the post-`#250` hardening
sweep, a Patient-role JWT (patient1@gesco.com) was used to POST to
`/api/app/appointments`. The response was **400 Bad Request** with
validation errors citing missing required fields (patientId,
doctorAvailabilityId, identityUserId, etc.).

Expected: **403 Forbidden** if Patient role does not hold the
`CaseEvaluation.Appointments.Create` permission.

Actual: the request reached the model-validation phase, which means
the permission gate was passed. Two reads of this:

1. **Intentional (parity-port design).** OLD `P:\PatientPortalOld`
   allows patients to self-book their own appointments. The new
   stack may have granted `Appointments.Create` to Patient role on
   purpose so the self-book flow works. In that case OBS-37 is
   working-as-designed and can be closed.
2. **Gap.** If Patient role should NOT have Create, this is a
   permission-definition leak: probably
   `CaseEvaluationPermissionDefinitionProvider.cs` granted
   `Appointments.Create` more broadly than intended, OR the
   `[Authorize(...)]` attribute on the AppService method allows
   any authenticated caller.

## Reproduction

```bash
TOKEN=$(curl -s -X POST 'http://falkinstein.localhost:44368/connect/token' \
  -d 'grant_type=password&username=patient1@gesco.com&password=1q2w3E*r&client_id=CaseEvaluation_App&scope=offline_access openid profile email CaseEvaluation' \
  | jq -r .access_token)

curl -i -X POST 'http://falkinstein.localhost:44327/api/app/appointments' \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"appointmentTypeId":"a0a00002-0000-4000-9000-000000000003",
       "locationId":"a0a00005-0000-4000-9000-000000000001",
       "appointmentDate":"2026-06-02","appointmentTime":"10:00:00",
       "requestConfirmationNumber":"TEST-001"}'
```

Returns HTTP 400 with `validationErrors` array. (Would return 403
with `Microsoft.AspNetCore.Authorization.AuthorizationMiddlewareResultHandler`
in the response if Create permission was denied.)

## Recommended action

Determine if Patient role SHOULD have Create permission:

- If yes (parity self-book): close OBS-37 as working-as-designed.
  Document the design decision in `docs/parity/` so future role-
  matrix audits don't re-flag it.
- If no: tighten `CaseEvaluationPermissionDefinitionProvider.cs` so
  `Appointments.Create` is granted only to Applicant Attorney,
  Defense Attorney, Claim Examiner, and Clinic Staff / Staff
  Supervisor / IT Admin (the role-matrix in the booking flow). Add
  a unit test asserting Patient is denied.

## Functional impact

If gap: medium severity -- bypasses the role-based segregation of
booking duties. Patients could theoretically self-book at-will,
which may violate the WCAB workflow if the doctor's office is
supposed to gatekeep.

If by-design: zero impact, just documentation hygiene.

## Related

- `[[OBS-32]]` (booker AA section prefill behavior).
- `[[BUG-038]]` (appointments/add route missing permissionGuard,
  filed on parity branch; closed as low-severity earlier).
