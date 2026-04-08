# Patients

Patient records for workers' comp IME evaluations. Each patient is linked to an IdentityUser account and has comprehensive demographic, contact, and language information. The feature supports three usage patterns: admin CRUD (list/create/edit/delete), appointment booking (get-or-create patient + auto-create IdentityUser with "Patient" role), and self-service profile management (logged-in patients can view/update their own profile).

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/Patients/PatientConsts.cs` | Max lengths for 15 string fields, default sort |
| Domain.Shared | `src/.../Domain.Shared/Enums/Gender.cs` | Enum: Male(1), Female(2), Other(3) |
| Domain.Shared | `src/.../Domain.Shared/Enums/PhoneNumberType.cs` | Enum: Work(28), Home(29) |
| Domain | `src/.../Domain/Patients/Patient.cs` | Aggregate root — has TenantId but NOT IMultiTenant, 22+ properties, 3 FKs |
| Domain | `src/.../Domain/Patients/PatientManager.cs` | DomainService — create/update with length validation |
| Domain | `src/.../Domain/Patients/PatientWithNavigationProperties.cs` | Projection — State + AppointmentLanguage + IdentityUser + Tenant |
| Domain | `src/.../Domain/Patients/IPatientRepository.cs` | Custom repo interface — comprehensive filtering on all 15+ string fields |
| Contracts | `src/.../Application.Contracts/Patients/PatientCreateDto.cs` | Creation input — FirstName/LastName/Email [Required] |
| Contracts | `src/.../Application.Contracts/Patients/PatientUpdateDto.cs` | Update input — IHasConcurrencyStamp |
| Contracts | `src/.../Application.Contracts/Patients/PatientDto.cs` | Full output DTO |
| Contracts | `src/.../Application.Contracts/Patients/PatientWithNavigationPropertiesDto.cs` | Rich output with 4 nav props |
| Contracts | `src/.../Application.Contracts/Patients/GetPatientsInput.cs` | Filter input — 15+ fields, DOB range, genderId |
| Contracts | `src/.../Application.Contracts/Patients/CreatePatientForAppointmentBookingInput.cs` | Booking-specific input (no identityUserId — auto-created) |
| Contracts | `src/.../Application.Contracts/Patients/IPatientsAppService.cs` | Service interface — 16 methods including booking + profile |
| Application | `src/.../Application/Patients/PatientsAppService.cs` | CRUD + booking patient creation + profile endpoints |
| EF Core | `src/.../EntityFrameworkCore/Patients/EfCorePatientRepository.cs` | 4-way LEFT JOIN (State, Language, IdentityUser, Tenant) |
| HttpApi | `src/.../HttpApi/Controllers/Patients/PatientController.cs` | Manual controller (16 endpoints) at `api/app/patients` |
| Angular | `angular/src/app/patients/.../patient.component.ts` | List page with 20+ filter fields |
| Angular | `angular/src/app/patients/.../patient.abstract.component.ts` | Base list logic |
| Angular | `angular/src/app/patients/.../patient-detail.component.ts` | Modal for create/edit |
| Angular | `angular/src/app/patients/.../patient-profile.component.ts` | Self-service profile page for logged-in patients |
| Proxy | `angular/src/app/proxy/patients/` | Auto-generated REST client (16 methods) |

## Entity Shape

```
Patient : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant — has TenantId property but not the interface)
├── TenantId              : Guid?                  (manual FK to Tenant, NOT auto-filtered by ABP)
├── FirstName             : string [max 50, req]   (patient first name)
├── LastName              : string [max 50, req]   (patient last name)
├── MiddleName            : string? [max 50]       (patient middle name)
├── Email                 : string [max 50, req]   (contact email)
├── GenderId              : Gender                 (Male=1, Female=2, Other=3)
├── DateOfBirth           : DateTime               (date of birth)
├── PhoneNumber           : string? [max 20]       (primary phone)
├── SocialSecurityNumber  : string? [max 20]       (SSN — PII)
├── Address               : string? [max 100]      (mailing address)
├── City                  : string? [max 50]       (city)
├── ZipCode               : string? [max 15]       (postal code)
├── RefferedBy            : string? [max 50]       (who referred — note typo in field name)
├── CellPhoneNumber       : string? [max 12]       (mobile number)
├── PhoneNumberTypeId     : PhoneNumberType         (Work=28, Home=29)
├── Street                : string? [max 255]      (street address)
├── InterpreterVendorName : string? [max 255]      (interpreter vendor for non-English patients)
├── ApptNumber            : string? [max 100]      (appointment reference number)
├── OthersLanguageName    : string? [max 100]      (language if not in AppointmentLanguage list)
├── StateId               : Guid?                  (FK → State, optional)
├── AppointmentLanguageId : Guid?                  (FK → AppointmentLanguage, optional)
└── IdentityUserId        : Guid                   (FK → IdentityUser, required)
```

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `StateId` | State | SetNull | Optional. Host-scoped lookup |
| `AppointmentLanguageId` | AppointmentLanguage | SetNull | Optional. Host-scoped lookup |
| `IdentityUserId` | IdentityUser | NoAction | Required. Patient's login account |
| `TenantId` | Tenant | SetNull | Optional. Links to SaaS tenant |

**Inbound FK:** `Appointment.PatientId` → references this entity.

## Multi-tenancy

**IMultiTenant: No.** Patient does NOT implement `IMultiTenant` — ABP's automatic tenant filter does NOT apply. However, Patient has a manual `TenantId` property (FK to Tenant with SetNull) which allows optional tenant association. Filtering by tenant must be done explicitly in queries.

- DbContext config is **inside** `if (builder.IsHostDatabase())` block — host context only
- The `TenantId` FK exists but without `IMultiTenant`, ABP won't auto-filter by tenant

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `PatientToPatientDtoMappers` | `Patient` → `PatientDto` | No |
| `PatientWithNavigationPropertiesToPatientWithNavigationPropertiesDtoMapper` | `PatientWithNavigationProperties` → `PatientWithNavigationPropertiesDto` | No |
| `PatientToLookupDtoGuidMapper` | `Patient` → `LookupDto<Guid>` | Yes — sets `DisplayName = source.Email` |

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.Patients          (Default — list/get + lookups)
CaseEvaluation.Patients.Create   (CreateAsync)
CaseEvaluation.Patients.Edit     (UpdateAsync)
CaseEvaluation.Patients.Delete   (DeleteAsync)
```

