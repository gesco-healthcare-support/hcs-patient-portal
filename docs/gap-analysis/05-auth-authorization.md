# 05 -- Auth + Authorization + Permissions: Gap Analysis OLD vs NEW

## Summary

OLD ships a hand-rolled JWT + stored-procedure permission tree driving 7 hard-coded roles and 22 navigation modules, all delivered through a grant-everything stub today. NEW replaces the custom stack with OpenIddict + ABP Commercial's declarative permission system: 1 built-in admin role, 4 seeded external roles, 62 permission strings across 16 entity groups, and per-method `[Authorize]` enforcement. Intentional architectural swap aside, there are at least 14 MVP-blocking gaps: OLD has 3 extra external roles (Adjuster is not renamed to Claim Examiner consistently, PatientAttorney vs "Applicant Attorney" is only an alias) and at least 8 permission groups that have no NEW counterpart (AppointmentDocuments, AppointmentChangeLogs, AllAppointmentRequest, Reports, SystemParameters, CustomFields, Users, NotificationTemplates). MVP risk rating: High.

## Method

Reproducibility timestamp: 2026-04-23.

Sources:
1. OLD code: `P:\PatientPortalOld\**`.
2. NEW code: `W:\patient-portal\development\src\**`.
3. OLD local bring-up notes: `P:\PatientPortalOld\_local\CHANGELOG.md`.
4. NEW architecture docs: `W:\patient-portal\development\docs\backend\PERMISSIONS.md`, `docs\architecture\MULTI-TENANCY.md`.

Key files read:
- OLD: `PatientAppointment.Domain\Core\UserAuthenticationDomain.cs`, `PatientAppointment.Infrastructure\Security\JwtTokenProvider.cs`, `PatientAppointment.Infrastructure\Authorization\UserAuthorization.cs`, `PatientAppointment.Api\Controllers\Api\Core\UserAuthenticationController.cs`, `UserAuthorizationController.cs`, `patientappointment-portal\src\app\domain\authorization\can-activate-page.ts`, `patientappointment-portal\src\app\domain\access-permission.service.ts`, `patientappointment-portal\src\app\enums\role.ts`, `patientappointment-portal\src\app\enums\user-type.ts`, `PatientAppointment.DbEntities\Models\Role.cs`, `PatientAppointment.DbEntities\Models\RolePermission.cs`, `PatientAppointment.DbEntities\Enums\UserType.cs`.
- NEW: `Application.Contracts\Permissions\CaseEvaluationPermissions.cs`, `Application.Contracts\Permissions\CaseEvaluationPermissionDefinitionProvider.cs`, `Domain\Identity\ExternalUserRoleDataSeedContributor.cs`, `Domain\OpenIddict\OpenIddictDataSeedContributor.cs`, `Domain.Shared\MultiTenancy\MultiTenancyConsts.cs`, `Domain\Data\CaseEvaluationDbMigrationService.cs`, `AuthServer\appsettings.json`, `HttpApi.Host\appsettings.json`.

Counting method:
- OLD role count = 7 enum values in `role.ts:2-8`.
- OLD permission grant space = `RolePermissions` rows x 4 CRUD flags. In local DB, empty; effective permissions come from `access-permission.service.ts:17-90` sidebar module lists (22 distinct modules).
- NEW role count = 1 admin (ABP default) + 4 seeded externals = 5.
- NEW permission count = 2 Dashboard (Host + Tenant) + 15 entity groups * (1 Default + 3 CRUD children) = 62.

## OLD version state

### Login flow

Endpoint: `POST /api/userauthentication/login`. Controller: `UserAuthenticationController.cs:41-54`.

Flow (per `UserAuthenticationDomain.cs:50-113`):
1. Lookup `User` by lowercased `EmailId` where `StatusId != Delete`.
2. If `!user.IsVerified`, email verification link and return 400.
3. Verify password via `IPasswordHash.VerifySignature`.
4. Reject inactive/deleted accounts.
5. Resolve `UserType` from `RoleUserType` join.
6. Build JWT via `JwtTokenProvider.WriteToken(user)`. Expiry from `server-settings.json:2-4` = 12 hours.
7. Delete all prior `ApplicationUserToken` for user; persist new one (single-token-per-user).
8. Call `UserAuthorization.GetAccessModules(applicationModuleId=0, userId=roleId)` to build permission tree.
9. Return `UserAuthenticationViewModel { Token, Modules, EmailId, FullName, RoleId, UserTypeId, IsFirstTime, IsAccessor }`.

