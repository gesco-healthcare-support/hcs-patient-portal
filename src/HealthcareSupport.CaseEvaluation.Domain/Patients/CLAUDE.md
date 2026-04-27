# Patients

Patient records for workers' comp IME evaluations. Each Patient is linked to an IdentityUser account and carries demographic, contact, claim, and language information used by appointment booking, all-parties notification emails (legal record), and a self-service profile page.

The feature supports three usage patterns: admin CRUD (host-context list/create/edit/delete), inline appointment booking (get-or-create patient + auto-create IdentityUser with the "Patient" role), and self-service profile read/update for the logged-in patient.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Patients/PatientConsts.cs` | Max-length constants for 15 string fields + default sort |
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/Gender.cs` | Enum: Male=1, Female=2, Other=3 |
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/PhoneNumberType.cs` | Enum: Work=28, Home=29 (non-sequential) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs` | Aggregate root: FullAuditedAggregateRoot<Guid>; has TenantId but does NOT implement IMultiTenant |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Patients/PatientManager.cs` | DomainService: CreateAsync + UpdateAsync with Check.NotNull/Check.Length validation on every field |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Patients/PatientWithNavigationProperties.cs` | Projection: Patient + State + AppointmentLanguage + IdentityUser + Tenant |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Patients/IPatientRepository.cs` | Custom repo: GetWithNavigationPropertiesAsync, GetListAsync, GetListWithNavigationPropertiesAsync, GetCountAsync (filters on 18+ fields) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/PatientCreateDto.cs` | Create input: FirstName/LastName/Email [Required], 22 fields total |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/PatientUpdateDto.cs` | Update input: implements IHasConcurrencyStamp |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/PatientDto.cs` | Output DTO: FullAuditedEntityDto<Guid>, IHasConcurrencyStamp |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/PatientWithNavigationPropertiesDto.cs` | Rich output with 4 nav DTOs (State, AppointmentLanguage, IdentityUser, Saas Tenant) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/GetPatientsInput.cs` | Filter input: 18 nullable filters + DOB range + sort/page |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/CreatePatientForAppointmentBookingInput.cs` | Booking-flow input (no IdentityUserId; AppService auto-creates the user) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/IPatientsAppService.cs` | Service interface: 16 methods (CRUD + booking + profile + 4 lookups) |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs` | Implements all 16 methods; orchestrates IdentityUser creation + Patient role grant |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` | Riok.Mapperly partials: PatientToPatientDtoMappers, PatientWithNavigationPropertiesToPatientWithNavigationPropertiesDtoMapper, PatientToLookupDtoGuidMapper |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Patients/EfCorePatientRepository.cs` | LEFT JOIN across Patient + State + AppointmentLanguage + IdentityUser + Tenant |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Patients/PatientController.cs` | Manual REST controller mounted at `api/app/patients` |
| Tests | `test/HealthcareSupport.CaseEvaluation.Application.Tests/Patients/PatientsAppServiceTests.cs` | xUnit + Shouldly: CRUD, length validation theory (15 fields), cross-tenant pin test, email-lookup, profile-tests skipped |
| Tests | `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/Patients/PatientRepositoryTests.cs` | Repo-level: filtering by name/email/identityUserId; tenant nav prop resolution |
| Angular | `angular/src/app/patients/patient/components/patient.abstract.component.ts` | Abstract base with CRUD wiring |
| Angular | `angular/src/app/patients/patient/components/patient.component.ts` | Concrete list page (template + styles in adjacent .html/.scss) |
| Angular | `angular/src/app/patients/patient/components/patient-detail.component.ts` | Concrete create/edit modal (template + styles in adjacent .html/.scss) |
| Angular | `angular/src/app/patients/patient/components/patient-profile.component.ts` | Self-service profile component for logged-in patients |
| Proxy | `angular/src/app/proxy/patients/` | Auto-generated REST client (one method per AppService endpoint) |

## Entity Shape

```
Patient : FullAuditedAggregateRoot<Guid>           (NO IMultiTenant; ABP auto-filter does NOT apply)
+-- TenantId              : Guid?                  (manual FK to Saas.Tenant; SetNull)
+-- FirstName             : string  [max 50, req]
+-- LastName              : string  [max 50, req]
+-- MiddleName            : string? [max 50]
+-- Email                 : string  [max 50, req]
+-- GenderId              : Gender                 (Male=1, Female=2, Other=3)
+-- DateOfBirth           : DateTime
+-- PhoneNumber           : string? [max 20]
+-- SocialSecurityNumber  : string? [max 20]       (PII; plaintext)
+-- Address               : string? [max 100]
+-- City                  : string? [max 50]
+-- ZipCode               : string? [max 15]
+-- RefferedBy            : string? [max 50]       (typo: should be ReferredBy)
+-- CellPhoneNumber       : string? [max 12]
+-- PhoneNumberTypeId     : PhoneNumberType        (Work=28, Home=29)
+-- Street                : string? [max 255]
+-- InterpreterVendorName : string? [max 255]
+-- ApptNumber            : string? [max 100]
+-- OthersLanguageName    : string? [max 100]
+-- StateId               : Guid?                  (FK -> State, optional, SetNull)
+-- AppointmentLanguageId : Guid?                  (FK -> AppointmentLanguage, optional, SetNull)
+-- IdentityUserId        : Guid                   (FK -> IdentityUser, required, NoAction)
```

