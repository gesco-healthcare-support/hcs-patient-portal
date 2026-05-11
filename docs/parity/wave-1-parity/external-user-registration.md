---
feature: external-user-registration
old-source:
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\User\UsersController.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\UserAuthenticationController.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\user-add.component.ts
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\login\verify-email\verify-email.component.ts
old-docs:
  - socal-project-overview.md (lines 119-127, 221-227, 295-349)
  - data-dictionary-table.md (Users table, Roles table)
  - socal-api-documentation.md (UserAuthentication, Users sections)
audited: 2026-05-01
re-verified: 2026-05-03
status: in-progress
priority: 1
depends-on:
  - external-user-login        # shares VerificationCode + IsVerified with login
  - external-user-forgot-password  # reuses VerificationCode field for reset token
---

# External user registration (+ email verification)

## Purpose

A new external user (Patient, Adjuster, Applicant Attorney, Defense Attorney) self-registers via a public form, receives a verification email, clicks the link to confirm their email, and can then log in. This audit covers two endpoints together because OLD reuses `VerificationCode` field for both the verification link and the eventual password reset (single-token reuse) -- the registration audit is incomplete without the verification step.

## OLD behavior

### Registration form (UI)

OLD uses a single `user-add` component (`components/user/users/add/user-add.component.ts`) for BOTH external registration and IT-Admin-driven internal user creation. The form distinguishes the two via `UserType` enum:

- `UserType.ExternalUser` -- public registration, user enters password
- `UserType.InternalUser` -- IT Admin creates, backend auto-generates password

Form fields (external user, per role):

| Field | All roles | Attorneys only (Applicant + Defense) | Notes |
|-------|-----------|--------------------------------------|-------|
| EmailId | required | required | Lowercased on save |
| UserPassword | required | required | Must match password regex |
| ConfirmPassword | required | required | Must equal UserPassword |
| RoleId | required | required | One of: Patient, Adjuster, ApplicantAttorney, DefenseAttorney |
| FirstName, LastName | required | required | |
| DateOfBirth | required | required | Must be in the past |
| PhoneNumber | required | required | |
| FirmName | -- | required | Attorney-only validation in `UserDomain.CommonValidation` |
| FirmEmail | -- | auto-populated | Set to `EmailId.ToLower()` in `UserDomain.Add` |

NOTE: OLD uses role name "Patient Attorney" -- in NEW, this role is renamed **Applicant Attorney** per `_old-docs-index.md` naming overrides.

### Backend flow (`POST /api/Users` -> `UserDomain.Add`)

1. **Validation (`AddValidation`):**
   - Email uniqueness: must not match existing user where `StatusId != Status.Delete`
   - Password regex: `RegexConstant.systemPasswordPattern` (exact pattern not yet captured -- TO VERIFY during impl; check `Infrastructure/Constants/RegexConstant.cs`)
   - `UserPassword == ConfirmPassword`
   - For Attorney roles: `FirmName` must not be empty
2. **Persistence (external user path):**
   - Hash password via `IPasswordHash.Encrypt(...)` -> `(Signature, Salt)`
   - Generate `VerificationCode = Guid.NewGuid()`
   - Set `IsVerified = false`
   - Insert via `UserUow.RegisterNew<User>(user)` + `Commit()`
   - Update `CreatedBy = user.UserId`, `CreatedOn = DateTime.Now`, `EmailId = lowercased`
   - Second `Commit()` for the update
   - Clear `UserPassword` and `ConfirmPassword` from response
3. **Email send:**
   - Subject: `"Your have registered successfully - Patient Appointment portal"` (typo "Your" preserved verbatim from OLD)
   - Body: `EmailTemplate.UserRegistered` HTML template
   - URL embedded: `{clientUrl}/verify-email/{userId}?query={verificationCode}`

### Email verification flow (`PUT /api/UserAuthentication/putemailverification`)

- **Request body:** `{ "UserId": int, "VerificationCode": string-as-guid }`
- **Validation (`PutEmailVerificationValidation`):**
  - Parse `VerificationCode` as Guid -- if not a valid Guid, return "Invalid activation link"
  - Find user by `UserId` -- if not found, return "User not exist"
  - Compare stored `user.VerificationCode == provided VerificationCode` -- if mismatch, return "Invalid activation link"
- **Action:** Set `user.IsVerified = true`, save. (Note: `VerificationCode` is NOT cleared -- it remains in the DB. This matters because login resets it on subsequent unverified login attempts, and forgot-password overwrites it. So the stale code persists harmlessly until reused.)
- **Response:** the User entity (200 OK).

### Critical OLD behaviors to preserve

- **Email lowercase normalization:** EmailId is lowercased on registration AND on lookup (login, forgot-password).
- **Soft delete via `StatusId`:** users with `StatusId == Status.Delete` are excluded from email-uniqueness check. Re-registration of a soft-deleted email is allowed.
- **Login auto-resends verification:** if a user attempts login while `IsVerified == false`, login regenerates `VerificationCode` (if null) and re-sends the verification email; returns `FailedLogin = true` with message "We have sent a verification link to your registered email id, please verify your email address to login." This is implemented in `UserAuthenticationDomain.PostLogin` lines 124-145. **No separate "resend verification" endpoint exists** in OLD.
- **Login auto-resend re-uses the existing VerificationCode** if non-null, otherwise generates a new one. So a user with a still-valid code who clicks the original email after a failed login will still verify successfully.
- **Email verification does NOT log the user in.** After verification, user is redirected to login screen and must enter credentials again.
- **`VerificationCode` is shared with forgot-password flow** -- forgot-password generates a new `VerificationCode` (overwriting any pending verification code), and password reset clears `VerificationCode = null` after use. This means: a user who initiates forgot-password before completing registration verification will have their verification code overwritten; they need to re-trigger verification via the auto-resend on login.

