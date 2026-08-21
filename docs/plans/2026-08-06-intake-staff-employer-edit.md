---
feature: Intake Staff can save employer details from the appointment edit form
date: 2026-08-06
status: draft
base-branch: main
related-issues: []
---

# Intake Staff employer-detail edit

Found while live-gating epic phase 4e; NOT caused by it and independent of the reschedule epic.
Branches off `main`, not off the stacked 4d/4e branches.

## Goal

An Intake Staff member can complete an "Edit details" save on an appointment that has employer
data, instead of every such save failing at the employer step.

## Context & decisions

### What is actually wrong (two of my first three claims were wrong -- corrected here)

- The failure is NOT silent. `appointment-view.component.ts:1182-1184` catches the downstream
  upsert failure, sets `errorMessage`, and the red `ad-note--rejected` banner renders "Appointment
  and patient updated, but a downstream save (employer / attorney) failed." The form stays in edit
  mode. Verified live 2026-08-06.
- The employer PUT is NOT gated on the section being dirty. `hasEmployerData()` (`:2000-2011`)
  returns true when any employer field has a VALUE, so `upsertEmployerDetails` fires on EVERY save.
  Intake Staff therefore cannot complete an edit-details save on ANY appointment carrying employer
  data -- every time, not intermittently.
- Blast radius is exactly ONE entity, not five. The save fans out to three upserts (`:1169-1175`):
  employer (standalone service, gated `AppointmentEmployerDetails.Edit`,
  `AppointmentEmployerDetailsAppService.cs:109`) plus applicant and defense attorney, which are
  bare `[Authorize]` on `AppointmentsAppService` (`:1256`, `:1513`) and cannot 403 this way. Injury
  details are deliberately not upserted from this form (`:1171-1174`).
- The partial save is real and disclosed: appointment and patient commit, employer does not.

### Why the grant is missing

`IntakeStaffGrants()` gives Intake Staff `Create` on all five booking children and `Edit` on none --
only Appointments, Patients and DoctorAvailabilities. The seeder comment at `:543-550` records
fixing the CREATE half of this exact 403 class on 2026-07-16 and choosing "Create only (no Edit),
matching the sibling child grants at this tier". So today's state is a decision, not an oversight --
but it collides with a form that presents the fields as editable regardless.

### Resolved decisions

- Decision 1 (Adrian, 2026-08-06, via modal): GRANT Intake Staff
  `CaseEvaluation.AppointmentEmployerDetails.Edit`. They are the front-line reviewer, already hold
  `Appointments.Edit` and `Patients.Edit`, and already CREATE the employer record at booking --
  being unable to correct what they typed reads as a gap in the tier rather than a safeguard.
  Rejected: hiding the fields per-role (more UI work, and leaves them needing a supervisor for a
  typo) and dirty-gating the call (ends the every-save failure but leaves the capability mismatch
  for anyone who genuinely edits the section).
- Decision 2 (Adrian, 2026-08-06, via modal): improve the failure message to name the section and
  distinguish "you do not have permission" from "it broke". Naming "employer / attorney" is
  actively misleading -- the attorney upserts cannot fail this way.

### Deployment note

No migration. `GrantAllAsync` calls `_permissionManager.SetAsync(..., isGranted: true)`
(`:134-140`), which is idempotent and additive, so an existing office picks the grant up on the
next DbMigrator run. The grant must therefore reach every tenant: it is seeded in the PER-TENANT
pass (`:103`), which the migrator runs per office.

## Tasks

