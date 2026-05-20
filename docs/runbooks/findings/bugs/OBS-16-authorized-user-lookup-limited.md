---
id: OBS-16
title: Authorized User dropdown shows only a 3-user subset; missing internal admins + DA + CE
severity: observation
found: 2026-05-14 hardening Phase 3.9
flow: booking-authorized-users-modal
---

# OBS-16 — Authorized User picker is filtered to a small subset

## Symptom
Phase 3.9: as SoftwareThree (Patient), opened the "Additional Authorized User" modal in the booking form. The user-picker `<select formcontrolname="identityUserId">` contained only:
- `Select email` (placeholder)
- `patient@falkinstein.test`
- `applicant.attorney@falkinstein.test`
- `SoftwareFour@gesco.com`

**Missing from the list:**
- `SoftwareOne@evaluators.com` (admin + Clinic Staff + Staff Supervisor)
- `SoftwareTwo@evaluators.com` (same)
- `SoftwareFive@gesco.com` (DA)
- `SoftwareSix@gesco.com` (CE)
- `defense.attorney@falkinstein.test` (seeded synthetic DA)
- `adjuster@falkinstein.test` (seeded synthetic CE)
- `admin@falkinstein.test`, `staff@falkinstein.test`, `supervisor@falkinstein.test`

## Hypothesis
The lookup endpoint likely filters by external-user role only AND further restricts to roles compatible with the Patient booker's view. AA is in (delegated access makes sense for the Patient's own attorney). The other roles (DA, CE, internal staff) probably can't be granted "Authorized User" access in OLD parity either, but it's worth checking the OLD app:
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\` for the analogous picker filter

OR there's a tenant-scoped lookup that's missing some rows. Note that DA + CE didn't appear despite being external users in the same tenant — suggests a more aggressive filter than just "external user."

## Functional impact
The Patient couldn't grant SoftwareOne (internal staff) access via this modal. Save with empty selection is a no-op (no DB row created).

## To do (fix session)
1. Locate the `/api/app/appointments/.../authorized-user-lookup` (or equivalent) endpoint.
2. Determine what filter it applies — is it intentional (Patient can only authorize their own AA) or a bug (over-restrictive)?
3. Check OLD parity for what the picker SHOULD show.
4. If intentional: update the parity audit doc + label the picker accordingly ("Authorize Your Attorney").
5. If bug: surface all eligible users.

## Related
- [[BUG-014]] (hardcoded email URLs) is in the same flow family; both relate to the inter-user notification model.
- The `defense.attorney@falkinstein.test` synthetic user is in the seeded DB (per `InternalUsersDataSeedContributor`) but doesn't appear here, suggesting a tenant-scoped filter is excluding them by role.