No status/state enum on Patient itself; lifecycle (active vs locked-after-submit) is enforced upstream by Appointment, not by a Patient state field.

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `StateId` | `State` | SetNull | Optional; host-scoped lookup |
| `AppointmentLanguageId` | `AppointmentLanguage` | SetNull | Optional; host-scoped lookup |
| `IdentityUserId` | `Volo.Abp.Identity.IdentityUser` | NoAction | Required; the patient's portal login |
| `TenantId` | `Volo.Saas.Tenants.Tenant` | SetNull | Optional; manual FK -- NOT IMultiTenant |

Navigation projection `PatientWithNavigationProperties` carries Patient + State? + AppointmentLanguage? + IdentityUser? + Tenant? for joined reads.

## Multi-tenancy

**IMultiTenant: No.** Patient does NOT implement `IMultiTenant`. ABP's automatic tenant filter does NOT apply. The class declaration is `Patient : FullAuditedAggregateRoot<Guid>`; there is no `, IMultiTenant` interface.

- Patient does have a manual `TenantId` property (FK to `Saas.Tenants.Tenant`, SetNull on delete), but ABP treats it as an ordinary nullable Guid -- not as a tenant discriminator.
- DbContext config lives in `CaseEvaluationDbContext.OnModelCreating` inside `if (builder.IsHostDatabase())` -- host-scoped only. There is no tenant DbContext entry for Patient.
- Consequence (HIPAA-relevant): any caller with `CaseEvaluation.Patients` permission can read every tenant's patients via `GetListAsync`. The skipped test `GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients` pins the target behavior; it flips green when Patient becomes IMultiTenant or PatientsAppService adds a manual `CurrentTenant.Id` filter. Tracked as FEAT-09 (see Known Gotchas).

## Mapper Configuration

