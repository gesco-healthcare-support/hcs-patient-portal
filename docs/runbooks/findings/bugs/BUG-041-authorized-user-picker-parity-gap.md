---
id: BUG-041
title: Authorized User picker restricts to 2 pre-existing roles; OLD allowed free-text email for any role
severity: medium
status: open
found: 2026-05-14 hardening Phase 3.9
promoted-from: OBS-16 (2026-05-22, after OLD-parity verification)
flow: booking-authorized-users-modal
component: src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:309 + angular/src/app/appointments/sections/* additional-authorized-user UI
---

# BUG-041 — Authorized User picker parity gap (NEW dropdown vs OLD free-text)

> 2026-05-24: renamed from `BUG-037-authorized-user-picker-parity-gap.md` to free `BUG-037` for the clinic-staff-document-upload-403 bug that main concurrently filed during the hardening run (`BUG-037-clinic-staff-cannot-upload-documents.md`).

> **Promoted from OBS-16 on 2026-05-22 after verifying OLD parity.** The OBS originally asked whether the narrow picker was intentional or over-restrictive. OLD-code review shows NEW's behavior is a real parity regression in expressiveness, distinct from the documented D-2 design decision.
>
> **NEW behavior (verified, current):** `ExternalSignupAppService.GetExternalUserLookupAsync` at line 309-374 returns only pre-existing users whose roles match the allow-list `["Patient", "Applicant Attorney"]`. SPA presents a single `<select>` dropdown of those users. Patient cannot authorize anyone who hasn't already registered, and cannot authorize a DA / CE / internal-staff even if they have. Filter is documented by Adrian's 2026-04-30 inline comment citing Wave-2 D-2.
>
> **OLD behavior (verified, `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-accessors\add\appointment-accessor-add.component.{ts,html}`):** the modal is titled "Add Authorized User Detail" with subheading *"Enter an email of a user with whom you want to share your appointment details."* Form fields:
>
> | Field | Control type | Source |
> |---|---|---|
> | First Name | free-text input | typed by Patient |
> | Last Name | free-text input | typed by Patient |
> | Email Address | free-text email input | typed by Patient |
> | User Type | `<select>` dropdown | `externalUserRoleLookUps` server lookup (likely all 4 external roles) |
> | Access Detail | View/Edit radio | `accessTypeLookUps` server lookup |
>
> **The authorized user does NOT need to be pre-registered in OLD.** Patient TYPES the email + name; the role dropdown CLASSIFIES the relationship (e.g. "this person is a Defense Attorney"); the system creates an accessor record regardless of whether the user has an account yet.
>
> **The D-2 decision and its over-application.** Adrian's 2026-04-30 comment says: "DA/CE register and login normally but their saved profiles do not surface in any picker, dropdown, or autocomplete to other tenant users." That is **compatible with OLD** because OLD does not have a user picker. NEW collapses two distinct concerns into one restriction:
>
> | Concern | D-2 says | OLD design | NEW behavior |
> |---|---|---|---|
> | DA/CE profile lookup surfaces | restrict | n/a (no picker exists) | restricted (correct) |
> | Patient's ability to grant authorization | (no D-2 statement) | by free-text email + role classification | restricted to 2 roles' pre-existing users |
>
> NEW's restriction on Patient's ability to grant authorization is **not** what D-2 specified -- it was added implicitly by collapsing the OLD free-text form into a dropdown.
>
> **Suggested fix shape:** restore OLD's free-text + role-classification pattern. Replace the dropdown with first-name / last-name / email / role-select fields. The role-select should return all 4 external roles from `externalUserRoleLookUps`. The accessor record stores the typed email + name + role; if the email later matches a registered user, downstream surfaces can resolve / merge. If never matched, the accessor remains a "future-user" pending record. This matches OLD parity and respects D-2 (DA/CE *profiles* still don't appear in lookups; Patient just types the email).
>
> **Alternative (smaller fix):** keep the dropdown but expand the role allow-list to all 4 external roles. Loses the "authorize someone not yet registered" capability but at least matches OLD's role coverage. Less work; preserves NEW's "must pre-exist" simplification.

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
