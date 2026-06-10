---
id: IP6
title: Patient becomes a record-only entity; relocate under User Management; kill shared-password auto-create at booking
type: enhancement
components: [angular/src/app/patients/patient/providers/patient-base.routes.ts, angular/src/app/app.routes.ts, angular/src/app/route.provider.ts, src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs, src/HealthcareSupport.CaseEvaluation.Domain/Patients/PatientManager.cs, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs, src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs, src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs]
related_known_bugs: [OBS-37, OBS-38, OBS-39, SEC-05, Q-12, NEW-SEC-04, OBS-25, BUG-004, BUG-008]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Today there are THREE competing ways to produce a Patient, and a Patient cannot exist
without an IdentityUser. The standalone admin "New Patient" form is an orphan that demands a
pre-existing IdentityUserId; booking silently auto-mints a login account with a shared
hardcoded password; and a separate invite path emails a claim link. The patient section also
lives under Doctor Management.

Desired end-state (simplify):
- Patient is a RECORD that may exist with NO login (the workers'-comp patient is routinely a
  third party booked by an attorney/CE/adjuster who never logs in).
- Relocate Patients under User Management (re-parent nav + re-path the route).
- KILL the shared-password auto-create at booking. Booking creates the record only. The
  appointment-request email (IP6 link to E1/E2/E3 model) carries the login/register link; on
  self-register with the SAME email, link the Patient record + that patient's appointments.
- Make Appointment.IdentityUserId nullable; patient visibility keys off Patient.IdentityUserId
  once claimed.
- RETIRE the standalone admin "New Patient" form in favor of the invite flow under User
  Management.

## Current behavior (from investigation)
- PATH 1 admin "New Patient" form: `PatientsAppService.CreateAsync` REQUIRES a non-empty
  IdentityUserId (throws "IdentityUser field is required" when Guid.Empty), creates NO
  IdentityUser, assigns NO role, sends NO email -- `PatientsAppService.cs:486-495`. The form
  itself has a REQUIRED IdentityUser lookup (`patient-detail.component.html:311-320`) and
  `identityUserId` is `Validators.required` in `patient-detail.abstract.service.ts:50-100`.
- PATH 2 booking auto-create: `GetOrCreatePatientForAppointmentBookingAsync`
  (`PatientsAppService.cs:138-322`) is gated only by `[Authorize]` (ANY authenticated booker).
  When no IdentityUser exists for the email it ALWAYS mints one with
  `tempPassword = CaseEvaluationConsts.AdminPasswordDefaultValue` (verified
  `PatientsAppService.cs:245`), grants the "Patient" role (`:259-268`), sets neither a real
  password nor EmailConfirmed, sends NO email. There is no branch that books WITHOUT minting
  an account.
- PATH 3 invite: `ExternalSignupAppService.InviteExternalUserAsync` (`:868-944`) issues a
  one-time 7-day SHA256-hashed token, emails a tenant-prefixed claim link; the recipient
  self-registers via `RegisterAsync` (`:460-676`), which for `UserType.Patient` (=1) creates
  the IdentityUser with the user's OWN password and calls `PatientManager.CreateAsync` with
  placeholder defaults (`:566-583`). Only this path emails a claim link.
- Domain contract blocks record-only today: `PatientManager.CreateAsync`
  (`PatientManager.cs:23-54`) takes identityUserId as a REQUIRED Guid (Check.NotNull, :25).
- Structural root cause: `Appointment.IdentityUserId` is a REQUIRED NoAction FK
  (Migrations Designer:4039-4043; `AppointmentManager.CreateAsync:37` Check.NotNull). The
  Angular form sets `appointment.identityUserId = patient.identityUserId`
  (`appointment-add.component.ts:1592, :1706, :1826`) -- the appointment FK IS the patient's
  IdentityUser, not the booker's. Visibility keys off Patient.IdentityUserId == CurrentUser.Id
  (`AppointmentsAppService.cs:186-191`, ComputeExternalPartyVisibilityAsync case 2).
- Nav misplacement: Patients route lives at `/doctor-management/patients` parented to
  `::Menu:DoctorManagement` (`patient-base.routes.ts`); top-level routes at
  `app.routes.ts:128` (my-profile) and `:136` (list). A `::Menu:UserManagement` parent
  already exists hosting Invite External User + Internal Users (`route.provider.ts:35-64`).
- Documented intent already matches: Patients CLAUDE.md Q-12 / SEC-05 (`Domain/Patients/CLAUDE.md:58-60`)
  -- "Auto-created patient IdentityUsers get AdminPasswordDefaultValue ... Intent: replace
  with invite-token flow." Parked test at `PatientsAppServiceTests.cs:474` tracked as
  NEW-SEC-04.

