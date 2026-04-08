<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md on 2026-04-08 -->

# Patients

Patient records for workers' comp IME evaluations. Each patient is linked to an IdentityUser account and has comprehensive demographic, contact, and language information. Supports three patterns: admin CRUD, appointment booking (auto-creates IdentityUser with "Patient" role), and self-service profile management.

## Entity Shape

```
Patient : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant — has TenantId but not the interface)
├── FirstName             : string [max 50, req]
├── LastName              : string [max 50, req]
├── MiddleName            : string? [max 50]
├── Email                 : string [max 50, req]
├── GenderId              : Gender                 (Male=1, Female=2, Other=3)
├── DateOfBirth           : DateTime
├── PhoneNumber           : string? [max 20]
├── SocialSecurityNumber  : string? [max 20]       (PII — stored plaintext)
├── Address               : string? [max 100]
├── City                  : string? [max 50]
├── ZipCode               : string? [max 15]
├── RefferedBy            : string? [max 50]       (note: typo in field name)
├── CellPhoneNumber       : string? [max 12]
├── PhoneNumberTypeId     : PhoneNumberType         (Work=28, Home=29)
├── Street                : string? [max 255]
├── InterpreterVendorName : string? [max 255]
├── ApptNumber            : string? [max 100]
├── OthersLanguageName    : string? [max 100]
├── StateId               : Guid?                  (FK → State)
├── AppointmentLanguageId : Guid?                  (FK → AppointmentLanguage)
└── IdentityUserId        : Guid                   (FK → IdentityUser, required)
```

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `StateId` | State | SetNull | Optional |
| `AppointmentLanguageId` | AppointmentLanguage | SetNull | Optional |
| `IdentityUserId` | IdentityUser | NoAction | Required. Patient's login |
| `TenantId` | Tenant | SetNull | Optional |

**Inbound FK:** `Appointment.PatientId` → references this entity.

## Multi-tenancy

**IMultiTenant: No.** Patient does NOT implement `IMultiTenant`. Has a manual `TenantId` property (FK to Tenant) but ABP's automatic tenant filter does not apply. DbContext config is only inside `IsHostDatabase()`.

## Business Rules

1. **GetOrCreate for booking** — searches by email; if not found, creates IdentityUser with "Patient" role + Patient record
2. **Self-service profile** — `GetMyProfileAsync` / `UpdateMyProfileAsync` requires only `[Authorize]`, not Patients permissions
3. **Email lookup** — `GetPatientByEmailForAppointmentBookingAsync` returns null (not error) when not found
4. **SSN stored plaintext** — PII concern, no encryption
5. **PhoneNumberType values** — Work=28, Home=29 (non-sequential, possibly from legacy system)

## Known Gotchas

1. **Typo: `RefferedBy`** — should be "ReferredBy". In entity, DTOs, DB column. Requires migration to fix.
2. **DbContext config only in host** — unlike other IMultiTenant entities configured in both contexts
3. **No tests**
4. **Menu under DoctorManagement** — `/doctor-management/patients`, order 4

## Permissions

```
CaseEvaluation.Patients          (Default)
CaseEvaluation.Patients.Create   (CreateAsync)
CaseEvaluation.Patients.Edit     (UpdateAsync)
CaseEvaluation.Patients.Delete   (DeleteAsync)
```

Booking and profile methods only require `[Authorize]`.

## Mapper Configuration

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `PatientToPatientDtoMappers` | Entity → DTO | No |
| `PatientWithNavigationProperties...DtoMapper` | NavProps → NavPropsDto | No |
| `PatientToLookupDtoGuidMapper` | Entity → LookupDto | Yes — `DisplayName = source.Email` |

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

## Related Features

- [States](../states/overview.md) — FK via `StateId`
- [Appointment Languages](../appointment-languages/overview.md) — FK via `AppointmentLanguageId`
- [Appointments](../appointments/overview.md) — inbound FK via `Appointment.PatientId`

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