## OLD code map

### Backend (.NET / C#)

| Layer | File | Purpose |
|-------|------|---------|
| API controller | `PatientAppointment.Api/Controllers/Api/User/UsersController.cs` | `POST /api/Users` -> `Domain.User.Add(user)` |
| API controller | `PatientAppointment.Api/Controllers/Api/Core/UserAuthenticationController.cs` | `PUT /api/UserAuthentication/putemailverification` -> `UserAuthenticationDomain.PutEmailVerification(verifyUser)` |
| Domain | `PatientAppointment.Domain/UserModule/UserDomain.cs` | `Add`, `AddValidation`, `SendEmail` (registration), `AddInternalUser` (admin-driven) |
| Domain | `PatientAppointment.Domain/Core/UserAuthenticationDomain.cs` | `PutEmailVerification`, `PutEmailVerificationValidation`; also `PostLogin` (with auto-resend) |
| Models | `PatientAppointment.Models/...` | `User`, `VerifyUser`, `UserCredentialViewModel`, `vEmailSenderViewModel` |
| DB entities | `PatientAppointment.DbEntities/...` | `User` entity (with `VerificationCode`, `IsVerified`, `Salt`, `Password` fields); `Roles`, `RoleUserType`, `UserType` enums |
| Email templates | `EmailTemplate.UserRegistered`, `EmailTemplate.AddInternalUser` | HTML template stored in app config -- TO LOCATE during impl |
| Constants | `Infrastructure/Constants/RegexConstant.cs` -- `systemPasswordPattern` | TO READ during impl for exact regex |
| Validation messages | `Infrastructure/ValidationMessages/ValidationFailedCode.*` | All user-facing error strings (UserValidation, PasswordPatternValidation, ConfirmPasswordValidation, FirmNameValidation, etc.) -- TO READ during impl |

### Frontend (Angular)

| Component | File | Purpose |
|-----------|------|---------|
| Registration form | `patientappointment-portal/src/app/components/user/users/add/user-add.component.{ts,html}` | The signup UI; handles both external public + internal admin-driven creation |
| Verify email | `patientappointment-portal/src/app/components/login/verify-email/verify-email.component.{ts,html}` | Catches `/verify-email/{userId}?query={code}` URL, calls PUT endpoint |
| Login service | `patientappointment-portal/src/app/components/login/login.service.ts` | Wraps the auth-related HTTP calls including verify-email |
| User models | `patientappointment-portal/src/app/database-models/user.ts`, `v-user-record.ts`, `v-user.ts` | TypeScript shapes |

## NEW current state

### Backend

- **No `IUsersAppService` or external-user registration AppService exists.** ABP provides default user CRUD via `IIdentityUserAppService` (admin-only) and registration via `IAccountAppService.RegisterAsync` (public).
- **`src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUsersDataSeedContributor.cs`** -- seeds initial internal users (Clinic Staff / Staff Supervisor / IT Admin presumably; TO VERIFY).
- **`src/HealthcareSupport.CaseEvaluation.Domain/Identity/ChangeIdentityPasswordPolicySettingDefinitionProvider.cs`** -- customizes ABP's password policy. TO READ during impl to verify it matches OLD's `systemPasswordPattern`.
- **No external-role-aware registration logic.** ABP's default `RegisterAsync` does NOT accept role selection or attorney-specific fields like `FirmName`.

### Frontend

- **No registration / login / verify-email components in `angular/src/app/`.** Only background SVGs at `angular/src/assets/images/login/`.
- ABP Commercial provides login UI via the AuthServer Razor Pages app (separate from the Angular SPA). Login redirects out to AuthServer, then back with token.
- Email verification redirect URL: ABP default is `/Account/EmailConfirmation?userId={guid}&confirmationToken={token}`.

## Gap analysis

3-column gap table. Severity: **B** = blocker, **I** = important, **C** = cosmetic.

