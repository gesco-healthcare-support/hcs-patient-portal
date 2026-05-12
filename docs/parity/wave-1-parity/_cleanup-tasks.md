---
type: cleanup
audited: 2026-05-01
status: task-a-implemented-pending-testing; task-b-pending
phase: 0 (run before feature implementation begins)
---

# Cleanup tasks (Phase 0 of implementation)

Two cleanup tasks were identified during the audit phase and tracked as task IDs #7 and #8 in the working session's task list. Both must be executed BEFORE feature implementation begins, because subsequent feature code will reference role-model and entity-model decisions made by these cleanups.

A fresh implementation session will not have access to the original task list IDs. This file is the durable record. Treat it as Phase 0 of the master implementation plan.

## Task A -- Remove Doctor user role + login from NEW

**[IMPLEMENTED 2026-05-01 - pending testing]**

Implementation summary (Phase 0.1):

- `Doctor.IdentityUserId` field removed; constructor signature updated; `DoctorManager.CreateAsync`/`UpdateAsync` no longer accept `identityUserId`.
- `DoctorDto`/`DoctorCreateDto`/`DoctorUpdateDto`/`GetDoctorsInput`/`DoctorWithNavigationProperties(Dto)` no longer expose `IdentityUserId`.
- `IDoctorsAppService.GetIdentityUserLookupAsync` removed; corresponding controller route `/identity-user-lookup` removed; `DoctorsAppService` no longer holds `IdentityUserManager` or syncs IdentityUser fields on update.
- `EfCoreDoctorRepository` no longer joins `IdentityUser`; the `identityUserId` filter argument was removed from repository methods.
- `InternalUserRoleDataSeedContributor`: `DoctorRoleName` const + `DoctorGrants()` method + `EnsureRoleAsync(DoctorRoleName)` + `GrantAllAsync(DoctorRoleName, ...)` calls removed. Per-tenant seed reduced from 3 internal roles to 2 (Staff Supervisor + Clinic Staff). Doctor is non-user per OLD spec.
- `InternalUsersDataSeedContributor`: `doctor@<tenantSlug>.test` user seed entry + `LinkDoctorEntityAsync` method + `IRepository<Doctor, Guid>` dependency removed. Per-tenant seed reduced from 4 users to 3 (admin, supervisor, staff).
- `DoctorTenantAppService`: removed `EnsureRoleAsync("Doctor")` and `IdentityRoleManager` dependency; `CreateDoctorProfileAsync` now keys on Doctor.Email rather than `IdentityUserId`.
- `UserExtendedAppService`: removed Doctor sync side-effect (no longer reads `Doctor.IdentityUserId`); kept as extension seam for future hooks.
- DbContext mappings (host + tenant): removed `b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.IdentityUserId)` for `Doctor`. Host mapping retains `HasOne<Tenant>()`.
- Test fixtures: `Doctor1UserId`/`Doctor2UserId`/`Doctor1UserName`/`Doctor2UserName`/`Doctor1Email`/`Doctor2Email`/`DoctorRoleName` constants removed from `IdentityUsersTestData`. Doctor user seeding dropped from `IdentityUsersDataSeedContributor`. `CaseEvaluationIntegrationTestSeedContributor` Doctor seed no longer passes `identityUserId:`.
- Files deleted: none (entity stays as non-user reference).
- EF migration generated: `20260502000305_Drop_Doctor_IdentityUserId.cs` -- drops FK `FK_AppDoctors_AbpUsers_IdentityUserId`, drops index `IX_AppDoctors_IdentityUserId`, drops column `IdentityUserId`. Down() restores them.
- Solution build: 0 errors, 0 warnings.
- Pending: integration-test pass + DbMigrator run on dev DB to verify acceptance criteria; manual smoke test (login as admin/supervisor/staff; no doctor user present; Doctor entity still managed via UI).

### Original spec

**Original task #7.** Per role-model decision locked 2026-05-01 (see `project_role-model.md` memory file and `external-user-registration.md` "Review updates" section): NEW must match OLD's role model exactly -- 4 external + 3 internal user roles, with Doctor as a non-user entity (no login). NEW currently has Doctor as a logging-in user role with a planned "own appointments only" filter (W-DOC-1). Remove all of it for strict OLD parity.

### Why

OLD spec (`socal-project-overview.md` lines 119-135) defines exactly 4 external + 3 internal user roles. Doctor is a non-user entity -- the doctor entity exists in the DB but never logs in; Staff Supervisor manages doctor availability, location preferences, and appointment-type preferences on the doctor's behalf.