**Note:** Appointment booking methods (`GetOrCreatePatientForAppointmentBookingAsync`, `UpdatePatientForAppointmentBookingAsync`, `GetMyProfileAsync`, `UpdateMyProfileAsync`) only require `[Authorize]` — any authenticated user, not Patients-specific permissions.

## Business Rules

1. **GetOrCreate for appointment booking** — `GetOrCreatePatientForAppointmentBookingAsync` searches for existing patient by email. If not found, creates a new IdentityUser with "Patient" role and a linked Patient record. This is the primary patient onboarding path during appointment booking.

2. **Self-service profile** — `GetMyProfileAsync` returns the Patient record linked to `CurrentUser.Id`. `UpdateMyProfileAsync` updates that patient. Only requires `[Authorize]`, no Patients permission.

3. **Email lookup for booking** — `GetPatientByEmailForAppointmentBookingAsync` finds patient by email. Returns null if not found (not an error). Used by the booking form to check if patient exists before creating.

4. **IdentityUser auto-creation** — when creating a patient via the booking flow, `PatientsAppService` creates an IdentityUser, assigns the "Patient" role, and links it to the new Patient entity.

5. **PhoneNumberType values are non-sequential** — Work=28, Home=29 (not 1/2). These appear to come from an external system or legacy database.

6. **SSN stored in plaintext** — `SocialSecurityNumber` is a nullable string field with no encryption. PII concern.

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `Appointment.PatientId` | NoAction | No | Required FK — appointments reference the patient |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| PatientComponent | `angular/src/app/patients/patient/components/patient.component.ts` | `/patients` | List view with 20+ filter fields |
| AbstractPatientComponent | `angular/src/app/patients/patient/components/patient.abstract.component.ts` | — | Base directive with CRUD wiring |
| PatientDetailModalComponent | `angular/src/app/patients/patient/components/patient-detail.component.ts` | — | Modal with 22+ form fields |
| PatientProfileComponent | `angular/src/app/patients/patient/components/patient-profile.component.ts` | `/doctor-management/patients/my-profile` | Self-service profile for logged-in patients |

**Pattern:** ABP Suite abstract/concrete for admin CRUD + standalone `PatientProfileComponent` for self-service.

**Forms:**
- firstName (max 50, req), lastName (max 50, req), middleName (max 50), email (max 50, req)
- genderId (select, req), dateOfBirth (datepicker, req), phoneNumber (max 20), socialSecurityNumber (max 20)
- address (max 100), city (max 50), zipCode (max 15), refferedBy (max 50)
- cellPhoneNumber (max 12), phoneNumberTypeId (select: Work=28/Home=29, req)
- street (max 255), interpreterVendorName (max 255), apptNumber (max 100), othersLanguageName (max 100)
- stateId (lookup), appointmentLanguageId (lookup), identityUserId (lookup, req), tenantId (lookup)

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.Patients`)
- `*abpPermission="'CaseEvaluation.Patients.Create'"` — create button
- `*abpPermission="'CaseEvaluation.Patients.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.Patients.Delete'"` — delete action

**Services injected:**
- `ListService`, `PatientViewService`, `PatientDetailViewService`, `PermissionService`, `PatientService` (proxy)
- Profile: `AuthService`, `ConfigStateService`, `RestService`, `FormBuilder`, `Router`

## Known Gotchas

1. **Typo: `RefferedBy`** — should be "ReferredBy". Exists in entity, DTOs, database column. Changing it requires a migration.

2. **No IMultiTenant despite TenantId** — Patient has a `TenantId` property but does NOT implement `IMultiTenant`. ABP's automatic tenant filter won't apply. DbContext config is only in host context. This is a significant design choice — Patient is technically host-scoped with an optional tenant association, not truly multi-tenant like Appointment or Doctor.

3. **No tests** — no test files found for Patients.

4. **Profile component for external users** — `patient-profile.component` handles both patient users (editable form) and non-patient external users (read-only view). The branching logic is in the component.

5. **Menu placement** — under `DoctorManagement` parent menu at `/doctor-management/patients`, order 4. Patients are managed alongside Doctors and DoctorAvailabilities.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Docs: [docs/features/patients/overview.md](/docs/features/patients/overview.md) (if exists)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