| OLD | NEW (current state) | Gap | Sev |
|-----|----------------------|-----|-----|
| Public registration form with role selection (4 external roles) | ABP `IAccountAppService.RegisterAsync` accepts only basic fields, no role selection | Build a custom `IExternalUserRegistrationAppService` that wraps ABP `UserManager` to create user + assign role | B |
| Attorney-only fields (FirmName, FirmEmail) | Not present in NEW | Add fields to a custom registration DTO; persist on user-extra entity (or extend ABP `IdentityUser` via `IObjectExtensionManager`) | B |
| Email lowercase normalization on save + lookup | ABP normalizes via `LookupNormalizer`; default is uppercase normalization for lookup | Verify ABP's normalization matches OLD's lowercase semantics; if not, override `ILookupNormalizer` | I |
| `IsVerified` flag drives login + auto-resend | ABP setting `IdentitySettingNames.User.IsEmailConfirmationRequiredForLogin` toggles login block; ABP DOES NOT auto-resend on login attempt | Either (a) accept ABP behavior + add explicit "resend verification" endpoint with UI link on login screen, or (b) override `SignInManager` to auto-resend. **Recommend (a)** -- simpler, more standard UX. | I |
| `VerificationCode` GUID stored in user record | ABP uses cryptographic data-protection tokens generated on demand, not stored | None (ABP improvement); the URL shape changes from `?query={guid}` to `?confirmationToken={signed-token}` | I |
| Email verification URL: `/verify-email/{userId}?query={code}` | ABP default: `/Account/EmailConfirmation?userId={guid}&confirmationToken={token}` | New Angular `verify-email` component handles ABP URL format. Mount at `/verify-email/...` and translate to ABP's API call. **Alternative:** customize ABP's email template to point to Angular SPA route. | I |
| Email verification URL uses int `userId` | ABP uses Guid `userId` | URL shape change is unavoidable (ABP user IDs are Guid) | I |
| Email verification does NOT auto-login | ABP's default likewise does not | None | -- |
| Email subject: `"Your have registered successfully - Patient Appointment portal"` (verbatim, typo preserved) | ABP default email subject | Customize ABP email template to match OLD subject + body. Note: typo "Your" is in OLD; **NEW should fix** to "You have registered successfully - Patient Appointment portal" since stakeholder copy review hasn't happened. **TO CONFIRM with Adrian.** | C |
| Soft delete via `StatusId == Status.Delete` allows re-registration of email | ABP uses `IsDeleted` (soft delete via `ISoftDelete`); query filter excludes deleted by default; new registration would conflict on email uniqueness due to query filter | Verify ABP's default behavior allows re-registration of soft-deleted email. If not, override or use `IDataFilter<ISoftDelete>` toggle. **Likely works out of the box but TO VERIFY.** | I |
| Validation message strings are loaded from `ApplicationUtility.GetValidationMessage(ValidationFailedCode.X)` | ABP uses `IStringLocalizer` + JSON resource files | Port the validation message keys to `src/.../Domain.Shared/Localization/CaseEvaluation/en.json` | I |
| Registration form HTML/CSS layout | NEW Angular has no registration component | Build new Angular component matching OLD layout. **Branding/theming hooks must be present from day 1** (see Branding section below). | B |

## Internal dependencies surfaced

These OLD internal-user features touch external-user registration. **Pausing for Adrian to expand scope before audit goes deeper into them.**

- **Initial internal user seeding** -- `InternalUsersDataSeedContributor.cs` creates the IT Admin who later approves users. Need to verify the seed shape matches OLD's expected internal users (3 roles).
- **Role enum / `RoleUserType` mapping** -- OLD has `Roles` enum (PatientAttorney/DefenseAttorney/Patient/Adjuster/ClinicStaff/StaffSupervisor/ITAdmin) AND `UserType` enum (External/Internal). The relationship is in the `RoleUserTypes` table. NEW must replicate this two-tier classification (or use ABP roles + a custom `IsExternal` claim/extension property). **Decision needed:** ABP roles natively don't differentiate external vs internal; we either add a custom property to `IdentityUser` or add a parallel `RoleUserType` mapping.
- **IT Admin user-management UI** -- the OLD `UserDomain.AddInternalUser` path is invoked by IT Admin via the same `POST /api/Users` endpoint. NEW likely uses ABP's built-in admin UI (`/identity/users`) which doesn't auto-generate passwords or send welcome emails. **Out of scope for external registration audit -- separate audit slice.**

**Ask Adrian:** confirm scope expansion for `InternalUsersDataSeedContributor` review (small) vs `IT Admin user management` (separate audit slice).

## Branding/theming touchpoints

Per `_old-docs-index.md` branding constraint: **no Branding table in OLD; branding hard-coded in Angular templates.** For registration specifically:

- **Email subject lines** ("Patient Appointment portal" string is the doctor's clinic identifier in OLD): hard-coded in `UserDomain.SendEmail` and `UserAuthenticationDomain.PostLogin` lines 129. **Parameterize as `{ClinicName}` template token in NEW** so multi-doctor extension just swaps the token value.
- **Email template HTML** (`EmailTemplate.UserRegistered`, `EmailTemplate.AddInternalUser`): contain logo, colors, footer copy. **Parameterize all branding in template:** logo URL, primary/secondary colors (CSS), clinic name, support email, support phone.
- **Verification URL domain**: OLD reads `applicationUrl.clientUrl` from server settings. **In NEW, this becomes a per-tenant config** -- the URL host/path may differ per doctor's site.
- **Registration form copy** ("Welcome to {Clinic}", "By registering you agree to..."): currently OLD has these as hard-coded HTML. Move to localization JSON keyed by clinic-tenant.

Output during impl: `docs/parity/_branding.md` will collect all branding touchpoints across features, with a single source of truth (config schema + CSS variable list).

## Replication notes

### ABP-specific wiring

- **Registration AppService** -- new `IExternalUserRegistrationAppService` in `Application.Contracts/ExternalUserRegistration/`. Pattern:
  ```csharp
  Task<UserDto> RegisterAsync(ExternalUserRegistrationDto input);
  ```
- **DTO naming** per branch CLAUDE.md: `ExternalUserRegistrationCreateDto` (the request shape). Avoid `CreateUpdate{Entity}Dto`.
- **AppService impl** in `Application/ExternalUserRegistration/ExternalUserRegistrationAppService.cs`:
  - Extends `CaseEvaluationAppService`
  - **MUST add `[RemoteService(IsEnabled = false)]`** -- otherwise ABP exposes duplicate route alongside the manual controller
  - Calls `UserManager.CreateAsync(user, password)` then `UserManager.AddToRoleAsync(user, roleName)`
  - For attorney roles, persists FirmName/FirmEmail via either: (a) `IObjectExtensionManager` extension property on `IdentityUser`, or (b) parallel `ApplicantAttorneyProfile` entity FK'd to user. **Recommend (b)** for cleaner separation.
- **Manual controller** in `HttpApi/Controllers/ExternalUserRegistration/` extending `AbpController, IExternalUserRegistrationAppService` with route `[Route("api/app/external-user-registration")]`.
- **Mapper** -- Riok.Mapperly partial class extending `MapperBase<ExternalUserRegistrationCreateDto, IdentityUser>` in `CaseEvaluationApplicationMappers.cs`. Never AutoMapper.
- **Permissions** -- registration is anonymous (no permission check). Login + post-registration flows go through ABP defaults.
- **Localization** -- validation messages in `src/.../Domain.Shared/Localization/CaseEvaluation/en.json` with keys mirroring OLD's `ValidationFailedCode.*` (e.g., `Registration:DuplicateEmail`, `Registration:PasswordPatternMismatch`, `Registration:FirmNameRequired`). `L("Key")` in C#, `| abpLocalization` in Angular templates. Keys MUST exist in `en.json` before referenced.

### Email verification (Option A from prior decision)

Use ABP's built-in `IAccountAppService.VerifyEmailAsync(VerifyEmailDto)`. Customizations:

- Enable setting `IdentitySettingNames.User.IsEmailConfirmationRequiredForLogin = "true"` in `CaseEvaluationSettingDefinitionProvider`
- Customize the email template (subject + body) to match OLD's copy + branding
- Add "Resend verification email" link to login page (NEW Angular component) hitting `IAccountAppService.SendEmailConfirmationLinkAsync`

### Things NOT to port

- Custom `VerificationCode` GUID in user record -- ABP uses cryptographic tokens
- Custom JWT issuance via `IJwtTokenProvider` + `ApplicationUserTokens` table -- OpenIddict in NEW
- Custom password hashing (`IPasswordHash`) -- ABP uses `IPasswordHasher<TUser>`
- `EmailTemplate` enum + HTML template path config -- ABP uses `IEmailTemplateProvider` (or simply email content in localization)
- `Status` enum on User (Active/InActive/Delete) -- ABP `IsActive` + `ISoftDelete`

### Open items requiring code reads during impl

- Exact `RegexConstant.systemPasswordPattern` value -- match in `ChangeIdentityPasswordPolicySettingDefinitionProvider`
- Exact email template HTML for `UserRegistered` -- locate via `ApplicationUtility.GetEmailTemplateFromHTML` source
- Verify NEW's `InternalUsersDataSeedContributor` produces the right 3 internal roles
- Verify ABP's `IsEmailConfirmationRequiredForLogin = true` setting is enabled in NEW (currently ?)

### Verification (manual test plan, post-impl)

1. Visit `/registration` (or wherever NEW Angular hosts the form)
2. Pick role "Applicant Attorney" -- verify `FirmName` field appears + is required
3. Submit with valid data -- verify email arrives at the inbox with the expected subject + verification link
4. Click link -- verify lands on `/verify-email/...` page in Angular, succeeds, shows confirmation
5. Try login before verification -- verify "verify your email" message + auto-resend OR explicit "resend" link works
6. Login after verification -- verify dashboard loads
7. Repeat for Patient (no FirmName field), Adjuster, Defense Attorney
8. Try registering with duplicate email -- verify validation error
9. Try registering with weak password -- verify regex pattern error
10. Try email verification with bad token -- verify error message

Tests (xUnit + Shouldly per branch CLAUDE.md):
- `ExternalUserRegistrationAppServiceTests.RegisterAsync_WithValidData_CreatesUserAndAssignsRole`
- `..._WithDuplicateEmail_ThrowsBusinessException`
- `..._WithWeakPassword_ThrowsBusinessException`
- `..._AttorneyWithoutFirmName_ThrowsBusinessException`
- `..._SendsConfirmationEmail` (mocking `IEmailSender`)
- Synthetic data only per `.claude/rules/{hipaa-data,test-data}.md`.

---

## Review updates (2026-05-01)

Resolutions after Adrian's confirmations on the 3 audit-review questions, plus new findings from reading NEW Identity files.

### Q1 (typo fix) -- confirmed

Email subject becomes `"You have registered successfully - {ClinicName}"` (with branding token, fixing OLD's "Your" -> "You" typo).

### Q2 (internal user seed) -- read; findings here

`InternalUsersDataSeedContributor.cs` (`HealthcareSupport.CaseEvaluation.Domain/Identity/`) read in full. Behavior:

- **Gated to `ASPNETCORE_ENVIRONMENT=Development`** -- production never gets test logins.
- **Idempotent** -- if a user with the email exists, it is left alone.
- **Default password for every seeded account: `1q2w3E*`** (matches stock ABP password policy: upper / lower / digit / special).
- **Per-tenant seed plan** (5 users total): 4 per tenant + 1 host:

| Email | Role | Notes |
|-------|------|-------|
| `admin@<slug>.test` | (tenant admin role) | Uses ABP's stock "admin" role for the tenant. The existing tenant-admin user provisioned by `DoctorTenantAppService.CreateAsync` is reached by this slot. |
| `supervisor@<slug>.test` | Staff Supervisor | Uses `InternalUserRoleDataSeedContributor.StaffSupervisorRoleName` |
| `staff@<slug>.test` | Clinic Staff | Uses `InternalUserRoleDataSeedContributor.ClinicStaffRoleName` |
| `doctor@<slug>.test` | **Doctor** | Linked to the tenant's `Doctor` entity via `Doctor.IdentityUserId`. Re-keys `Doctor.Email` to match. |
| `it.admin@hcs.test` (host scope) | IT Admin | Host-side, no tenant. |

- **Tenant slug** is generated from tenant name: lowercased, non-alphanumeric stripped, runs of `-` collapsed, fallback "tenant".
- **Doctor entity link (`LinkDoctorEntityAsync`):** picks the first Doctor row by `CreationTime`, sets `IdentityUserId` to the seeded doctor user, and aligns `Doctor.Email`. Note from comment: "future 'own appointments only' filter (W-DOC-1)" referenced.

#### Divergence from OLD: Doctor as a user role

OLD spec (`socal-project-overview.md` lines 119-135) defines exactly **4 external + 3 internal user roles**. Doctor is NOT a user role in OLD -- the Doctor entity exists but doesn't log in. The OLD overview describes Staff Supervisor as the one managing doctor availability, locations, and appointment-type preferences (lines 529-539); the Doctor never logs in.

NEW has added Doctor as a logging-in user role with a planned "own appointments only" filter (referenced as W-DOC-1 in seed comments).

**This is a NEW feature beyond OLD spec.** Two paths:

- **(a) Strict parity:** remove the Doctor role + Doctor login. Doctor data stays as a non-user entity managed by Staff Supervisor.
- **(b) Keep NEW's extension:** Doctor logs in, sees own appointments. Adds a 5th internal role (or arguably a 6th since admin + Staff Supervisor + Clinic Staff + IT Admin + Doctor = 5). Requires audit doc for Doctor flow.

**Adrian's call.** Non-blocker for external-user registration (registration only deals with external users). Will surface again in the internal-user audit slice.

### Q3 (Option A: custom property on IdentityUser) -- confirmed

Use ABP's `IObjectExtensionManager` to add custom properties to `IdentityUser`. ABP doc reference: https://docs.abp.io/en/abp/latest/Object-Extensions

Properties to add:

- `IsExternalUser` (bool) -- replaces OLD's `UserType.External` / `UserType.Internal` distinction
- `FirmName` (string, nullable) -- attorney-only
- `FirmEmail` (string, nullable) -- attorney-only

(For Adrian, a Node analogue: this is like adding columns to a Mongoose schema via plugin instead of forking the User model.)

### Password policy reconciliation

OLD `systemPasswordPattern` from `PatientAppointment.DbEntities/Constants/RegexConstant.cs:15`:

```
^(?=.*[0-9])(?=.*[a-zA-Z])(?=.*[-.!@#$%^&*()_=+/\\'])([a-zA-Z0-9-.!@#$%^&*()_=+/\\']+)$
```

Parsed requirements:

- At least one digit
- At least one letter (upper OR lower -- not both)
- At least one special char from the set `- . ! @ # $ % ^ & * ( ) _ = + / \ '`
- No characters outside `[a-zA-Z0-9-.!@#$%^&*()_=+/\']`
- No explicit minimum length

Current NEW policy (`ChangeIdentityPasswordPolicySettingDefinitionProvider.cs`) sets ALL of `RequireNonAlphanumeric`, `RequireLowercase`, `RequireUppercase`, `RequireDigit` to **false** -- a regression from OLD's stronger requirements.

**Decision (Phase 1):** Update the provider to:

| Setting | Value | Reason |
|---------|-------|--------|
| `IdentitySettingNames.Password.RequireDigit` | `true` | Matches OLD |
| `IdentitySettingNames.Password.RequireNonAlphanumeric` | `true` | Matches OLD |
| `IdentitySettingNames.Password.RequireLowercase` | `false` | OLD allows any letter (either case) |
| `IdentitySettingNames.Password.RequireUppercase` | `false` | Same |
| `IdentitySettingNames.Password.RequiredLength` | `8` | OLD has none -- pick sane minimum |

ABP's stock policy can't express "any letter" precisely (separate `RequireUpper` / `RequireLower` toggles, no "letter" toggle). The above is the closest match. If we want exact regex parity later, add a custom `IPasswordValidator<IdentityUser>` -- but the practical gap is small (NEW will accept `aaa1!` which OLD's regex also accepts; difference is in restricted character set).

(For Adrian: in Express terms, ABP's `IPasswordValidator` is like an Express middleware that runs before user-create -- you can plug in custom validators alongside the stock ones.)

ABP setting reference: https://docs.abp.io/en/abp/latest/Modules/Identity#password-complexity

### Updated open items

| Item | Status |
|------|--------|
| Exact `systemPasswordPattern` regex value | RESOLVED (above) |
| Exact email template HTML for `UserRegistered` | TO READ during impl (locate `EmailTemplate` + `ApplicationUtility.GetEmailTemplateFromHTML` resolution path) |
| Verify NEW seed produces the 3 OLD internal roles | RESOLVED -- 4 tenant + 1 host = 5 seeded; Doctor role is NEW extension (see Q2) |
| Verify ABP's `IsEmailConfirmationRequiredForLogin = true` is enabled in NEW | TO VERIFY during impl -- check `CaseEvaluationSettingDefinitionProvider.cs` |
| Confirm ABP's lookup normalizer matches OLD's lowercase | TO VERIFY during impl |

### Doctor role question -- RESOLVED 2026-05-01

Adrian's call: **(a) Strict parity** -- remove Doctor login from NEW. Doctor stays as a non-user entity in the DB; Staff Supervisor manages doctor availability / locations / appointment-type preferences on Doctor's behalf, matching OLD spec verbatim.

Cleanup work tracked in task **"Remove Doctor user role + login from NEW (strict OLD parity)"**. Scope:

- `InternalUsersDataSeedContributor.SeedTenantUsersAsync` -- remove `doctor@<slug>.test` seed slot
- `InternalUsersDataSeedContributor.LinkDoctorEntityAsync` -- remove method
- `InternalUserRoleDataSeedContributor.DoctorRoleName` constant -- remove
- `Doctor.IdentityUserId` field -- remove (verify no other code depends on it before removing)
- Planned `W-DOC-1` "own appointments" filter -- descoped, do not implement

Sequencing: cleanup happens AFTER the external user audit slices complete, so we don't churn during active audit work.

---

## Phase 8 verification pass (2026-05-03)

Re-read OLD source vs NEW current state. Citations are file:line against
`P:\PatientPortalOld\` (OLD) and the working tree (NEW).

### A. OLD claims confirmed verbatim

| Audit claim | OLD evidence | Status |
|---|---|---|
| 4 external roles: Patient, Adjuster, PatientAttorney, DefenseAttorney | `PatientAppointment.Models\Enums\Roles.cs:14-17` | CONFIRMED |
| Frontend mirrors same 4 IDs | `patientappointment-portal\src\app\const\external-user-role-type.ts:1-7` | CONFIRMED |
| Email subject typo `"Your have registered successfully ..."` | `UserDomain.cs:321`, `UserAuthenticationDomain.cs:129` | CONFIRMED -- typo appears in 2 places, "You have" never appears for this email; **real typo, fix to "You have"** |
| Email URL pattern `/verify-email/{userId}?query={verificationCode}` | `UserDomain.cs:323` | CONFIRMED |
| Email lowercase normalization on save | `UserDomain.cs:106, 119` | CONFIRMED |
| Soft delete via `StatusId == Status.Delete` | `UserDomain.cs:71, 257-261` | CONFIRMED |
| `VerificationCode = Guid.NewGuid()` + `IsVerified = false` on insert | `UserDomain.cs:113-114` | CONFIRMED |
| Login auto-resends verification when unverified | `UserAuthenticationDomain.cs:124-145` | CONFIRMED -- but Phase 9 scope, not Phase 8 |
| Email verification sets `IsVerified = true` and does NOT clear `VerificationCode` | `UserAuthenticationDomain.cs:285-294` | CONFIRMED |
| `systemPasswordPattern` regex shape | `RegexConstant.cs:15` | CONFIRMED -- requires digit + alpha + special from a 21-char set, no min length |
| `ConfirmPassword == Password` only enforced for ExternalUser | `UserDomain.cs:88` | CONFIRMED |

### B. OLD bugs found in source -- NEW must FIX, not preserve

| OLD bug | OLD evidence | NEW behavior | Sev |
|---|---|---|---|
| `CommonValidation` checks `(RoleId == PatientAttorney \|\| RoleId == PatientAttorney)` -- duplicate check, DefenseAttorney never gets FirmName validated | `UserDomain.cs:272` | **NEW validates FirmName for BOTH ApplicantAttorney AND DefenseAttorney** (intent-correct; OLD-bug-fix exception) | B |
| `user.FirmName = user.FirmName;` no-op assignment in Add | `UserDomain.cs:107` | NEW omits the dead line | C |
| Email subject typo "Your have registered successfully" | `UserDomain.cs:321`, `UserAuthenticationDomain.cs:129` | NEW: "You have registered successfully - {ClinicName}" (Q1 confirmed) | C |
| HTML filename typo `User-Registed.html` | `wwwroot\EmailTemplates\User-Registed.html` | NEW Phase 4 owns the body via `NotificationTemplate` code `UserRegistered` (filename moot) | C |
| `AddValidation` skips password regex when password is empty (`!String.IsNullOrEmpty` gate) | `UserDomain.cs:79-86` | NEW: empty password is rejected by ABP `UserManager.CreateAsync` regardless | I (defensive improvement) |

### C. NEW state vs audit-doc plan -- material divergence

The original audit assumed nothing existed. **Reality (verified 2026-05-03):**

- A substantial `ExternalSignupAppService` already exists in
  `src\HealthcareSupport.CaseEvaluation.Application\ExternalSignups\ExternalSignupAppService.cs`
  (645 lines). It performs the basic create-user + assign-role + create-Patient flow plus
  Session A's S-5.1 / D.2 extensions: tenant lookup, admin-side invite,
  auto-link-appointments-on-register.
- Contracts live under `Application.Contracts\ExternalSignups\` (not `ExternalRegistration\`
  as the audit + plan suggested). Files: `ExternalUserType.cs`, `ExternalUserSignUpDto.cs`,
  `ExternalUserLookupDto.cs`, `ExternalUserProfileDto.cs`,
  `IExternalSignupAppService.cs`, `InviteExternalUserDto.cs`.
- Controller at `api/public/external-signup` (`ExternalSignupController.cs`) +
  `api/app/external-users/me` (`ExternalUserController.cs`).
- Angular has an `external-users\components\invite-external-user.component.ts` that
  references `ExternalUserType`.

**Plan correction (binding for Phase 8 implementation):**

| Plan said | Reality | Phase 8 action |
|---|---|---|
| Create `Application.Contracts\ExternalRegistration\IExternalUserRegistrationAppService` | `IExternalSignupAppService` already exists in `ExternalSignups\` | **Enhance in place** rather than creating a parallel directory. Renaming the directory would break Session A's tenant-invite + Angular references. |
| `RegisterAsync(ExternalUserRegisterInput)` | `RegisterAsync(ExternalUserSignUpDto)` already exists | Enhance the existing DTO + AppService, do not duplicate. |

### D. New audit gaps surfaced (NOT in original gap table)

| # | Gap | OLD ref | NEW ref | Sev | Action |
|---|---|---|---|---|---|
| G1 | NEW `ExternalUserType` enum has `ClaimExaminer` instead of `Adjuster`. OLD has 4 external roles: Patient/Adjuster/PatientAttorney/DefenseAttorney. | `Roles.cs:14-17` | `ExternalUserType.cs:1-9` (Patient=1, ClaimExaminer=2, ApplicantAttorney=3, DefenseAttorney=4) | B | **Add `Adjuster = 5` to enum + role-name mapping.** ClaimExaminer is a NEW deviation that contradicts locked memory `project_role-model.md` ("Claim Examiner is metadata not a role"). Removing ClaimExaminer breaks Session A's invite flow + Angular comp; **defer cleanup, flag as known divergence**. Phase 8 only ADDS Adjuster. |
| G2 | NEW DTO missing `ConfirmPassword` | `UserDomain.cs:88` | `ExternalUserSignUpDto.cs:6-31` | B | Add `[Required] string ConfirmPassword`. AppService validates `Password == ConfirmPassword`. |
| G3 | NEW DTO missing `FirmName` / `FirmEmail` for attorneys | `UserDomain.cs:104-108` | `ExternalUserSignUpDto.cs` | B | Add nullable `FirmName` + `FirmEmail` (StringLength 256). AppService requires `FirmName` for ApplicantAttorney + DefenseAttorney; auto-derives `FirmEmail` from `Email.ToLower()` when not provided (OLD parity, `UserDomain.cs:106`). Persists to IdentityUser extension props (Phase 2.4). |
| G4 | NEW does not set `IsExternalUser = true` extension prop on register | OLD `UserType.ExternalUser = 7` | -- | I | Set `user.SetProperty(ExtensionConfigurator.IsExternalUserPropertyName, true)` in AppService after `CreateAsync`. |
| G5 | NEW does not preserve OLD's verbatim email subject (typo fix) | `UserDomain.cs:321` "Your have registered successfully -" | -- | C | Use NotificationTemplate `UserRegistered` Subject `"You have registered successfully - {ClinicName}"`. **OLD-BUG-FIX**: typo `Your -> You`. Phase 4 already seeded the template with stub subject; Phase 18 handlers will read it. Phase 8 does not need to send the email itself -- ABP's `UserManager.CreateAsync` triggers `IEmailSender` via the `EmailConfirmationLink` flow when `RequireConfirmedEmail` is set. |
| G6 | OLD `CommonValidation` line 272 bug: PatientAttorney duplicated, DefenseAttorney never gets FirmName check | `UserDomain.cs:272` | -- | B | NEW validates `FirmName` for BOTH `ApplicantAttorney` AND `DefenseAttorney` roles. **OLD-BUG-FIX**. |
| G7 | OLD's frontend collects FirstName/LastName/DOB/Phone (line 39-49 audit table) | `user-add.component.html` + ts | NEW DTO has only optional FirstName/LastName | I | Adrian's lock 2026-04-30 (per existing `ExternalUserSignUpDto` comment): names captured later on booking form; DOB/Phone not collected at registration. **Documented strict-parity deviation** -- accepted. No action. |
| G8 | OLD password regex permits `aaa1!` (no length rule) | `RegexConstant.cs:15` | NEW `RequiredLength = 8` | I | NEW is stricter; documented Phase 2.1 decision. **Acceptable deviation** -- improves security. |
| G9 | OLD AddValidation password regex skipped when password empty | `UserDomain.cs:79-86` | -- | I | NEW: empty password rejected at DTO `[Required]` + ABP `UserManager.CreateAsync` requires non-empty. Defensive improvement. |
| G10 | OLD second commit lowercases EmailId AFTER initial uniqueness check (subtle bug -- `'A@x.com'` and `'a@x.com'` both pass uniqueness, both stored as `'a@x.com'`, second one fails on DB unique index instead of validation) | `UserDomain.cs:71` (case-sensitive check) + 119 (lowercase on update) | -- | I | NEW: ABP's `LookupNormalizer` normalizes for unique-by-email check at `FindByEmailAsync` time, so a duplicate is caught at validation, not at DB insert. **OLD-bug-fix by framework default**. |
| G11 | NEW already has invite + auto-link-appointments + tenant-options flows beyond OLD | -- | `ExternalSignupAppService.cs:88-202, 415-447, 469-542` | -- | KEEP. Useful Session A NEW extensions. Document. |
| G12 | OLD requires DOB on Add path? Audit table says yes; OLD code reads DOB only on UpdateValidation | `UserDomain.cs:165-168` (Update path only) | -- | I | OLD's Add path doesn't validate DOB despite the audit table saying "required". Effective OLD behavior: DOB is sent by frontend but server doesn't enforce. NEW omitting DOB matches the *server* behavior. No action. |

### E. Phase 8 file ownership rule (Session B vs Session A)

`memory\project_two-session-split.md` lists `src\...\Application\ExternalRegistration\`
as Session B owned. The actual code lives in `Application\ExternalSignups\` -- not
in either session's owned-roots list. **This phase classifies `ExternalSignups\`
as Session B territory** for the registration concern; Session A's tenant-invite
flow within the same file is read-only-by-Session-B (no modifications to
`InviteExternalUserAsync`, `GetTenantOptionsAsync`, `ResolveTenantByNameAsync`,
or auto-link methods).

### F. Pre-existing infrastructure blockers affecting Phase 8 verification

- **ABP Pro license blocker** (Phase 4 finding;
  `docs\handoffs\2026-05-03-test-host-license-blocker.md`). Integration tests
  for `ExternalSignupAppServiceTests` cannot execute until the placeholder in
  `appsettings.secrets.json` is replaced. Phase 8 uses the same unit-test
  pattern as Phases 3 + 4 (extract pure helpers as `internal static`,
  exercise via `InternalsVisibleTo`).
- No new infrastructure blockers introduced by Phase 8.

### G. Audit-doc lifecycle annotations

| Gap-table row (original lines 136-149) | Status |
|---|---|
| Public registration form with role selection | [IMPLEMENTED 2026-05-03 - tested unit-level] -- `ExternalSignupAppService.RegisterAsync` + Phase-8 audit gap G1 added `Adjuster` to enum + role mapping; `ExternalUserType` enum now covers all 4 OLD external roles plus the NEW ClaimExaminer deviation. |
| Attorney-only fields (FirmName, FirmEmail) | [IMPLEMENTED 2026-05-03 - tested unit-level] -- `ExternalUserSignUpDto.FirmName` + `FirmEmail` (G3); persisted via Phase 2.4 IdentityUser extension props in `RegisterAsync`. `DeriveFirmEmail` auto-fills from email per OLD `UserDomain.cs:106`. |
| Email lowercase normalization | [DESCOPED 2026-05-03 - accepted ABP default] -- ABP's `LookupNormalizer` gives the same effective uniqueness behavior as OLD's lowercase-on-save; visible behavior matches. |
| `IsVerified` flag drives login + auto-resend | [DESCOPED 2026-05-03 - Phase 9 work] -- Phase 8 ships only the registration path. Auto-resend on login (OLD `UserAuthenticationDomain.cs:124-145`) lands in Phase 9. |
| `VerificationCode` GUID stored in user record | [DESCOPED 2026-05-03 - documented framework deviation] -- ABP `DataProtectionTokenProvider` replaces OLD's stored GUID. URL shape changes from `?query={guid}` to `?confirmationToken={signed-token}`; visible behavior preserved. |
| Email verification URL | [DESCOPED 2026-05-03 - Phase 9/UI work] -- Angular `verify-email` route lands with the login UI work; backend AppService change not required. |
| Email verification URL uses int userId | [DESCOPED 2026-05-03 - documented framework deviation] -- ABP IdentityUser is Guid; URL shape unavoidable change. |
| Email verification does not auto-login | [N/A 2026-05-03] -- ABP default matches OLD; no work required. |
| Email subject typo | [IMPLEMENTED 2026-05-03 - OLD-BUG-FIX, tested via Phase 4 NotificationTemplate seed] -- the `UserRegistered` template owns the verbatim subject; Phase 4 seed already in place. Subject becomes `"You have registered successfully - {ClinicName}"` when handlers wire (Phase 18). |
| Soft delete via `StatusId == Status.Delete` allows re-registration | [DESCOPED 2026-05-03 - documented framework deviation] -- ABP's `ISoftDelete` filter excludes deleted users from the unique-email check; re-registration of a soft-deleted email works the same. |
| Validation message strings | [IMPLEMENTED 2026-05-03 - tested unit-level] -- Phase 8 added `Registration:ConfirmPasswordMismatch` + `Registration:FirmNameRequiredForAttorney` localization keys. Future phases own message keys for events they implement (Phase 9 adds login messages, Phase 10 adds password-reset messages, etc.). |
| Registration form HTML/CSS layout | [DESCOPED 2026-05-03 - Angular UI work] -- backend-only phase. UI lands in Session A's UI-coordination phase per the two-session split. |

New audit-pass gaps (G1-G6) closed by Phase 8:

| Gap | Status |
|---|---|
| G1 Add `Adjuster` to `ExternalUserType` | [IMPLEMENTED 2026-05-03 - tested unit-level] -- `ExternalUserType.Adjuster = 5` + `ToRoleName` mapping. Test: `ToRoleName_MapsKnownTypes`. |
| G2 Add `ConfirmPassword` validation | [IMPLEMENTED 2026-05-03 - tested unit-level] -- DTO field + `ValidateRegistrationInput` enforcement. Test: `ValidateRegistrationInput_PasswordMismatch_ThrowsBusinessException` + 3 sibling cases. |
| G3 Add `FirmName` / `FirmEmail` to DTO + extension props | [IMPLEMENTED 2026-05-03 - tested unit-level] -- DTO fields + `RegisterAsync` writes via `SetProperty`; auto-derive in `DeriveFirmEmail`. Tests: `DeriveFirmEmail_*` (4 cases). |
| G4 Set `IsExternalUser = true` extension prop | [IMPLEMENTED 2026-05-03 - pending integration test] -- `RegisterAsync` writes via `user.SetProperty` before `CreateAsync`. Integration test gated on ABP Pro license. |
| G5 Email subject typo `Your -> You` | [IMPLEMENTED 2026-05-03 - OLD-BUG-FIX] -- Phase 4 NotificationTemplate seed already controls subject; Phase 18 handlers will write the corrected text. |
| G6 OLD `CommonValidation` PatientAttorney-twice bug | [IMPLEMENTED 2026-05-03 - OLD-BUG-FIX, tested unit-level] -- `IsAttorneyRole` covers both attorney roles; `ValidateRegistrationInput` enforces FirmName for both. Test: `ValidateRegistrationInput_DefenseAttorneyWithoutFirmName_Throws` (the case OLD missed). |