NEW added Doctor as a logging-in role and the W-DOC-1 "own appointments" filter as an extension beyond OLD spec. Adrian explicitly chose strict parity over this extension on 2026-05-01.

### Scope

Code locations to change:

- **`src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUsersDataSeedContributor.cs`:**
  - In `SeedTenantUsersAsync`, remove the `("doctor", InternalUserRoleDataSeedContributor.DoctorRoleName)` entry from the `seedPlan` array.
  - Remove the doctor-specific `if (prefix == "doctor") doctorUser = user;` capture and the post-loop `if (doctorUser != null) await LinkDoctorEntityAsync(doctorUser);` invocation.
  - Delete the `LinkDoctorEntityAsync(IdentityUser doctorUser)` method entirely.
  - The seed plan goes from 4 per-tenant users (admin, supervisor, staff, doctor) to 3 (admin, supervisor, staff). Host user `it.admin@hcs.test` stays.

- **`InternalUserRoleDataSeedContributor.cs`** (or wherever role-name constants live):
  - Remove the `DoctorRoleName` constant.
  - Remove the seed entry that creates the Doctor role in the tenant's role list.
  - Verify no other code references `DoctorRoleName`.

- **`src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs`:**
  - Remove the `IdentityUserId Guid?` FK field if present.
  - Verify no other code reads it.
  - The `Doctor` entity itself stays (managed entity, just not user-linked).

- **Any planned W-DOC-1 "own appointments only" filter:**
  - Search the codebase for `W-DOC-1` references and remove.
  - Specifically, in `EfCoreAppointmentRepository.cs`, remove any filter that scopes appointments to a doctor's IdentityUserId. The accessor-scoped filter (for attorney accessor visibility) stays.

- **Tests:**
  - Update `InternalUsersDataSeedContributor` tests to expect 3 per-tenant users + 1 host user.
  - Remove or update any test that asserts a Doctor role exists.

### Acceptance criteria

- Tenant data seed produces exactly 3 per-tenant users (admin, supervisor, staff) plus 1 host user (it.admin).
- No `DoctorRoleName` or doctor-role string anywhere in code.
- `Doctor.IdentityUserId` field absent from entity, DTO, and EF configuration.
- Doctor entity still functional via Staff Supervisor management UI (`staff-supervisor-doctor-management.md` audit covers this).
- All existing tests pass after cleanup.
- `git grep -i "DoctorRole"` returns no results in the source tree.

## Task B -- Remove `AppointmentSendBackInfo` from NEW

**[IMPLEMENTED-BACKEND 2026-05-01 - Angular cleanup pending; pending testing]**

Backend implementation summary (Phase 0.2):

- Files deleted: `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentSendBackInfo.cs`, `Application.Contracts/Appointments/AppointmentSendBackInfoDto.cs`, `Application.Contracts/Appointments/SendBackAppointmentInput.cs`, `angular/src/app/appointments/appointment/send-back-fields.ts`, `angular/src/app/appointments/appointment/components/send-back-appointment-modal.component.{ts,html}`.
- `AppointmentManager`: removed `IRepository<AppointmentSendBackInfo>` dep, `SendBackAsync` (Pending -> AwaitingMoreInfo + sendback-row insert), `SaveAndResubmitAsync` (AwaitingMoreInfo -> Pending), and the `AwaitingMoreInfo`/`SendBack`/`SaveAndResubmit` state-machine transitions. State machine now matches OLD: `Pending -> Approved | Rejected` (no AwaitingMoreInfo path). Approved -> downstream transitions unchanged.
- `IAppointmentsAppService` / `AppointmentsAppService`: removed `SendBackAsync`, `SaveAndResubmitAsync`, `GetLatestUnresolvedSendBackInfoAsync` interface methods + impls; removed `IRepository<AppointmentSendBackInfo>` ctor dep.
- `AppointmentController`: removed `[POST] /{id}/send-back`, `[POST] /{id}/save-and-resubmit`, `[GET] /{id}/send-back-info/latest` routes.
- `StatusChangeEmailHandler`: removed AwaitingMoreInfo email branch + `IRepository<AppointmentSendBackInfo>` dep + `GetLatestSendBackInfoAsync` helper. Now emails only on Approved + Rejected transitions.
- `CaseEvaluationApplicationMappers`: removed `AppointmentSendBackInfoToDtoMapper`.
- `CaseEvaluationDbContext` + `CaseEvaluationTenantDbContext`: removed `DbSet<AppointmentSendBackInfo>` properties + entity-configuration blocks.
- EF migration generated: `20260502001639_Drop_AppointmentSendBackInfo.cs` -- drops `AppAppointmentSendBackInfos` table. Down() restores the table (per ABP migration convention; for parity, this would never roll back).
- Solution build: 0 errors, 0 warnings.