## Relevant code locations
Backend:
- `src/.../Application/Patients/PatientsAppService.cs` -- CreateAsync (`:486-495`),
  GetOrCreatePatientForAppointmentBookingAsync (`:138-322`, shared-password at `:245`),
  UpdatePatientForAppointmentBookingAsync (`:324-371`), UpdateMyProfileAsync (`:517-551`).
- `src/.../Domain/Patients/PatientManager.cs` -- CreateAsync (`:23-54`, drop Check.NotNull on
  identityUserId), FindOrCreateAsync (`:135-205`).
- `src/.../Domain/Patients/Patient.cs` -- IdentityUserId becomes nullable FK.
- `src/.../Domain/Appointments/Appointment.cs` + AppointmentManager.cs (`:37`) -- nullable FK.
- `src/.../Application/Appointments/AppointmentsAppService.cs` -- ComputeExternalPartyVisibilityAsync (`:186-191`) + booking create.
- `src/.../Application/ExternalSignups/ExternalSignupAppService.cs` -- patient invite +
  link-by-email linkback hook on RegisterAsync (`:460-676`).
- `src/.../Application.Contracts/Patients/PatientCreateDto.cs` / PatientUpdateDto.cs --
  IdentityUserId becomes optional in the create shape.
- EF Core migrations -- FK nullability for Patient.IdentityUserId and Appointment.IdentityUserId.
Frontend (relocation + form retirement):
- `patient-base.routes.ts` -- parentName `::Menu:DoctorManagement` -> `::Menu:UserManagement`;
  path `/doctor-management/patients` -> `/user-management/patients`; order under UM.
- `app.routes.ts:128` (my-profile), `:136` (list) -- re-path both.
- Three hardcoded `navigateByUrl`: `appointment-add.component.ts:1341`,
  `home.component.ts:253`, `patient-profile.component.ts:177`.
- `route.provider.ts:35-64` -- the User Management parent (relocation target).
- `patients/patient/components/patient-detail.component.html` + `patient.component.html:5-13`
  + `patient-detail.abstract.service.ts` -- retire the standalone create form / drop the
  required IdentityUser lookup.
- `angular/src/app/proxy/` -- regenerate after DTO changes (never hand-edit).

## Phase 3 cross-reference
- SEC-05 / Q-12 / NEW-SEC-04 -- shared hardcoded AdminPasswordDefaultValue at booking; killed
  outright here (root cause removed, not patched). Un-skip `PatientsAppServiceTests.cs:474`.
- OBS-39 (seed-patient-blank-name) -- the blank-name Patient row that surfaces in this list is
  a symptom of the form/seed/auto-create disconnect; the record-only model fixes the source.
- OBS-37 (patient-create-no-403) -- self-serve vs staff-provisioned ambiguity is resolved by
  the single record path + opt-in claim; re-evaluate the Appointments.Create gate while here.
- OBS-38 (existing-patient-no-dob-prepop) -- same theme of Patient row data not flowing
  through; touch the existing-patient booking path while in this file.
- OBS-25 (invite-acceptance-no-auto-confirm) -- set EmailConfirmed=true on claim redemption so
  the patient claim is one click; bundle since the same RegisterAsync path is touched.
- BUG-008 (put-me-concurrency) -- UpdateMyProfileAsync is the claimed patient's self-edit path;
  must stay working post-change. Live-repro while here.
- BUG-004 (patient-preselected) -- external-user Register defaulted to Patient; relevant to the
  invite-as-create UX. Verify defaults are sane when reusing the invite flow.

## Research findings
- Internal patterns / prior art:
  - InvitationManager token pipeline already built and audited (one-time, 7-day TTL,
    256-bit token, SHA256 at rest) -- reuse, do not invent. Driven from
    `ExternalSignupAppService.InviteExternalUserAsync`.
  - AA/DA already follow the target model: a record exists independently and the account
    links on registration (AutoLink hooks in ExternalSignupAppService) -- the patient
    link-by-email is the same shape, so there is precedent to copy.
  - ExternalUserType is NUMERIC: Patient=1 (per angular/src/app/CLAUDE.md external-users note);
    Patient is already a first-class invite target.
  - Self-service edit paths (UpdateMyProfileAsync, UpdatePatientForAppointmentBookingAsync)
    already freeze IdentityUserId/TenantId/GenderId/DOB on update -- preserve that on the
    claimed-account path.
- External docs: none required; this reuses in-repo ABP Identity + InvitationManager
  patterns. EF Core nullable-FK migration is standard (alter column to nullable, NoAction
  retained).

## Approaches considered (with tradeoffs)
- Option B (minimal): keep booking auto-create but swap the shared password for an
  invite-token claim (locked account + emailed claim link). Pros: smallest change, no
  migration, removes the security defect fast. REJECTED as the end-state because it still
  mints an IdentityUser at booking for a third-party patient who may never log in -- it makes
  the conflation safe but does not remove it, leaves the orphan admin form, and keeps three
  create paths. (It remains a viable interim security-only fix if the full model must wait.)