### JWT shape (OLD)

Claim set from `JwtTokenProvider.cs:32-43`:

| Claim | Value source | Example |
|---|---|---|
| NameIdentifier | user.UserId | "1" |
| Name | user.EmailId | "admin@local.test" |
| Role | user.RoleId (int) | "1" for ITAdmin |
| Locality | Hardcoded "1" | "1" |
| System | Hardcoded `"RXDBSERVER\\MSSQLSERVER2017"` | literal |
| GivenName | Hardcoded "SoCal" | literal |
| GroupSid | user.UserTypeId | "6" (Internal) or "7" (External) |
| UserData | Hardcoded "1" | |
| Expiration | Hardcoded "5" | (claim value, not real exp) |

Signing: custom via `Rx.Core.Security.Jwt.TokenProvider` (binary). Token expiry real `exp` = `UtcNow.AddHours(12)`.

Token storage: per-token `SecurityKey` in `ApplicationUserToken` table.

Validation: per-request DB lookup of `ApplicationUserToken` matching the Authorization header.

Refresh tokens: none. `LogOut()` is empty.

### Permission data model

- `dbo.Roles`: RoleId int PK, RoleName, Status
- `dbo.RolePermissions`: RoleId FK, ApplicationModuleId FK, CanAdd/CanEdit/CanDelete/CanView bool
- `dbo.ApplicationModules`: 3-level nested tree
- `spm.RoleUserTypes`: RoleId -> UserType (Internal=6 / External=7)

### Permission resolution (OLD, runtime)

In the local bring-up, `dbo.spPermissions` is stubbed to return one row whose `ModuleAccess` JSON grants all 4 CRUD flags for sub-modules 1..150. **Every authenticated role passes every guarded route.** This is a workaround; original design populated `dbo.RolePermissions` keyed by `(RoleId, ApplicationModuleId)` with a seed we don't have.

### Frontend guards (OLD)

`CanActivatePage` (`can-activate-page.ts:44-185`):
- Fetches permission tree from `/api/userauthorization/authorize`, caches per `cacheMinutes`.
- `checkAccess(data, userPermission)` at `:197-215` returns `userPermission[applicationModuleId][accessItem]`.
- On failure: `resolvePromise(false)` calls `storage.local.clearAll()` and hard-redirects to `/login` -- this is the "logged out when clicking Book Appointment" bug the stub papers over.

`AccessPermissionService` (`access-permission.service.ts:1-144`) is a parallel client-side gate enumerating module names per role for sidebar visibility.

### Seeded roles (OLD, post-bring-up)

| RoleId | RoleName | UserTypeId | Seeded email |
|---|---|---|---|
| 1 | ItAdmin | 6 (Internal) | admin@local.test |
| 2 | StaffSupervisor | 6 | supervisor@local.test |
| 3 | ClinicStaff | 6 | staff@local.test |
| 4 | Patient | 7 (External) | patient@local.test |
| 5 | Adjuster | 7 | adjuster@local.test |
| 6 | PatientAttorney | 7 | patatty@local.test |
| 7 | DefenseAttorney | 7 | defatty@local.test |

All seeded users: password `Admin@123`.

## NEW version state

### Login flow

Three components:
- **HealthcareSupport.CaseEvaluation.AuthServer** on port 44368 (OIDC provider)
- **HealthcareSupport.CaseEvaluation.HttpApi.Host** on port 44327 (resource server)
- **Angular client** on port 4200 (public OIDC client `CaseEvaluation_App`)

Per errata, despite config declaring HTTPS + `RequireHttpsMetadata: true`, the AuthServer responds on HTTP in the current running instance.

Supported grants (`OpenIddictDataSeedContributor.cs:73-80`):
- `authorization_code` (SPA standard via PKCE)
- `password` (resource owner credentials -- legacy)
- `client_credentials` (service-to-service)
- `refresh_token`
- `LinkLogin` (ABP social linking)
- `Impersonation` (ABP admin acting as user)

Public client `CaseEvaluation_App` (no secret, PKCE-required) + Swagger client `CaseEvaluation_Swagger`.

