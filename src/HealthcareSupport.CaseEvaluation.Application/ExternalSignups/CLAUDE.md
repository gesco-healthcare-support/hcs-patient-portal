# ExternalSignups

Cross-cutting self-registration module for external users (patients, applicant/defense
attorneys, claim examiners). Creates IdentityUser accounts with role assignment and
optionally creates a Patient record. NOT a standard entity CRUD feature -- operates on
ABP's IdentityUser and the existing Patient entity via PatientManager.

## Service Shape

No domain entity. The AppService operates on ABP's `IdentityUser`, `IdentityRole`, and
`Tenant` entities, plus the project's `PatientManager`.

| Method | Auth | Purpose |
|---|---|---|
| `RegisterAsync(ExternalUserSignUpDto)` | `[AllowAnonymous]` | Create IdentityUser + role + Patient (if type=Patient) |
| `GetTenantOptionsAsync(filter?)` | `[AllowAnonymous]` | List available tenants for registration form |
| `GetExternalUserLookupAsync(filter?)` | No explicit attribute | List external users by role |
| `GetMyProfileAsync()` | `[Authorize]` | Return current user's profile (via ExternalUserController) |

### ExternalUserType -> Role Mapping

| Enum Value | Role Name |
|---|---|
| Patient (1) | "Patient" |
| ClaimExaminer (2) | "Claim Examiner" |
| ApplicantAttorney (3) | "Applicant Attorney" |
| DefenseAttorney (4) | "Defense Attorney" |

## Known Gotchas

1. **Missing `[RemoteService(IsEnabled = false)]`** -- Deviation from project convention
   (stated at parent Application/CLAUDE.md). Could cause ABP to register duplicate routes
   alongside the controllers, producing 500s on ambiguous route resolution.

2. **Two controllers, one AppService** -- `ExternalSignupController` (at
   `api/public/external-signup`) handles registration. `ExternalUserController` (at
   `api/app/external-users`) handles profile. Both delegate to `ExternalSignupAppService`.
   Controllers are in DIFFERENT directories (`ExternalSignups/` vs `ExternalUsers/`).

3. **Registration Patient stub (G-06-08)** -- `Gender.Unspecified` (the "not provided"
   sentinel), `DOB = DateTime.MinValue`, and `PhoneNumberType.Home` are placeholders set at
   registration before demographics are collected. The booking form requires a real Gender +
   DOB at booking; the booking prefill and read surfaces treat the sentinels as blank rather
   than surfacing a fabricated value.

4. **`GetExternalUserLookupAsync` may be unprotected** -- No explicit `[Authorize]` or
   `[AllowAnonymous]` attribute. If ABP does not apply a default policy, unauthenticated
   callers could list all external users with names and emails. Do not call this endpoint
   from new code without first adding authorization + rate-limiting.

5. **`RegisterAsync` / `GetTenantOptionsAsync` have no rate limiting and no CAPTCHA** --
   public `[AllowAnonymous]` endpoints collecting PII (name, email, password). Do not call
   these endpoints from new code without first adding authorization + rate-limiting.

6. **`[IgnoreAntiforgeryToken]`** on `ExternalSignupController` -- CSRF protection disabled
   for public registration. Intentional for public APIs but a known security surface.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Related: [Patients CLAUDE.md](../../HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md) -- Patient records created by RegisterAsync
- Related: [Appointments CLAUDE.md](../../HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md) -- booking form uses external user lookup

<!-- MANUAL:START -->
<!-- MANUAL:END -->
