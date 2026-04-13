# ExternalSignups

Cross-cutting self-registration module for external users (patients, applicant/defense attorneys, claim examiners). Creates IdentityUser accounts with role assignment and optionally creates a Patient record. NOT a standard entity CRUD feature — operates on ABP's IdentityUser and the existing Patient entity via PatientManager.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Contracts | `src/.../Application.Contracts/ExternalSignups/ExternalUserType.cs` | Enum: Patient(1), ClaimExaminer(2), ApplicantAttorney(3), DefenseAttorney(4) |
| Contracts | `src/.../Application.Contracts/ExternalSignups/ExternalUserSignUpDto.cs` | Registration input — email, password, name, userType, tenantId |
| Contracts | `src/.../Application.Contracts/ExternalSignups/ExternalUserProfileDto.cs` | Profile output — identityUserId, name, email, userRole |
| Contracts | `src/.../Application.Contracts/ExternalSignups/ExternalUserLookupDto.cs` | Lookup output — identityUserId, name, email, userRole |
| Contracts | `src/.../Application.Contracts/ExternalSignups/IExternalSignupAppService.cs` | Service interface — 4 methods |
| Application | `src/.../Application/ExternalSignups/ExternalSignupAppService.cs` | Registration logic, role management, Patient creation, user lookup |
| HttpApi | `src/.../HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs` | Public-facing signup at `api/public/external-signup` — `[IgnoreAntiforgeryToken]` |
| HttpApi | `src/.../HttpApi/Controllers/ExternalUsers/ExternalUserController.cs` | Profile endpoint at `api/app/external-users/me` — `[Authorize]` |

## Service Shape

No domain entity. The AppService operates on ABP's `IdentityUser`, `IdentityRole`, and `Tenant` entities, plus the project's `PatientManager`.

### Methods

| Method | Auth | Purpose |
|---|---|---|
| `RegisterAsync(ExternalUserSignUpDto)` | `[AllowAnonymous]` | Create IdentityUser + role + Patient (if type=Patient) |
| `GetTenantOptionsAsync(filter?)` | `[AllowAnonymous]` | List available tenants for registration form |
| `GetExternalUserLookupAsync(filter?)` | No explicit attribute | List external users by role (Patient, Applicant/Defense Attorney) |
| `GetMyProfileAsync()` | `[Authorize]` | Return current user's profile (via ExternalUserController) |

### ExternalUserType -> Role Mapping

| Enum Value | Role Name |
|---|---|
| Patient (1) | "Patient" |
| ClaimExaminer (2) | "Claim Examiner" |
| ApplicantAttorney (3) | "Applicant Attorney" |
| DefenseAttorney (4) | "Defense Attorney" |

## Relationships

This module creates and reads the following entities (no FKs of its own):

| Entity | Interaction | Notes |
|---|---|---|
| `IdentityUser` | Creates (RegisterAsync) / Reads (profile, lookup) | ABP built-in |
| `IdentityRole` | Creates if missing (EnsureRoleAsync) / Reads | ABP built-in |
| `Tenant` | Reads (GetTenantOptionsAsync) / Context-switches | ABP SaaS |
| `Patient` | Creates via `PatientManager.CreateAsync` (only type=Patient) | Project entity |

## Multi-tenancy

**Not an IMultiTenant entity** — this module orchestrates across tenant contexts.

- `GetTenantOptionsAsync`: returns empty list if already in tenant context (host-only operation)
- `RegisterAsync`: resolves tenant from `CurrentTenant.Id` or `input.TenantId`, then switches via `CurrentTenant.Change(tenantId)`
- `GetExternalUserLookupAsync` and `GetMyProfileAsync`: run in current tenant context

## Mapper Configuration

No Riok.Mapperly mappers. All DTOs are constructed manually in the AppService methods.

## Permissions

**No dedicated permissions** in `CaseEvaluationPermissions.cs`.

Auth is handled per-method:
- `RegisterAsync`: `[AllowAnonymous]` — public registration
- `GetTenantOptionsAsync`: `[AllowAnonymous]` — public tenant list
- `GetMyProfileAsync`: `[Authorize]` — any authenticated user
- `GetExternalUserLookupAsync`: **No explicit attribute** — may be unprotected (see Gotcha #6)

## Business Rules

1. **Registration creates IdentityUser + role** — `RegisterAsync` creates a new IdentityUser (username = email), assigns the role matching `ExternalUserType`, and creates the role if it doesn't exist via `EnsureRoleAsync`.

2. **Patient auto-creation with hardcoded defaults** — ONLY when `ExternalUserType.Patient`: creates a Patient via `PatientManager.CreateAsync` with:
   - `stateId: null`, `appointmentLanguageId: null`
   - `genderId: Gender.Male` (hardcoded, not from input)
   - `dateOfBirth: DateTime.UtcNow.Date` (hardcoded, not from input)
   - `phoneNumberTypeId: PhoneNumberType.Home` (hardcoded, not from input)

3. **Duplicate email check** — `FindByEmailAsync` before creation; throws `UserFriendlyException` if exists.

4. **Tenant resolution** — uses `CurrentTenant.Id` if available, otherwise requires `input.TenantId`. Throws if neither provided.

5. **Role auto-creation** — `EnsureRoleAsync` creates the role in the tenant if it doesn't exist.

6. **External user lookup filtering** — returns only users with "Patient", "Applicant Attorney", or "Defense Attorney" roles. Excludes the current user.

## Angular Integration

Not a standalone Angular module. Called via REST from other features:
- `angular/src/app/appointments/appointment-add.component.ts` — external user lookup during booking
- `angular/src/app/appointments/appointment/components/appointment-view.component.ts` — external user lookup
- `angular/src/app/patients/patient/components/patient-profile.component.ts` — GetMyProfileAsync
- `angular/src/app/app.component.ts` — profile check on app init

No generated proxy service — Angular components call the API directly via `RestService`.

## Known Gotchas

1. **Two controllers, one AppService** — `ExternalSignupController` (at `api/public/external-signup`) handles registration. `ExternalUserController` (at `api/app/external-users`) handles profile. Both delegate to `ExternalSignupAppService`. Controllers are in DIFFERENT directories.

2. **Missing `[RemoteService(IsEnabled = false)]`** — Deviation from project convention. Could cause ABP to register duplicate routes alongside the controllers.

3. **`[IgnoreAntiforgeryToken]`** on `ExternalSignupController` — CSRF protection disabled for public registration. Intentional for public APIs but a security surface.

4. **Hardcoded Patient defaults** — `Gender.Male`, `DOB = DateTime.UtcNow.Date`, `PhoneNumberType.Home` are placeholder values. Patient profile needs updating after registration.

5. **HIPAA concern** — Patient PII (name, email, password) collected via public `[AllowAnonymous]` endpoint with no additional safeguards beyond duplicate email check. No rate limiting, no CAPTCHA.

6. **GetExternalUserLookupAsync may be unprotected** — No explicit `[Authorize]` or `[AllowAnonymous]` on the controller endpoint. If ABP doesn't apply a default policy, unauthenticated callers could list all external users with names and emails.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Related: [Patients CLAUDE.md](../../HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md) — Patient records created by RegisterAsync
- Related: [Appointments CLAUDE.md](../../HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md) — booking form uses external user lookup

<!-- MANUAL:START -->
<!-- MANUAL:END -->