### JWT shape (NEW, OIDC access_token)

Standard OIDC shape (HIGH confidence):

| Claim | Source | Notes |
|---|---|---|
| iss | `AuthServer.Authority` | `https://localhost:44368` (or HTTP per errata) |
| sub | ABP user `Id` (Guid) | Standard OIDC |
| aud | `"CaseEvaluation"` | |
| exp, iat, jti | Standard | Default 3600s access / 1296000s refresh |
| role | Repeatable | `"admin"`, `"Patient"`, etc. |
| email, email_verified | | |
| given_name, family_name, phone_number | | |
| sid (session id) | | |
| tenantid (ABP) | Guid or empty for host | ABP-specific |
| scope | Space-separated | `address email phone profile roles CaseEvaluation` |
| oi_tkn_id | OpenIddict internal | |

Signing: OpenIddict default RS256 with rotating certificate.

### OIDC discovery (inferred)

Expected endpoints:
- `/connect/authorize`, `/connect/token`, `/connect/endsession`, `/connect/introspect`, `/connect/revocat`, `/connect/userinfo`
- `/.well-known/jwks`
- `/.well-known/openid-configuration`

### Permission system (NEW)

File: `Application.Contracts\Permissions\CaseEvaluationPermissionDefinitionProvider.cs:8-81`.

Constants file: `CaseEvaluationPermissions.cs:1-133` -- 16 nested static classes under single `"CaseEvaluation"` group.

62 permission strings:
- Dashboard (2): Host, Tenant
- Books (4), States (4), AppointmentTypes (4), AppointmentStatuses (4), AppointmentLanguages (4), Locations (4), WcabOffices (4), Doctors (4), DoctorAvailabilities (4), Patients (4), Appointments (4), AppointmentEmployerDetails (4), AppointmentAccessors (4), ApplicantAttorneys (4), AppointmentApplicantAttorneys (4) -- each entity has `Default`, `Create`, `Edit`, `Delete`.

Enforcement: `[Authorize(CaseEvaluationPermissions.Appointments.Default)]` on AppService class; method-level for Create/Edit/Delete. Frontend: `requiredPolicy: 'CaseEvaluation.Appointments'` in route config, `*abpPermission="'CaseEvaluation.Appointments.Create'"` on buttons.

### Seeded roles (NEW)

| Role | Source | Seeded permissions |
|---|---|---|
| admin | ABP `Volo.Abp.Identity.IdentityDataSeedContributor` | ALL (ABP default) |
| Patient | `ExternalUserRoleDataSeedContributor.cs:25` | none -- empty shell |
| Claim Examiner | `:26` | none |
| Applicant Attorney | `:27` | none |
| Defense Attorney | `:28` | none |

External roles are seeded per-tenant (inside `_currentTenant.Change`) with zero permission grants by design. Admin assigns at runtime via Permission Management UI per `docs/backend/PERMISSIONS.md:198-199`.

### Multi-tenancy (NEW)

Per `docs/architecture/MULTI-TENANCY.md`:
- Doctor-per-tenant strategy.
- 7 host-only entities; 7 tenant-scoped entities.
- `ICurrentTenant` resolved from `__tenant` header/cookie/route.
- Auto-filter: `WHERE TenantId = @currentTenant` on every `IMultiTenant` query.
- Dual DbContext (`CaseEvaluationDbContext` both + `CaseEvaluationTenantDbContext` tenant-only).

## Delta

### MVP-blocking gaps