### T1 -- grant the permission

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs`
  -- add `yield return Edit("AppointmentEmployerDetails");` immediately after the existing
  `Create("AppointmentEmployerDetails")` at `:550`, with a comment recording that the edit form
  upserts employer on every save so Create-without-Edit made the form unusable for this role.
- pattern: the `Create("AppointmentEmployerDetails")` line and its comment block at `:543-550`.
- approach: tdd
- acceptance (EARS): THE SYSTEM SHALL include `CaseEvaluation.AppointmentEmployerDetails.Edit` in
  the Intake Staff tenant grant set. WHERE a role is not Intake Staff or Staff Supervisor, THE
  SYSTEM SHALL NOT gain any permission from this change.

### T2 -- pin it with a test

- what: MODIFY `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Identity/InternalUserRoleGrantsTests.cs`
  -- add a fact asserting the Intake Staff set contains
  `CaseEvaluation.AppointmentEmployerDetails.Edit`, with a comment naming the every-save behaviour
  of `hasEmployerData()` so a later reader knows why Edit is required and does not "tidy" it away.
- pattern: `IntakeShadow_can_create_every_booking_child` (`:171-179`) and its parity-fix comment.
- approach: tdd
- acceptance (EARS): WHEN the Intake Staff grant set is built, THE SYSTEM SHALL contain the employer
  Edit permission, and removing the grant SHALL fail this test.

### T3 -- say which save failed and why

- what: MODIFY `angular/src/app/appointments/appointment/components/appointment-view.component.ts`
  -- capture the error in the catch at `:1182` (currently a bare `catch`), and set a message that
  names the employer section and distinguishes a permission failure (HTTP 403) from any other
  fault. Keep the existing "appointment and patient were saved" disclosure, since that remains true.
- pattern: the sibling error branches at `:1195-1206`, which already set specific messages per
  stage.
- approach: test-after
- acceptance (EARS): WHEN the employer upsert fails with 403, THE SYSTEM SHALL tell the user they
  lack permission to edit employer details and that the rest of the save succeeded. WHEN it fails
  for any other reason, THE SYSTEM SHALL report a failure without claiming a permission problem.

### T4 -- pure helper + spec for the message

- what: CREATE the message derivation as a pure exported function beside the component (mirroring
  the util-plus-spec convention used by `reschedule-chain.util.ts`, `cr-approve.util.ts` and the
  other `*.util.ts` files in that folder) so the branch is unit-tested without a TestBed.
- pattern: `reschedule-chain.util.ts` + `reschedule-chain.util.spec.ts`.
- approach: tdd
- acceptance (EARS): WHEN given a 403, THE SYSTEM SHALL return the permission wording. WHEN given
  any other error, THE SYSTEM SHALL return the generic wording. WHEN given no error, THE SYSTEM
  SHALL return no message.

## Validation loop

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test test/HealthcareSupport.CaseEvaluation.Domain.Tests/HealthcareSupport.CaseEvaluation.Domain.Tests.csproj
```

Frontend:

```
export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe"
npx prettier --check <changed files>
npx eslint <changed files>
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

No migration; `has-pending-model-changes` is not expected to change and is not part of this loop.

Mutation check: delete the new `Edit("AppointmentEmployerDetails")` grant and confirm T2 fails;
restore.

## Live gate

Requires a DbMigrator run so the new grant reaches the seeded offices, then an API restart.

1. `docker compose run --rm db-migrator`, then `docker restart main-api-1`.
2. Sign in at `admin.localhost:4200` as `clistaff1@gesco.com` / `1q2w3E*r`, Enter practice
   (Falkinstein).
3. Open an appointment that HAS employer data -- falkinstein A00038 is the confirmed repro; it
   failed on every save before this change.
4. "Edit details", change any field, Save.
5. Assert the success banner appears, NOT "a downstream save ... failed", and that the API log shows
   `PUT /api/app/appointment-employer-details/{id}` returning 200 rather than 403.
6. Confirm the employer change actually persisted by reloading the page.

### RESULT (2026-08-06) -- PASSED

Ran on falkinstein A00038 as `clistaff1@gesco.com` (Intake Staff), the same appointment, role and
endpoint that returned 403 before the change.

- DbMigrator seeded all four offices; verified in SQL that `AbpPermissionGrants` now holds
  `CaseEvaluation.AppointmentEmployerDetails.Edit` for `Intake Staff` alongside the existing
  `.Create` and read grants.
- Edited `employerOccupation` and saved: the banner read "Saved -- Appointment, patient, employer,
  applicant attorney, and defense attorney details updated successfully" and the form left edit
  mode, where before it stayed in edit mode with the downstream-failure error.
- API log: `PUT /api/app/appointment-employer-details/{id} - 200` (was 403).
- Persistence confirmed in SQL: `Occupation` = the edited value.

The new 403 wording could NOT be exercised live, because the grant is what stops the 403 happening
-- that branch is covered by the 8 unit tests in `save-failure-message.util.spec.ts` instead. Stated
rather than claimed as live-verified.

## Risk / rollback

Blast radius: one permission on one internal role, plus one error message. No schema, no API
surface, no external-user path. Staff Supervisor and every external role are untouched.

The permission widens what Intake Staff may change -- that is the point, and it is narrower than
the `Appointments.Edit` and `Patients.Edit` they already hold. Employer details are not PHI-bearing
in the way patient identifiers are.

Rollback: revert the commit and re-run DbMigrator. Note that `SetAsync(isGranted: true)` does not
un-grant on revert -- an office that has already been re-seeded keeps the permission until it is
explicitly revoked, so a rollback needs a deliberate revoke rather than just a code revert.