- Option C (full UX): retire the admin form AND replace it with a User-Management "Add Patient
  (invite)" action in one step, booking becomes record-only. REJECTED as the starting move
  because it bundles a schema change + PHI-visibility rewrite + a user-facing page removal in
  one drop; highest coordination/risk. Its destination (form retired, single invite-create
  surface) is exactly what we want -- we just sequence the schema change first.
- Option A (chosen): Patient becomes record-only (nullable Appointment.IdentityUserId +
  nullable Patient.IdentityUserId), booking inserts the record only, login is an opt-in claim
  via the appointment-request email's register link, and self-register links by email. WINS
  because it removes the root cause (the required FK that forced account-minting), collapses
  three paths into one record path + one claim path, kills the shared password by deletion not
  by patching, and matches the documented intent (Q-12/SEC-05) and the AA/DA precedent. The
  form retirement (Option C's UX) folds in naturally once record-only exists.

## Decision (locked 2026-06-03)
Patient is a RECORD that may exist with no login.
- Relocate Patients under User Management: re-parent nav (`::Menu:DoctorManagement` ->
  `::Menu:UserManagement`) and re-path `/doctor-management/patients` ->
  `/user-management/patients`; update `app.routes.ts:128, :136`, the three hardcoded
  navigateByUrl (`appointment-add.component.ts:1341`, `home.component.ts:253`,
  `patient-profile.component.ts:177`), and the nav.
- KILL the shared-password auto-create: `GetOrCreatePatientForAppointmentBookingAsync` creates
  the Patient record ONLY (no IdentityUser, no role, no shared password). The
  appointment-request email's login/register link lets the patient self-register; on register
  with the SAME email, link the Patient record + their appointments (link-by-email).
- Make `Appointment.IdentityUserId` nullable; patient visibility keys off
  `Patient.IdentityUserId` once claimed.
- RETIRE the standalone admin "New Patient" form in favor of the invite flow under User
  Management.
- Keep it SIMPLE: one record path + one opt-in claim path. ALL deletes remain soft.

## Implementation outline (no code)
1. Domain: `PatientManager.CreateAsync` accepts a nullable identityUserId (drop Check.NotNull
   at `:25`); `Patient.IdentityUserId` becomes nullable. (server enforcement)
2. Domain: `Appointment.IdentityUserId` becomes nullable; `AppointmentManager.CreateAsync`
   drops the Check.NotNull at `:37`. (server enforcement)
3. EF Core migration: alter both columns to nullable; retain NoAction FK. FLAG MIGRATION.
4. Application (booking): `GetOrCreatePatientForAppointmentBookingAsync` -- remove the
   IdentityUser create / role grant / `AdminPasswordDefaultValue` block (`:245-268`); insert
   the Patient record only and persist the appointment with null IdentityUserId. (server)
5. Application (link-by-email): on self-register (`ExternalSignupAppService.RegisterAsync`),
   find an existing Patient by email, set Patient.IdentityUserId, and back-link that patient's
   appointments; set EmailConfirmed=true on redemption (folds OBS-25). Mirror the AA/DA
   AutoLink hook. (server enforcement)
6. Application (visibility): confirm ComputeExternalPartyVisibilityAsync case 2
   (`:186-191`) still surfaces appointments via Patient.IdentityUserId == CurrentUser.Id and
   degrades safely when null (unclaimed -> not visible to any patient login). (server)
7. Application/Contracts: make IdentityUserId optional in PatientCreateDto; retire admin
   create endpoint usage. REGENERATE PROXY after DTO changes.
8. Angular relocation: re-parent + re-path in `patient-base.routes.ts`; update `app.routes.ts`
   `:128`/`:136`; update the three navigateByUrl call sites; update nav under UM. (UI affordance)
9. Angular form retirement: remove the standalone "New Patient" create form / required
   IdentityUser lookup; point "Add Patient" at the User-Management invite flow (Patient=1).
   (UI; server remains the enforcement source for record creation)
10. Tests: un-skip `PatientsAppServiceTests.cs:474`; assert no shared password is set and that
    booking produces a record with null IdentityUserId; assert link-by-email on register.

## Dependencies
- BLOCKS UM4 (this is its named upstream dependency -- the relocated Patients section + invite
  flow live under the User Management surface UM4 builds).
- Depends on the E1/E2/E3 email model (appointment-request email carries the login/register
  link that is now the patient's only path to a login). Coordinate the email body's
  "log in or register" link with the link-by-email registration.
- Touches the same RegisterAsync redemption path as OBS-25 (auto-confirm) -- land together.

## Residual open questions
- Auto-email the claim link at booking vs only when staff explicitly grants portal access.
  Default to the appointment-request email's existing link (no extra contact); confirm no
  separate "send portal invite" trigger is needed for Phase 1. (minor; UX timing only)