Pending Angular cleanup (deferred to a follow-up task):

- `angular/src/app/appointments/appointment/components/appointment-view.component.ts` (~969 lines) still has SendBackInfo imports, state, methods, action handlers, and `latestSendBackInfo` property.
- `angular/src/app/appointments/appointment/components/appointment-view.component.html` still has the send-back modal selector and resubmit-mode UI.
- `angular/src/app/proxy/appointments/appointment.service.ts` + `models.ts` still reference `AppointmentSendBackInfoDto` and `SendBackAppointmentInput` -- these regenerate when `abp generate-proxy` runs against the cleaned-up backend.

Pending tests + manual verification:

- `dotnet test` pass; DbMigrator run on dev DB to apply both Phase 0 migrations; manual smoke test (no Send Back button on appointment detail page).
- `npx ng build --configuration development` will fail until Angular surgery + proxy regeneration completes.

### Original spec

**Original task #8.** Per strict-parity decision locked 2026-05-01: NEW has a "send back" feature (`AppointmentSendBackInfo` entity) where staff returns an appointment to the user for changes. Not in OLD spec. Remove.

### Why

OLD's appointment lifecycle is `Pending -> Approved | Rejected`. There is no intermediate "send back to user" state. If a clinic staff member needs the user to make changes, OLD's flow is to Reject the appointment -- the user can then use the Re-Request flow (`IsReRequestForm = true`) to edit and resubmit, reusing the same `RequestConfirmationNumber`.

NEW added `AppointmentSendBackInfo` as a smoother UX for "minor changes needed". Removed for strict OLD parity. Can be re-added later as a feature beyond OLD if business approves.

### Scope

- **`src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentSendBackInfo.cs`** -- delete the entity file.
- **`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentSendBackInfoDto.cs`** -- delete.
- **`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/SendBackAppointmentInput.cs`** -- delete.
- **`AppointmentManager.cs`:** remove the `SendBack(...)` method (or whatever the send-back domain method is named -- TO VERIFY exact name).
- **`AppointmentsAppService.cs`:** remove the AppService method that exposes send-back.
- **`AppointmentController.cs`:** remove the manual controller route for send-back.
- **`appointment-routes.ts`** + **`appointment.component.html`** + **`appointment-add.component.ts`**: remove send-back UI buttons and routes.
- **`send-back-appointment-modal.component.{ts,html,scss}`** + **`send-back-fields.ts`** -- delete the Angular standalone modal component.
- **EF migration:** generate a migration that drops the `AppointmentSendBackInfo` table. Apply via `dotnet ef migrations add RemoveAppointmentSendBackInfo` then `database update`.
- **Tests:** remove any tests of the send-back flow.
- **Auto-generated proxy:** regenerate via `abp generate-proxy` after backend changes.

### Acceptance criteria

- No `AppointmentSendBackInfo`, `SendBack`, or `send-back` references remaining in the codebase (`git grep -i "sendback\|send-back"` returns no app-code matches).
- Database migration applied; `AppointmentSendBackInfo` table dropped.
- Existing tests pass after cleanup.
- The Re-Request flow (rejected -> resubmit with same confirmation number) works end-to-end as the substitute UX, per `external-user-appointment-request.md` audit.

## Sequencing

Execute Task A and Task B in either order; they're independent.

Both must complete BEFORE Phase 1 (schema foundations) of the master implementation plan begins, because:

- The role model change affects every permission key registered in subsequent phases.
- The `AppointmentSendBackInfo` removal affects the Appointment entity migration baseline (cleaner to drop the table now than later).

## Verification before declaring Phase 0 done

- All affected projects build without warnings.
- `dotnet test` passes.
- `npx ng build --configuration development` for the Angular app passes.
- Manual smoke test: log in as the seeded admin/supervisor/staff users; no doctor user available; AppointmentDetail page does not show a SendBack button.

## Cross-references

- Role-model memory: `~/.claude/projects/W--patient-portal-replicate-old-app/memory/project_role-model.md`
- Strict-parity directive: `~/.claude/projects/W--patient-portal-replicate-old-app/memory/project_old-app-context.md`
- Affected feature audits:
  - `external-user-registration.md` (role model)
  - `clinic-staff-appointment-approval.md` (no send-back path; reject + re-request only)
  - `external-user-appointment-request.md` (Re-Request flow as substitute)
  - `staff-supervisor-doctor-management.md` (Doctor entity stays as managed entity)
- Master implementation prompt: see Phase 0 in the implementation plan you are executing.