| gap-id | capability | evidence-old | evidence-new-absent | effort |
|---|---|---|---|---|
| 5-G01 | Role: Adjuster (claim examiner variant) | `role.ts:6` + CHANGELOG seeds | NEW seeds "Claim Examiner" but per-role permission grants absent | Low |
| 5-G02 | Role: StaffSupervisor | `role.ts:3` | No supervisor tier in NEW | Medium |
| 5-G03 | Role: ClinicStaff | `role.ts:4` | No internal non-admin role | Medium |
| 5-G04 | Role: ITAdmin distinct from StaffSupervisor | `role.ts:2` | Only admin; no IT-vs-business split | Low |
| 5-G05 | Permission: AppointmentDocuments | `access-permission.service.ts:29,46-47,78` | No `AppointmentDocuments.*` permission in NEW | Medium |
| 5-G06 | Permission: AppointmentChangeLogs | `access-permission.service.ts:25,45,67` | Absent | Medium |
| 5-G07 | Permission: AllAppointmentRequest | `access-permission.service.ts:24,45,67,89` | Absent | Medium |
| 5-G08 | Permission: Reports | `access-permission.service.ts:32,53,76` | No `Reports.*` | Medium |
| 5-G09 | Permission: Users (user management) | `access-permission.service.ts:74` | Only ABP `AbpIdentity.Users.*` | Low (wire ABP Identity) |
| 5-G10 | Permission: CustomFields | `access-permission.service.ts:75` | Absent | Medium/High |
| 5-G11 | Permission: SystemParameters | `access-permission.service.ts:73` | Absent (ABP Settings covers) | Low-Medium |
| 5-G12 | Permission: NotificationTemplates | `module-names.const.ts:3` | Absent | Medium |
| 5-G13 | Self-service email verification | `UserAuthenticationController.cs:81-91` | ABP provides `/Account/EmailConfirmation`; not verified wired | Low |
| 5-G14 | Self-service forgot-password | `UserAuthenticationController.cs:56-66` | ABP Account Module provides; not verified wired | Low |

### Non-MVP gaps

| gap-id | capability | evidence-old | new-absent |
|---|---|---|---|
| 5-N01 | Accessor flag on User (`IsAccessor`) | `UserAuthenticationDomain.cs:102` | NEW uses `AppointmentAccessors` entity |
| 5-N02 | Role-owned AppointmentTypes | `RoleAppointmentType.cs:12` | No role-to-type mapping |
| 5-N03 | Single-token-per-user enforcement | `UserAuthenticationDomain.cs:84-88` | OpenIddict allows concurrent tokens |
| 5-N04 | `x-session` client-side idle timeout header | `JwtTokenProvider.cs:76-81` | No equivalent |
| 5-N05 | In-process permission cache with `cacheMinutes` | `can-activate-page.ts:100-128` | ABP distributed cache at different layer |
| 5-N06 | `/home` vs `/dashboard` internal/external routing | CHANGELOG + guard code | NEW does not distinguish user types in routing |

### Intentional architectural differences

| Topic | OLD | NEW | Why |
|---|---|---|---|
| Auth server | Custom JWT via `Rx.Core.Security.Jwt` | OpenIddict on port 44368 OIDC | Industry-standard OIDC |
| Permission authoring | `dbo.spPermissions` runtime JSON tree | `PermissionDefinitionProvider` compile-time | Declarative, analyzable |
| Permission schema | `dbo.RolePermissions` 4-bool columns | `AbpPermissionGrants` key-value | Extensible to user/claim grants |
| Frontend guard | `CanActivatePage` + `applicationModuleId`/`accessItem` | `authGuard` + `requiredPolicy: '<string>'` | ABP Angular proxy convention |
| Claim shape | `ClaimTypes.Role` = int RoleId | `role` claim = string role name | Human-readable |
| Role storage | int `RoleId` FK | ABP `IdentityRole` GUID records | ABP Identity |
| Multi-tenancy | Tenant-per-DB, DbServer/CompanyName in JWT | Row-level `IMultiTenant` + `__tenant` header | Per ADR 004 |
| Logout | Client storage clear | OpenIddict endsession + revocation | Real OIDC revocation |
| Token storage | Per-user `ApplicationUserToken` | `AbpOpenIddictTokens` | OIDC-standard lifecycle |

### Extras in NEW

- `Dashboard.Host` / `Dashboard.Tenant` permission split
- `Impersonation` grant type (admin acts as tenant user)
- `LinkLogin` grant type (social login linking)
- `client_credentials` grant (M2M)
- Refresh tokens / silent renewal
- PKCE + `S256` code challenge
- `/connect/endsession`, `/connect/userinfo`, `/connect/introspect`, `/connect/revocat`
- Swagger OAuth2 integration (`CaseEvaluation_Swagger` client)
- ApplicantAttorneys + AppointmentApplicantAttorneys permission groups (split from attorney logic)
- AppointmentAccessors permission group (was `User.IsAccessor` flag in OLD)

### JWT shape vs OIDC flow side-by-side

