---
stage: 1
title: Registration and booking-entry research (R1, R2)
audited: 2026-05-04
old-source-roots:
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\User\UsersController.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs
new-source-roots:
  - W:\patient-portal\replicate-old-app\angular\src\app\
  - W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\ExternalSignups\ (after G2 merge)
  - W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Patients\PatientsAppService.cs
  - W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Appointments\AppointmentsAppService.cs
related-design-docs:
  - docs/design/external-user-registration-design.md
  - docs/design/terms-and-conditions-design.md
related-parity-docs:
  - docs/parity/external-user-registration.md
---

# Stage 1 -- Registration + Booking Entry

Two tasks at the entry of the appointment lifecycle: build the Angular
external-user registration form (R1), and propagate the dedup signal so
`Appointment.IsPatientAlreadyExist` is correctly written on initial
`Add()` rather than only at reschedule-approval (R2).

OLD source citations are exact path:line. Quoted code blocks are
verbatim from `P:\PatientPortalOld\` (read-only).

---

## R1 -- Angular external registration form

### OLD source (verbatim citations)

- `patientappointment-portal\src\app\components\user\users\add\user-add.component.html` -- registration template (62 lines)
- `patientappointment-portal\src\app\components\user\users\add\user-add.component.ts` -- registration component (130 lines)
- `patientappointment-portal\src\app\components\user\users\domain\user.models.ts` -- `UserLookupGroup` shape
- `patientappointment-portal\src\app\components\login\login\login.component.html:35` -- "Don't have an account yet? Sign-Up" link target
- `patientappointment-portal\src\app\components\login\login\login.component.ts:116-119` -- `gotoSignup()` routes to `/users/add`
- `PatientAppointment.Api\Controllers\Api\User\UsersController.cs:36-46` -- `POST /api/Users` calls `Domain.User.AddValidation` then `Domain.User.Add(user)`
- `PatientAppointment.Domain\UserModule\UserDomain.cs:67-93` -- `AddValidation` (email-uniqueness, password regex, password-confirm)
- `PatientAppointment.Domain\UserModule\UserDomain.cs:95-129` -- `Add` (external user branch -- attorney firm derivation, GUID `VerificationCode`, `IsVerified=false`, `SendEmail` on `isNewUser=true`)
- `PatientAppointment.Domain\UserModule\UserDomain.cs:104-108` -- attorney FirmEmail / FirmName persistence:

  ```csharp
  if (user.RoleId == (int)Roles.PatientAttorney || user.RoleId == (int)Roles.DefenseAttorney)
  {
      user.FirmEmail = user.EmailId.ToLower().ToString().Trim();
      user.FirmName = user.FirmName;
  }
  ```

- `PatientAppointment.Domain\UserModule\UserDomain.cs:113-114` -- verification GUID + verify flag:

  ```csharp
  user.VerificationCode = Guid.NewGuid();
  user.IsVerified = false;
  ```

- `PatientAppointment.Domain\UserModule\UserDomain.cs:314-333` -- `SendEmail(user, isNewUser=true)` (`EmailTemplate.UserRegistered`, link `/verify-email/{userId}?query={VerificationCode}`)
- `PatientAppointment.DbEntities\Constants\RegexConstant.cs:15` -- password pattern:

  ```csharp
  public const string systemPasswordPattern =
      @"^(?=.*[0-9])(?=.*[a-zA-Z])(?=.*[-.!@#$%^&*()_=+/\\'])([a-zA-Z0-9-.!@#$%^&*()_=+/\\']+)$";
  ```

- `PatientAppointment.DbEntities\Models\User.cs` -- columns `FirmName`, `FirmEmail`, `IsAccessor`, `IsVerified`, `VerificationCode`, `RoleId`, `UserTypeId` (via Role nav)

### OLD behavior (numbered)

1. Visitor lands on `/users/add` (linked from login footer "Sign-Up").
2. Lookup call fetches `genderLookUps`, `externalUserRoleLookUps` (only the 4 external roles), `cityLookUps` -- `user-add.component.ts:48`.
3. Component sets system defaults via `setDefaultValues` (`ts:57-68`) -- `userTypeId=ExternalUser`, `applicationTimeZoneId=1`, `createdBy=1`, `createdOn=now`, `statusId=Active`, `phoneNumber=0`, `oldPassword="null"` (literal string), `isActive=Active`.
4. Visitor selects role from dropdown (4 options). On change, `selectChangeHandler()` (`ts:84-101`) toggles `isAttorney`. When `roleId == PatientAttorney || DefenseAttorney`: makes `firmName` required (max 50), removes required from `firstName`. Otherwise: required on `firstName` (max 50), no firmName.
5. Form re-renders: non-attorney shows `[firstName | lastName]` (50/50 row), attorney shows single `firmName` field (`html:23-36`).
6. Always-visible: email (text, no `type=email`), password, confirm-password, footnote "By clicking 'Sign Up' you agree to our terms of service and privacy policy" (link opens `TermAndConditionComponent` modal).
7. Sign Up button is `[disabled]="!userFormGroup.valid"` (`html:49`).
8. POST to `/api/Users` (`UsersController:37`). `AddValidation` (`UserDomain:67-93`):
   - `CommonValidation` -- if `RoleId in (PatientAttorney, DefenseAttorney)` and `FirmName == ""`, fail with `FirmNameValidation` (`UserDomain:272-275`).
   - Email uniqueness on non-deleted rows.
   - Password regex `systemPasswordPattern`.
   - For external users: `UserPassword.Trim() == ConfirmPassword.Trim()`.
9. `Add` (`UserDomain:95-129`):
   - Attorney branch -- copy `EmailId.ToLower().Trim()` -> `FirmEmail`; persist `FirmName`.
   - Hash password (`PasswordHash.Encrypt` -> Signature + Salt).
   - `VerificationCode = Guid.NewGuid()`, `IsVerified = false`.
   - Insert; second `Commit` after setting `CreatedBy = user.UserId` (self-ref) + lowercased `EmailId`.
   - Clear plaintext + confirm fields on the response object.
   - `SendEmail(user, isNewUser=true)` -- HTML template `EmailTemplate.UserRegistered` with link `${clientUrl}/verify-email/${userId}?query=${VerificationCode}` (`UserDomain:322-323`).
10. Frontend on success: green toast `"Your registration is successfully done, please verify your email to login."` then `setTimeout(() => router.navigate(['login']), 1000)` (`ts:73-76`).
11. Email verification: visitor clicks link, lands on `/verify-email/:userId?query=:guid`, `PUT /api/UserAuthentication/putemailverification` flips `IsVerified=true`. (Out of R1 scope -- handled by separate verify-email screen.)
12. Login on unverified user triggers auto-resend of verification email (`UserAuthenticationDomain.PostLogin`, lines 124-145). NEW deviates here per design Section 10 item 3 -- explicit "Resend" link on login page.

### OLD form fields per role

| Field | Patient | Adjuster | Applicant Atty | Defense Atty | OLD validation | OLD citation |
|---|---|---|---|---|---|---|
| User Type (`roleId`) | required | required | required | required | required + must be one of 4 external roles | `html:14-22`, `ts:84-101` |
| First Name (`firstName`) | required, max 50 | required, max 50 | hidden | hidden | `requiredValidator() + Validators.maxLength(50)` | `html:23-27`, `ts:96` |
| Last Name (`lastName`) | required (template-implicit), max 50 | same | hidden | hidden | template `formControlName="lastName"`; reactive form binding from `User` model attributes | `html:28-31` |
| Firm Name (`firmName`) | hidden | hidden | required, max 50 | required, max 50 | `requiredValidator() + Validators.maxLength(50)` | `html:33-36`, `ts:87-89` |
| Email (`emailId`) | required, unique, max 100 | same | same | same | server-side: `AddValidation` email-uniqueness; no client regex (template `type="text"`) | `html:37-40`, `UserDomain:71-75` |
| Password (`userPassword`) | required, regex `systemPasswordPattern` | same | same | same | server-side regex (`UserDomain:78-86`) | `html:41-44`, `RegexConstant.cs:15` |
| Confirm Password (`confirmPassword`) | required, must equal `userPassword` | same | same | same | server-side equality (`UserDomain:88-91`) | `html:45-48` |
| T&C link / checkbox | text-only footnote (no checkbox); footnote text is mandatory copy but not a separate form control | same | same | same | none -- click is acceptance | `html:50-52` |

OLD password regex (verbatim):

```regex
^(?=.*[0-9])(?=.*[a-zA-Z])(?=.*[-.!@#$%^&*()_=+/\\'])([a-zA-Z0-9-.!@#$%^&*()_=+/\\']+)$
```

Hidden / system-set fields (set in `setDefaultValues`, `ts:57-68`): `applicationTimeZoneId=1`, `userTypeId=ExternalUser`, `createdBy=1`, `createdOn=Date.now()`, `statusId=Active`, `phoneNumber=0` (literal 0 -- OLD bug, design Section 10 item 6), `oldPassword="null"` (literal string), `isActive=Active`. Most are dropped in NEW (ABP audit fields handle CreatedBy/CreatedOn; `IsActive` is the IdentityUser property; `phoneNumber` becomes nullable + collected later).

### NEW current state

**Backend (post-G2 merge, available on `feat/replicate-old-app-track-identity`):**

- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/IExternalSignupAppService.cs` -- declares `Task RegisterAsync(ExternalUserSignUpDto input)` (NOT `SignupAsync`). Also declares `GetTenantOptionsAsync`, `ResolveTenantByNameAsync`, `GetExternalUserLookupAsync`, `GetMyProfileAsync`, `InviteExternalUserAsync`.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/ExternalUserSignUpDto.cs` -- carries `UserType` (enum), `FirstName?`, `LastName?`, `Email`, `Password`, `ConfirmPassword`, `FirmName?`, `FirmEmail?`, `TenantId?`. Required: `UserType`, `Email`, `Password`, `ConfirmPassword`. The DTO docs cite OLD lines for parity. Strings have `[StringLength]` server-side caps.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/ExternalUserType.cs` -- enum with five values: `Patient=1, ClaimExaminer=2, ApplicantAttorney=3, DefenseAttorney=4, Adjuster=5`. Locked memory (`project_role-model.md`) is "ClaimExaminer is metadata not a role" -- `ClaimExaminer` is retained here as a documented deviation. **Build R1 with the four canonical external roles (Patient, Adjuster, ApplicantAttorney, DefenseAttorney) only**; do NOT surface `ClaimExaminer` in the role dropdown. Cite OLD `Roles.cs:14-17`.
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` -- `[AllowAnonymous]` on `RegisterAsync`. Validates `ConfirmPassword == Password` (mirrors OLD `UserDomain.cs:88`). Validates `FirmName` required for Applicant + Defense Attorney (fixes OLD bug `UserDomain.cs:272` which double-checked `PatientAttorney`). Auto-derives `FirmEmail = Email.ToLower()` when omitted (mirrors OLD `UserDomain.cs:106`). Sets four `IdentityUser` extension properties (next bullet). Creates the right downstream entity (Patient row for Patient role; ApplicantAttorney row for AA; nothing for DA / CE per D-2). Calls `_userManager.CreateAsync` then `AddToRoleAsync` with role-name lookup. Also auto-links pre-existing appointments via `AppointmentApplicantAttorneyEmail` / `DefenseAttorneyEmail`.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationModuleExtensionConfigurator.cs:27-30` -- the four IdentityUser extension props **exist**: `FirmName`, `FirmEmail`, `IsExternalUser`, `IsAccessor`. Verified in this branch (already merged from identity-track via the constants); the registration code path can `user.SetProperty(...)` against these names.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs` -- manual controller exposes `RegisterAsync` at the standard ABP route.

**Frontend (NEW):**

- `angular/src/app/account/` -- absent (`ls` returns no files).
- `angular/src/app/register/` -- absent.
- The only ExternalSignup-adjacent UI is `angular/src/app/external-users/components/invite-external-user.component.{ts,html}`, which is the admin invite flow (`InviteExternalUserAsync`), not the public register form.
- Auto-generated proxy under `angular/src/app/proxy/external-signups/` exposes `registerAsync(input: ExternalUserSignUpDto)` -- consume this in R1, do NOT hand-roll the HTTP call.

**Design / parity docs (already written):**

- `docs/design/external-user-registration-design.md` -- **canonical contract** for R1. Locks the route (`/account/register`), the auth-shell variant 1 chrome, the field tables (mirroring OLD's sr-only label / placeholder pairs), the password-policy mapping (`IdentitySettingNames.Password`: RequireDigit + RequireNonAlphanumeric + RequiredLength 8), the 8 strict-parity exceptions.
- `docs/design/terms-and-conditions-design.md` -- T&C is a footnote-link modal (no checkbox required); content is per-tenant via `branding.termsAndConditions`. R1 must wire the link the same way OLD does.
- `docs/parity/external-user-registration.md` -- parity audit (gap table, regex translation, role-mapping rules).

### Implementation plan -- R1

Files to create / touch:

1. **`angular/src/app/account/register/register.component.ts`** (new). Standalone component; reactive form; injects `ExternalSignupService` proxy (auto-generated), `Router`, `ToasterService`, `ConfigStateService`. Posts via the typed proxy method.
2. **`angular/src/app/account/register/register.component.html`** (new). Auth-shell variant 1 chrome (matches design Section 2 ASCII layout). Role select first; `*ngIf="!isAttorney"` for First/Last row; `*ngIf="isAttorney"` for Firm Name. Always-visible Email / Password / Confirm. Disabled Sign-Up button until `formGroup.valid`. Footnote with T&C link. Card-footer Sign-In link.
3. **`angular/src/app/account/register/register.component.scss`** (new). Per `_design-tokens.md` -- consumes `--brand-primary`, `--bg-card`, `--shadow-login-form`, etc.
4. **`angular/src/app/account/account.routes.ts`** (new). Route definitions for `/account/register` and (out of R1 scope but provisioned) `/account/verify-email/:userId`.
5. **`angular/src/app/account/components/terms-and-conditions-modal.component.ts`** (new). Standalone modal with content from `branding.termsAndConditions` per `terms-and-conditions-design.md`. Trigger from the footnote link.
6. **`angular/src/app/app.routes.ts`** (touch). Lazy-load `account` route group. Update the existing login-page footer "Don't have an account?" link to route to `/account/register`.
7. **`angular/src/app/account/register/role-mapping.ts`** (new). Tiny pure module mapping the 4-option Angular select value to `ExternalUserType` enum. Whitelist Patient / Adjuster / ApplicantAttorney / DefenseAttorney; do NOT include `ClaimExaminer` (memory `project_role-model.md`).
8. **`src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`** (touch). Add localization keys for the success toast verbatim from OLD: `"RegistrationSuccessToast": "Your registration is successfully done, please verify your email to login."`. Plus error keys `RegistrationConfirmPasswordMismatch`, `RegistrationFirmNameRequired`, `RegistrationEmailAlreadyUsed`. (Server-side catches use `L["..."]`; the toast is bound on the client with `| abpLocalization`.)
9. **`src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs`** (touch -- only if password-policy not yet configured per design doc Section 3). Configure `IdentitySettings` defaults via `Configure<AbpIdentityOptionsExtended>` or the standard `IdentityOptions` -- set `RequireDigit=true, RequireNonAlphanumeric=true, RequireLowercase=false, RequireUppercase=false, RequiredLength=8`.

Tests (per `.claude/rules/rpe-workflow.md` -- test-after for UI components):

- **`test/HealthcareSupport.CaseEvaluation.Application.Tests/ExternalSignups/ExternalSignupAppServiceUnitTests.cs`** (extend if exists; the file `ExternalSignupValidatorUnitTests.cs` already exists per glob result):
  - Patient registration -- Email + Password + ConfirmPassword + FirstName + LastName -> Patient row + IdentityUser + Patient role + `IsExternalUser=true`.
  - Adjuster registration -- same shape but no Patient row.
  - ApplicantAttorney with FirmName -> AA row + IdentityUser + AA role + `IsExternalUser=true` + `FirmName` + `FirmEmail` ext-props set.
  - ApplicantAttorney without FirmName -> `UserFriendlyException` with `RegistrationFirmNameRequired`.
  - ApplicantAttorney with explicit `FirmEmail = "intake@firm.com"` -> ext-prop reads back as `"intake@firm.com"` (not auto-derived).
  - ApplicantAttorney with FirmEmail null -> `FirmEmail` ext-prop equals `Email.ToLower()`.
  - Password and ConfirmPassword differ -> `UserFriendlyException` with `RegistrationConfirmPasswordMismatch`.
  - Duplicate email -> `UserFriendlyException` with `RegistrationEmailAlreadyUsed`.
- **Angular**: snapshot-style template test that asserts:
  - 4 options in role dropdown.
  - First/Last show only for Patient + Adjuster.
  - Firm Name shows only for the two attorney roles.
  - Sign Up disabled until valid.
- **`scripts/tests/B6-ExternalSignup.ps1`** -- a similar PowerShell smoke test exists (per glob) -- extend to cover R1.

### Acceptance criteria -- R1

- [ ] `/account/register` renders with auth-shell variant 1 chrome and 4-option role dropdown.
- [ ] Patient / Adjuster role -> First Name + Last Name shown; Firm Name hidden.
- [ ] Applicant Attorney / Defense Attorney role -> Firm Name shown; First / Last hidden.
- [ ] Sign Up button disabled until form is valid.
- [ ] T&C link opens the per-tenant T&C modal (content from `branding.termsAndConditions`).
- [ ] On submit, success toast renders verbatim "Your registration is successfully done, please verify your email to login." Then `setTimeout(..., 1000)` route to `/account/login`.
- [ ] After successful Patient registration: IdentityUser row exists with `IsExternalUser=true` ext-prop, role = Patient, plus a Patient entity row.
- [ ] After successful Applicant Attorney registration: IdentityUser ext-props `IsExternalUser=true, FirmName=<input>, FirmEmail=<input.Email.ToLower() if FirmEmail null else input.FirmEmail>`; ApplicantAttorney row created.
- [ ] Defense Attorney + Claim Examiner / Adjuster: do NOT create a downstream entity (per `ExternalSignupAppService` D-2 docstring) -- IdentityUser + role only.
- [ ] Mismatched ConfirmPassword -> validation modal renders the localized `RegistrationConfirmPasswordMismatch` message; row not persisted.
- [ ] Duplicate email -> validation modal renders the localized `RegistrationEmailAlreadyUsed` message; row not persisted.
- [ ] Attorney role with empty FirmName -> validation modal renders the localized `RegistrationFirmNameRequired` message.
- [ ] Login-page footer "Don't have an account?" link routes to `/account/register`.

### Gotchas / edge cases -- R1

- **Method name on the AppService is `RegisterAsync`, not `SignupAsync`.** The kickoff prompt said `SignupAsync`; the actual interface declares `RegisterAsync(ExternalUserSignUpDto)`. Use the proxy-generated method.
- **`ClaimExaminer` enum value lives in the contract but contradicts the role-model memory.** Hardcode the Angular role lookup to the 4 canonical externals; do NOT iterate `ExternalUserType.values()`.
- **Names are nullable in the DTO.** Per the AppService docstring, FirstName/LastName are not collected on the OLD form for attorneys, but the OLD form DOES collect them for Patient + Adjuster. Mirror exactly: form sends them when role is Patient or Adjuster, omits when attorney. NEW DTO accepts both shapes.
- **OLD's `phoneNumber=0` literal default is an OLD bug.** Per design Section 10 item 6, do NOT port -- send `null`. Phone is collected on first profile edit.
- **OLD's email is `<input type="text">` not `<input type="email">`.** Per strict parity, NEW could match -- but the design doc keeps `<input type="email">` for accessibility (and the regex check is server-side anyway). Confirm: design doc is authoritative; use `type="email"`.
- **T&C is a footnote link, not a checkbox.** OLD `html:50-52` is `<a>` -> popup, no separate form control. Do not add a required checkbox unless explicitly requested -- it would be a UX deviation from OLD.
- **OLD `setvalue()` (`ts:104`) copies `firmName` -> `firstName` on attorney change so the User entity has a non-null FirstName.** This is an OLD workaround for a non-null DB column. NEW does not need it because IdentityUser.Name is nullable -- the AppService passes `FirstName` through verbatim. Skip.
- **Strict-parity exception 8 in design doc**: OLD's `Doctor.png` logo -> NEW reads `var(--brand-logo-url)`. Do not hardcode.
- **Verification email content + link target are out of R1 scope.** Stage 1 covers the registration submit only; the verify-email screen is its own task. The AuthServer Razor email-verify gate is G4 (already in Stage 0 task list).
- **OLD form lacks `phoneNumber` capture entirely.** Adding a phone field on R1 is a deviation. Defer to future profile-completion step.
- **No internal-role registration via this surface.** The 3 internal roles (admin, Staff Supervisor, Clinic Staff, IT Admin, Doctor, Receptionist) come in via tenant-admin invite, not the public form -- mirrors OLD `UserDomain.AddInternalUser` (`UserDomain:281-312`), which is server-side admin-only.
- **Login-page link to register**: the OLD `login.component.html:35` has `Don't have an account yet? <a (click)="gotoSignup()">`. NEW currently has no public login page Angular component (the AuthServer Razor login is a parallel concern in G4). Add the link to whichever surface NEW lands on as part of R1.

---

## R2 -- IsPatientAlreadyExist on initial booking

### OLD source (verbatim citations)

- `PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs:199-220` -- `Add(Appointment appointment)` (initial-booking entry path):

  ```csharp
  public Appointment Add(Appointment appointment)
  {
      try
      {
          bool isPatientExist = IsPatientRegistered(appointment, out int patientId);

          #region Check Patient Exist or Not
          if (isPatientExist)
          {
              appointment.PatientId = patientId;
              appointment.Patient = null;
              appointment.IsPatientAlreadyExist = true;
          }
          else
          {
              appointment.Patient.PatientId = 0;
              AppointmentRequestUow.RegisterNew<Patient>(appointment.Patient);
              appointment.PatientId = appointment.Patient.PatientId;
              appointment.IsPatientAlreadyExist = false;
          }
          #endregion
  ```

- `PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs:732-780` -- `IsPatientRegistered` (the dedup function):

  ```csharp
  private bool IsPatientRegistered(Appointment appointment, out int patientId)
  {
      int counter = 0;
      bool isPatientRegistered = false;
      var patientList = AppointmentRequestUow.Repository<vPatientDetail>().All().Where(x =>
          x.LastName == appointment.Patient.LastName ||
          x.PhoneNumber == appointment.Patient.PhoneNumber ||
          x.SocialSecurityNumber == appointment.Patient.SocialSecurityNumber ||
          x.DateOfBirth == appointment.Patient.DateOfBirth ||
          x.Email == appointment.Patient.Email ||
          appointment.AppointmentInjuryDetails.Select(t => t.ClaimNumber).ToList().Contains(x.ClaimNumber)).ToList();
      patientId = 0;

      foreach (var item in patientList)
      {
          if (item.LastName == appointment.Patient.LastName) counter++;
          if (item.SocialSecurityNumber == appointment.Patient.SocialSecurityNumber) counter++;
          if (item.Email == appointment.Patient.Email) counter++;
          if (item.PhoneNumber == appointment.Patient.PhoneNumber) counter++;
          if (item.DateOfBirth == appointment.Patient.DateOfBirth) counter++;
          if (appointment.AppointmentInjuryDetails.Select(t => t.ClaimNumber).ToList().Contains(item.ClaimNumber)) counter++;

          if (counter >= 3) { patientId = item.PatientId; isPatientRegistered = true; break; }
          counter = 0;
      }
      return isPatientRegistered;
  }
  ```

### OLD behavior (numbered)

1. Booker submits an appointment via `POST /api/Appointments`. The appointment carries an inline `Patient` object (firstName, lastName, DOB, SSN, phone, email, etc.) and a list of `AppointmentInjuryDetails` (each with a `ClaimNumber`).
2. `Add()` first calls `IsPatientRegistered(appointment, out int patientId)` (`line 203`).
3. `IsPatientRegistered` runs an `OR`-prefiltered query against `vPatientDetail` view: pull all rows matching ANY of LastName / PhoneNumber / SocialSecurityNumber / DateOfBirth / Email / any-injury-row's-ClaimNumber.
4. For each row in the prefilter, count how many of the **6 fields** match exactly. The 6 are:
   - `LastName`
   - `SocialSecurityNumber`
   - `Email`
   - `PhoneNumber`
   - `DateOfBirth`
   - `ClaimNumber` (matches if ANY of `appointment.AppointmentInjuryDetails[].ClaimNumber` equals the candidate row's `ClaimNumber`)
5. **Threshold: `counter >= 3`** -- 3-of-6 exact matches. Counter resets between rows. First row to clear 3-of-6 wins; loop breaks.
6. **Comparison semantics**: equality only -- no `Trim()`, no `ToLower()`, no digit-stripping. Strict bug-as-design unless the source rows happen to be normalized at write time (they are not; the `Patient` model has free-text strings). Replicating OLD verbatim means case-sensitive, untrimmed compares.
7. If a match: `appointment.PatientId = patientId; appointment.Patient = null; appointment.IsPatientAlreadyExist = true;` (`line 208-210`).
8. Else: insert the inline patient as new (`AppointmentRequestUow.RegisterNew<Patient>`); `appointment.IsPatientAlreadyExist = false;` (`line 213-217`).
9. Both branches continue down the same `Add` flow (set status, due-date, conf number, etc.).

### NEW current state

**Dedup matching exists but signal does not propagate to `Appointment.IsPatientAlreadyExist`:**

- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/IPatientRepository.cs:76-84` -- `GetDeduplicationCandidatesAsync(tenantId, lastName, dob, phone, email, ssn, claimNumbers)` returns the prefilter list (matches OLD `OR`-prefilter semantics). Implemented in `EfCorePatientRepository:127`.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentBookingValidators.cs:128` -- `IsPatientDuplicate(incoming, candidate)` is the 3-of-6 threshold predicate (pure helper).
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/PatientMatching.cs` -- `Normalise` (trim + lowercase), `NormalisePhone` (strip non-digit), `NormaliseSsn` (strip non-digit). **NEW normalises**, OLD does not. This is a known bug-fix; not strict parity but acceptable per the audit.
- `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:93-251` -- `GetOrCreatePatientForAppointmentBookingAsync`:
  - Line 100-108: email-fast-path lookup; returns existing if email matches.
  - Line 121-132: calls `_patientRepository.GetDeduplicationCandidatesAsync(...)` (claimNumbers passed as null per the audit-doc commitment -- AppointmentInjuryDetail isn't available at Patient creation time).
  - Line 134-167: iterates candidates and applies `AppointmentBookingValidators.IsPatientDuplicate`. **On 3-of-6 match, returns `matchedWithNav` mapped to `PatientWithNavigationPropertiesDto`**.
  - Line 170-238: if no dedup match, creates IdentityUser (`CaseEvaluationConsts.AdminPasswordDefaultValue` -- known issue Q-12), grants Patient role, calls `_patientManager.FindOrCreateAsync` (NEW's own 3-of-6 with FirstName + ZipCode + LastName + DOB + SSN + Phone -- different field set, kept as safety net per code comment).
  - Line 241-250: returns `PatientWithNavigationPropertiesDto`.
  - **The dedup signal (existing-vs-newly-created) is NOT exposed to the caller.** Both branches return the same `PatientWithNavigationPropertiesDto` shape with no flag distinguishing them.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:557-762` -- `CreateAsync` -> `CreateAppointmentInternalAsync`:
  - Line 613: `_patientRepository.FindAsync(input.PatientId)` -- expects the caller to have already resolved Patient. Booker workflow: client first calls `GetOrCreatePatientForAppointmentBookingAsync(input)`, gets back `PatientWithNavigationPropertiesDto`, then sends `PatientId` on `AppointmentCreateDto`.
  - Line 696-723: `_appointmentManager.CreateAsync(...)` constructs the Appointment. The Manager constructor sets 11 fields but NOT `IsPatientAlreadyExist` (per `Domain/Appointments/CLAUDE.md` Business Rule 4: "constructor sets all 11 settable fields it accepts; the remaining 3 (`InternalUserComments`, `AppointmentApproveDate`, `IsPatientAlreadyExist`) are intentionally not constructor params").
  - Line 728-731: post-create mutations set `PatientEmail`, `ApplicantAttorneyEmail`, `DefenseAttorneyEmail`, `ClaimExaminerEmail`. **`IsPatientAlreadyExist` is NOT set here.** It defaults to `false` (the entity's default `bool`).
- Existing writers of `IsPatientAlreadyExist`:
  - `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.Approval.cs:96` -- `appointment.IsPatientAlreadyExist = false;` on approve (a reschedule-approval reset).
  - `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentRescheduleCloner.cs:96` -- `clone.IsPatientAlreadyExist = source.IsPatientAlreadyExist;` (cloning, not a producer).
  - **No producer at booking time.** This is the gap.

### Implementation plan -- R2

**Recommended design: Option A (extend `PatientWithNavigationPropertiesDto` with an `IsExisting` bool).**

Files to touch:

1. **`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/PatientWithNavigationPropertiesDto.cs`** -- add `public bool IsExisting { get; set; }` with a docstring citing OLD `AppointmentDomain.cs:210, 217`. Default `false`.
2. **`src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs`** -- in `GetOrCreatePatientForAppointmentBookingAsync`, set `result.IsExisting = true` on the four return paths that resolve an existing patient:
   - Line 105-108 (email fast-path).
   - Line 161-166 (3-of-6 dedup match).
   - The `_patientManager.FindOrCreateAsync` path (line 212-234) returns `(patient, wasFound)`; map `wasFound` -> `IsExisting` on the final return at line 241-250.
   - Set `result.IsExisting = false` in the truly-new branch (when `wasFound == false`).
3. **`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs`** -- add `public bool IsPatientAlreadyExist { get; set; }`. Cite OLD `AppointmentDomain.cs:210, 217`. Default `false`. Mark with a docstring that the Angular booking form must populate this from the `IsExisting` flag of the `PatientWithNavigationPropertiesDto` returned by the prior `GetOrCreatePatientForAppointmentBookingAsync` call.
4. **`src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:728-731`** -- after the existing `appointment.PatientEmail = input.PatientEmail` block (just after line 731), add `appointment.IsPatientAlreadyExist = input.IsPatientAlreadyExist;`. Place this BEFORE the `await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(...))` so the event reflects the final state.
5. **`angular/src/app/proxy/...`** -- regenerate via `abp generate-proxy` (per branch CLAUDE.md "Never edit `angular/src/app/proxy/`"). Both DTO additions flow through.
6. **Booking form Angular component** (when wired in B1 / V1 stage): pass the `isExisting` flag from `getOrCreatePatientForAppointmentBookingAsync`'s response into the subsequent `createAsync` call's `isPatientAlreadyExist` field. The shape is a single boolean handed across two REST calls -- minimal surface area.
7. **`src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs`** -- the `PatientWithNavigationProperties -> PatientWithNavigationPropertiesDto` Mapperly map needs `IsExisting` ignored (server sets it after mapping). Add `[MapperIgnoreTarget(nameof(PatientWithNavigationPropertiesDto.IsExisting))]` per ABP convention.

Tests (per `.claude/rules/rpe-workflow.md` -- `tdd` for booking domain logic):

- **`test/HealthcareSupport.CaseEvaluation.Application.Tests/Patients/PatientsAppServiceTests.cs`**:
  - `GetOrCreatePatientForAppointmentBookingAsync_WhenEmailMatchesExistingPatient_ReturnsIsExistingTrue`.
  - `GetOrCreatePatientForAppointmentBookingAsync_WhenDedupCandidateMatches3Of6_ReturnsIsExistingTrue`.
  - `GetOrCreatePatientForAppointmentBookingAsync_WhenNoMatch_CreatesNewPatientAndReturnsIsExistingFalse`.
- **`test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/AppointmentsAppServiceTests.cs`**:
  - `CreateAsync_WhenInputHasIsPatientAlreadyExistTrue_PersistsTrueOnAppointment` -- assert via `_appointmentRepository.GetAsync(id).IsPatientAlreadyExist == true`.
  - `CreateAsync_WhenInputHasIsPatientAlreadyExistFalse_PersistsFalseOnAppointment`.
  - `CreateAsync_WhenInputOmitsIsPatientAlreadyExist_DefaultsToFalse` -- regression for the gap.

### Acceptance criteria -- R2

- [ ] `PatientWithNavigationPropertiesDto.IsExisting` is true on the email-fast-path branch of `GetOrCreatePatientForAppointmentBookingAsync`.
- [ ] `PatientWithNavigationPropertiesDto.IsExisting` is true on the 3-of-6 dedup-match branch.
- [ ] `PatientWithNavigationPropertiesDto.IsExisting` is true on the FindOrCreate `wasFound=true` safety-net branch.
- [ ] `PatientWithNavigationPropertiesDto.IsExisting` is false on the truly-new-patient branch.
- [ ] `AppointmentCreateDto.IsPatientAlreadyExist` is included in the proxy after `abp generate-proxy`.
- [ ] After `AppointmentsAppService.CreateAsync(input)` with `input.IsPatientAlreadyExist=true`, the persisted Appointment has `IsPatientAlreadyExist=true`.
- [ ] After `AppointmentsAppService.CreateAsync(input)` with `input.IsPatientAlreadyExist=false`, the persisted Appointment has `IsPatientAlreadyExist=false`.
- [ ] The reschedule-approval reset path (`AppointmentsAppService.Approval.cs:96`) still sets `IsPatientAlreadyExist=false` correctly (regression check).
- [ ] All existing booking tests still pass.

### Gotchas / edge cases -- R2

- **The signal is set in two places now (booking creation AND reschedule approval).** Both are correct. The audit's previous read that the field was "only written during reschedule approval" is accurate -- this fix adds the missing producer at create time.
- **Approve-time reset behavior**: `AppointmentApprovalValidator.cs:106` -- `appointment.IsPatientAlreadyExist && input.OverridePatientMatch` is the gate that flips `IsPatientAlreadyExist` to `false` on approval. This stays. R2 only affects the create-time write.
- **Trust boundary**: a malicious external booker could POST `IsPatientAlreadyExist=true` even when no dedup match was found. **Mitigation**: server-side, after mapping, recompute by checking whether `input.PatientId` corresponds to a patient that pre-existed before this transaction. Cheaper alternative: trust the client (since `PatientId` is itself the resolved match). Recommend trusting -- the client already had to fetch a real `PatientId` from `GetOrCreatePatientForAppointmentBookingAsync`, and `IsPatientAlreadyExist` is observable but non-security-critical (it only changes the approval gate's "Override patient match" path, which still requires `Appointments.Edit` permission).
- **Field-name mismatch with OLD's dedup**: NEW's `Patient` entity does NOT carry `ClaimNumber` (lives on `AppointmentInjuryDetail`). Per the audit doc, `claimNumbers` is passed as `null` to `GetDeduplicationCandidatesAsync` and the predicate counts the remaining 5 fields with a still-3 threshold. **This is a strict-parity regression**: OLD bookers could match 3-of-6 including ClaimNumber; NEW needs 3-of-5. Document this as an open parity gap; do not block R2 on it. Future fix: pass `input.AppointmentInjuryDetails.Select(x => x.ClaimNumber).ToList()` once `GetOrCreatePatientForAppointmentBookingAsync` is callable from the booking flow with injury details available.
- **NEW normalisation differs from OLD.** OLD compares verbatim (case-sensitive, no trim); NEW normalises (`PatientMatching.Normalise` -> trim + lowercase). This catches more matches than OLD. Per `docs/parity/external-user-registration.md`-style precedent, this is a clear OLD bug, fixed for correctness. Add a `// PARITY-FLAG` comment if Adrian wants strict OLD behavior.
- **`PatientWithNavigationPropertiesDto.IsExisting`** is NOT mapped from a Domain entity field -- it's a flow-state signal computed by the AppService. Mapperly must ignore it; the AppService sets it explicitly after `Map`.
- **Two-call API shape**: client makes `getOrCreatePatientForAppointmentBookingAsync` -> reads `IsExisting` -> sends `IsPatientAlreadyExist` on `createAsync`. This is two REST calls. Alternative one-call shape (a sidecar option B) was rejected because it would break the existing booking flow's explicit Patient/Appointment two-step shape; the AppService boundaries are clean and the client cost is one boolean read.
- **Option B (sidecar return DTO from a dedup service)** rejected: would require a new method on `IPatientsAppService` (e.g. `DedupePatientForBookingAsync`) returning `(PatientWithNavigationPropertiesDto, bool)`. Two reasons against: (1) it duplicates the existing get-or-create surface, requiring the booking form to know about three patient endpoints (`get-by-email`, `get-or-create`, `dedupe`); (2) it doesn't match OLD's data flow, where the dedup result was a private detail of `Add()` and the signal landed on the appointment row (not a separate object). Option A keeps the OLD structure: `IsPatientAlreadyExist` sits on the appointment, derived from a flag the client already knows from the patient resolution.
- **AppointmentManager.CreateAsync does not accept `IsPatientAlreadyExist`** (`Domain/Appointments/CLAUDE.md` Business Rule 4). Per existing convention, the field is set via direct entity mutation post-create (the same pattern used by `appointment.PatientEmail = ...` at line 728-731). Do NOT change `AppointmentManager.CreateAsync`'s signature.

---

## Cross-task notes

- Both R1 and R2 depend on the G2 merge (identity branch into domain branch) so `ExternalSignupAppService` and `IExternalSignupAppService` are present in this worktree. Do not start before G2.
- The proxy regeneration step is shared: one `abp generate-proxy` run after R2's DTO change picks up both new fields if R1 has additionally landed.
- HIPAA: do NOT log `IsPatientAlreadyExist` at INFO level alongside `PatientId` -- the pair would correlate to a person across appointments. Use Trace / Debug only.
- Test data: per `.claude/rules/test-data.md`, all test patient rows must use synthetic data. `IsPatientAlreadyExist` regression tests need at least 2 synthetic patient seeds.