In `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly, NOT AutoMapper):

| Mapper Class | Source -> Destination | AfterMap? |
|---|---|---|
| `PatientToPatientDtoMappers` | `Patient` -> `PatientDto` | No AfterMap |
| `PatientWithNavigationPropertiesToPatientWithNavigationPropertiesDtoMapper` | `PatientWithNavigationProperties` -> `PatientWithNavigationPropertiesDto` | No AfterMap |
| `PatientToLookupDtoGuidMapper` | `Patient` -> `LookupDto<Guid>` | Yes -- `destination.DisplayName = source.Email;` (verified at line 286-289) |

The LookupDto mapper uses `[MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]` on both `Map` overloads so AfterMap is the sole source of `DisplayName`.

## Permissions

Defined in `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`:

```
CaseEvaluation.Patients          (Default) -- list/get + IdentityUser/Tenant lookups
CaseEvaluation.Patients.Create   -- CreateAsync
CaseEvaluation.Patients.Edit     -- UpdateAsync
CaseEvaluation.Patients.Delete   -- DeleteAsync
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` with `L("Permission:Patients")` parent and Create/Edit/Delete children.

Booking + profile + State/AppointmentLanguage lookups use `[Authorize]` (any authenticated user), NOT a Patients-specific permission. See Business Rules for the full breakdown.

## Business Rules

1. **GetOrCreate-by-email is the booking onboarding path.** `GetOrCreatePatientForAppointmentBookingAsync` searches by `email` (trimmed). If a Patient row exists, return it. If not, ensure an IdentityUser exists (find-by-email or create), grant the "Patient" role (creating the role if missing), then create the Patient via `PatientManager.CreateAsync`. This is the canonical patient-onboarding path during appointment booking.

2. **IdentityUser auto-creation uses `CurrentTenant.Id` and a hardcoded default password.** New IdentityUsers are created with `tenantId: CurrentTenant.Id`. Initial password is `CaseEvaluationConsts.AdminPasswordDefaultValue` -- the same value for every auto-created patient. (Listed here as the literal AppService behavior; intent on this is the invite-token flow per `docs/product/patients.md` -- see Known Gotchas.)

3. **Self-service profile keys off `CurrentUser.Id`.** `GetMyProfileAsync` and `UpdateMyProfileAsync` resolve the Patient by `IdentityUserId == CurrentUser.Id` via `GetCurrentPatientWithNavigationAsync`. Throws `AbpAuthorizationException` if the caller is not authenticated; throws `EntityNotFoundException` if no Patient row maps to that user. Only `[Authorize]` is required.

4. **Booking-update preserves frozen fields.** `UpdatePatientForAppointmentBookingAsync` (the booking-flow update, distinct from admin `UpdateAsync`) keeps `IdentityUserId`, `TenantId`, `GenderId`, `DateOfBirth`, and `PhoneNumberTypeId` from the existing record and falls back to the existing values for any null DTO field via `??`. Admin `UpdateAsync` does NOT use these fallbacks -- it forwards the DTO as-is.

5. **`IdentityUserId == Guid.Empty` is rejected on admin Create and Update.** Both throw `UserFriendlyException("The {0} field is required.", "IdentityUser")`. Booking-flow create does not check Guid.Empty (it generates a fresh user when needed).

6. **Email lookup returns null on not-found and on empty-string.** `GetPatientByEmailForAppointmentBookingAsync` returns null for whitespace/empty input rather than throwing, and null when no patient matches -- used by the booking form to decide whether to surface the "patient already exists" prompt.

7. **PhoneNumberType uses non-sequential enum values.** Work=28, Home=29 (not 1/2). Likely inherited from an external system or legacy schema.

8. **Length validation lives in PatientManager (not just attributes).** `PatientManager.CreateAsync` and `UpdateAsync` call `Check.Length(...)` on all 15 length-bounded string fields, throwing `ArgumentException` if exceeded. The DTO `[StringLength]` attributes are a second layer; the manager is the authoritative guard. Theory tests in `PatientsAppServiceTests` enumerate every field.

9. **Lookup permission asymmetry.** `GetStateLookupAsync` and `GetAppointmentLanguageLookupAsync` use `[Authorize]` only (any authenticated user can read state/language pickers from the booking form). `GetIdentityUserLookupAsync` and `GetTenantLookupAsync` require `CaseEvaluationPermissions.Patients.Default` -- those lookups are admin-form-only.

10. **No uniqueness check on email.** Neither AppService nor Manager queries for duplicate emails before insert; relying on the `_userManager.CreateAsync` failure for the IdentityUser side and on no constraint on the Patient side. Two Patient rows with the same email are possible if onboarding takes a non-booking path.

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `Appointment.PatientId` | NoAction | No -- configured in BOTH `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext` | Required FK; appointments cannot exist without a patient |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| AbstractPatientComponent | `angular/src/app/patients/patient/components/patient.abstract.component.ts` | -- | ABP Suite abstract base with CRUD wiring |
| PatientComponent | `angular/src/app/patients/patient/components/patient.component.ts` | `/patients` (under DoctorManagement parent menu) | Concrete list page with filter panel (template + styles in adjacent .html/.scss) |
| PatientDetailModalComponent | `angular/src/app/patients/patient/components/patient-detail.component.ts` | -- (modal) | Concrete create/edit modal with 22 form fields (template + styles in adjacent .html/.scss) |
| PatientProfileComponent | `angular/src/app/patients/patient/components/patient-profile.component.ts` | `/doctor-management/patients/my-profile` | Standalone Angular component for self-service profile (read for non-Patient users, editable for Patient role) |

**Pattern:** Mixed -- ABP Suite abstract/concrete pair for admin CRUD plus a standalone Angular component for the self-service profile.

### Sub-path A -- ABP Suite scaffold (admin CRUD)

**Forms** (PatientDetailModalComponent template):
- firstName (text, max 50, required), lastName (text, max 50, required), middleName (text, max 50)
- email (email, max 50, required), genderId (select, required), dateOfBirth (datepicker, required)
- phoneNumber (text, max 20), socialSecurityNumber (text, max 20)
- address (text, max 100), city (text, max 50), zipCode (text, max 15), refferedBy (text, max 50)
- cellPhoneNumber (text, max 12), phoneNumberTypeId (select Work/Home, required)
- street (text, max 255), interpreterVendorName (text, max 255)
- apptNumber (text, max 100), othersLanguageName (text, max 100)
- stateId (lookup), appointmentLanguageId (lookup), identityUserId (lookup, required), tenantId (lookup)

**Permission guards:**
- Route: `authGuard`, `permissionGuard` requiring `CaseEvaluation.Patients`
- Template: `*abpPermission="'CaseEvaluation.Patients.Create'"`, `Edit`, `Delete` on the corresponding action buttons

**Services injected:**
- `ListService`, `PatientViewService`, `PatientDetailViewService`, `PermissionService`, `PatientService` (proxy)

### Sub-path B -- Standalone profile component

**Imports:** Angular reactive-forms primitives, ABP form components, the auto-generated `PatientService` proxy.

**Forms:** same field list as the admin modal, but constrained to the logged-in user's record. Read-only render path used for non-Patient roles.

**Permission guards:** `[Authorize]` server-side only (no `*abpPermission` directive in template).

**Services injected:** `AuthService`, `ConfigStateService`, `RestService`, `FormBuilder`, `Router`, `PatientService` (proxy).

## Known Gotchas

1. **TenantId without IMultiTenant (FEAT-09).** `Patient` has a `TenantId` property and a manual FK to `Saas.Tenants.Tenant`, but does NOT implement `IMultiTenant`. ABP's automatic tenant filter is NOT engaged. Any caller with `CaseEvaluation.Patients` permission reads every tenant's patients. This is HIPAA-relevant. Intent (per `docs/product/patients.md`, Adrian-confirmed 2026-04-24) is strictly tenant-scoped; FEAT-09 is a code-vs-intent gap to close, not a design choice. The skipped xUnit test `GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients` pins the target behavior.

2. **Hardcoded default password on auto-created patients (Q-12).** `GetOrCreatePatientForAppointmentBookingAsync` assigns `CaseEvaluationConsts.AdminPasswordDefaultValue` to every newly created IdentityUser. Combined with the relaxed password policy (SEC-05), patient accounts are trivially compromised. Intent is an invite-token flow.

3. **SSN stored in plaintext.** `SocialSecurityNumber` is a nullable string with no encryption at rest and no UI masking. HIPAA-adjacent; firm policy on whether SSN should ever be required is open.

4. **Field-name typo `RefferedBy`.** Should be `ReferredBy`. Propagates through entity, DTOs, AppService, manager, repository signature, and DB column name. Renaming requires an EF migration plus Angular template edits.

5. **No uniqueness guard on email.** Neither AppService nor Manager checks for an existing Patient with the same email before inserting. Practical guardrail is the `_userManager.CreateAsync` IdentityUser email-uniqueness check, which only triggers in the booking flow (admin `CreateAsync` skips it).

6. **Booking + profile methods bypass Patients permissions.** `GetOrCreatePatientForAppointmentBookingAsync`, `UpdatePatientForAppointmentBookingAsync`, `GetMyProfileAsync`, `UpdateMyProfileAsync`, `GetPatientByEmailForAppointmentBookingAsync`, `GetPatientForAppointmentBookingAsync`, `GetStateLookupAsync`, `GetAppointmentLanguageLookupAsync` all use `[Authorize]` only. Documented in Business Rules, listed here because the asymmetry surprises reviewers expecting a uniform `Patients.Default` decoration.

7. **Constructor sets all 22 fields; manager re-validates lengths.** The `Patient` constructor takes every settable field and runs `Check.Length` on all 15 string fields; `PatientManager.CreateAsync` runs the same `Check.Length` checks before constructing. Double validation is a code-gen artifact (ABP Suite scaffold), not a bug.

## Test Coverage

- AppService tests: `test/HealthcareSupport.CaseEvaluation.Application.Tests/Patients/PatientsAppServiceTests.cs`
  - CRUD happy path: GetAsync, GetListAsync (no filter, by FirstName, by Email), CreateAsync, UpdateAsync (preserves IdentityUserId), DeleteAsync
  - Validation guards: empty `IdentityUserId` throws `UserFriendlyException` on Create + Update
  - Length validation theory: 15 fields x 2 (Create + Update on PatientManager) = 30 cases
  - Cross-tenant: `GetListAsync_FromHostContext_ReturnsPatientsFromBothTenants` passes (host-context behavior); `GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients` SKIPPED, pinning the FEAT-09 fix
  - Booking email lookup: found / not-found / empty
  - Profile tests: skipped pending `WithCurrentUser` test infrastructure (tracked in `docs/issues/INCOMPLETE-FEATURES.md#test-current-user-faking`)
- Repository tests: `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/Patients/PatientRepositoryTests.cs`
  - GetListAsync no-filter, GetListAsync by FirstName, GetCountAsync by Email, GetListWithNavigationPropertiesAsync by IdentityUserId, GetWithNavigationPropertiesAsync resolves Tenant nav prop

Coverage gaps still open: profile endpoints (CurrentUser faking), tenant-scoped visibility (waiting on FEAT-09 fix to flip the skipped test), Angular components.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Product intent: [docs/product/patients.md](/docs/product/patients.md)
- Feature overview: [docs/features/patients/overview.md](/docs/features/patients/overview.md) (if exists)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