```
OLD login                              NEW login (Authorization Code + PKCE)
----------                             --------------------------------------
POST /api/userauthentication/login    Angular -> AuthServer:44368/connect/authorize?
{ emailId, password } -> :59741         client_id=CaseEvaluation_App
                                        response_type=code
                                        scope=openid CaseEvaluation offline_access
                                        code_challenge=... code_challenge_method=S256
                                      User authenticates
                                      AuthServer -> ?code=...
                                      Angular -> /connect/token
                                        grant_type=authorization_code
                                        code=... code_verifier=...
                                      -> { access_token, id_token, refresh_token,
                                           expires_in, token_type: Bearer }

OLD token (custom JWT, 12h):           NEW access_token (OIDC, ~1h default):
alg: HS256 (Rx.Core)                   alg: RS256, kid: rotating, typ: at+jwt
Claims:                                Claims:
  NameIdentifier=UserId                  sub=Guid
  Name=email                             iss=https://localhost:44368
  Role=RoleId(int)                       aud=CaseEvaluation
  Locality=1                             exp, iat, jti
  System=RXDBSERVER\MSSQLSERVER2017      role=[admin|Patient|...]
  GivenName=SoCal                        tenantid=Guid or empty
  GroupSid=UserTypeId                    scope="... CaseEvaluation"
  UserData=1                             email, email_verified
  Expiration=5                           oi_tkn_id
  iss=PatientAppointment                 client_id=CaseEvaluation_App
  aud=Web
  exp=now+12h

Signing:   per-token ApplicationUserToken.SecurityKey   AuthServer rotating cert
Revocation: DELETE FROM ApplicationUserTokens           /connect/revocat
Validation: DB lookup + Rx base.ValidateToken           jwks_uri public key
Refresh:   none (re-login after 12h)                    /connect/token grant_type=refresh_token
```

### Permission matrices

**OLD (7 roles x 23 module names):** Patient/Adjuster/PatientAttorney/DefenseAttorney share a single `ExternalUserModules` list (4 external roles collapsed server-side). Internal roles have distinct matrices but effectively all get grant-everything in local. 23 distinct module names referenced; 27 constants total.

**NEW (5 roles x 62 permissions):** admin has all; 4 externals are empty shells at seed time. Admin assigns via Permission Management UI.

### Role mapping

| OLD RoleId | OLD RoleName | OLD UserType | NEW equivalent | Notes |
|---|---|---|---|---|
| 1 | ItAdmin | Internal | `admin` (built-in) | Merged with StaffSupervisor |
| 2 | StaffSupervisor | Internal | (no equivalent) | Gap 5-G02 |
| 3 | ClinicStaff | Internal | (no equivalent) | Gap 5-G03 |
| 4 | Patient | External | `Patient` | Empty shell |
| 5 | Adjuster | External | `Claim Examiner` | Renamed; empty shell |
| 6 | PatientAttorney | External | `Applicant Attorney` | Renamed per WCAB; empty shell |
| 7 | DefenseAttorney | External | `Defense Attorney` | Empty shell |

OLD 7 -> NEW 5. 2 internal roles (Supervisor/Staff) collapse into admin. 4 external roles 1:1 with renames.

## Open questions

1. **Role collapse:** Acceptable to map StaffSupervisor + ClinicStaff into single admin for MVP?
2. **AppointmentDocuments + Reports ownership:** In MVP scope? Requires permission groups.
3. **Email verification + forgot-password UX:** ABP defaults sufficient or rebrand needed?
4. **Token lifetime for MVP:** 12-hour OLD vs OIDC 1-hour default -- management preference?
5. **Single-device login:** OLD enforces (`DELETE FROM ApplicationUserTokens`). Required for MVP?
6. **Tenant onboarding flow:** Manual SaaS admin provision before MVP demo or UI for it?
7. **External role default permissions:** Add seed contributor to pre-populate default grants so app is walkable after seeding?
8. **Accessor model:** OLD `User.IsAccessor` flag vs NEW per-appointment entity -- required for MVP?
9. **Live OIDC verification:** Replace inferred discovery doc with live curl capture.
10. **JWT claim parity:** Are OLD's `Locality`, `System`, `GivenName` (SoCal), `UserData`, `Expiration` hard-coded claims consumed by any legacy integration, or droppable?
