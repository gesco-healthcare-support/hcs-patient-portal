<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md on 2026-04-08 -->

# Doctors

Doctor profiles for IME (Independent Medical Examination) physicians. Each doctor is linked to an IdentityUser account, has a Gender, and manages many-to-many collections of AppointmentTypes and Locations they serve. The system has two AppServices: `DoctorsAppService` for standard CRUD and `DoctorTenantAppService` for tenant provisioning (creating a tenant + admin user + doctor profile in one operation).

## Entity Shape

```
Doctor : FullAuditedAggregateRoot<Guid>, IMultiTenant
├── TenantId        : Guid?                              (tenant isolation — one doctor per tenant)
├── FirstName       : string [max 50, required]
├── LastName        : string [max 50, required]
├── Email           : string [max 49, required]          (note unusual max 49)
├── Gender          : Gender                             (Male=1, Female=2, Other=3)
├── IdentityUserId  : Guid?                              (FK → IdentityUser, optional)
├── AppointmentTypes : ICollection<DoctorAppointmentType> (M2M with AppointmentType)
└── Locations        : ICollection<DoctorLocation>        (M2M with Location)
```

Collection management via entity methods: `AddAppointmentType/RemoveAppointmentType`, `AddLocation/RemoveLocation`, plus bulk `RemoveAllExceptGivenIds` variants.

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `IdentityUserId` | IdentityUser | SetNull | Optional. Doctor's login account |
| `TenantId` | Tenant | SetNull | Links doctor to SaaS tenant |

**Many-to-many via join tables:**
- `DoctorAppointmentType` → composite key, cascade (host) / NoAction (tenant)
- `DoctorLocation` → composite key, cascade (host) / NoAction (tenant)

## Multi-tenancy

**IMultiTenant: Yes.** Each doctor IS a tenant — "one doctor per tenant" model. The `DoctorTenantAppService` creates the tenant, admin user, and doctor profile in one operation.

- DbContext config in **both** contexts (outside `IsHostDatabase()`)
- Host cascades join table deletes; Tenant uses NoAction
- `DoctorsAppService` disables `IMultiTenant` filter for cross-tenant lookups

## Business Rules

1. **UpdateAsync syncs IdentityUser** — updates the linked user's Name, Surname, and Email as a side-effect
2. **Collection sync is replace-all** — full AppointmentTypeIds/LocationIds list must be sent on update; omitted IDs are removed
3. **Gender defaults to Male(1)** — first enum value, not explicitly chosen
4. **Tenant provisioning** — `DoctorTenantAppService.CreateAsync` creates tenant + "Doctor" role + admin user + Doctor profile
5. **IDataFilter disables tenant filter** for cross-tenant lookups of host-scoped reference data

## Known Gotchas

1. **Two AppServices** — `DoctorsAppService` (CRUD) and `DoctorTenantAppService` (extends ABP TenantAppService, not CaseEvaluationAppService)
2. **Explicit Tenant FK** — unusual for IMultiTenant entities; creates a hard FK constraint most other entities don't have
3. **Host vs Tenant cascade differs** — deleting a Doctor cascades in host context but fails in tenant context if join records exist
4. **Tests exist** — `DoctorsDataSeedContributor` seeds 2 doctors with hardcoded GUIDs

## Mapper Configuration

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `DoctorToDoctorDtoMappers` | Entity → DTO | No |
| `DoctorWithNavigationProperties...DtoMapper` | NavProps → NavPropsDto | No |

No LookupDto mapper — doctors are not used as lookup values.

## Permissions

```
CaseEvaluation.Doctors          (Default)
CaseEvaluation.Doctors.Create   (CreateAsync)
CaseEvaluation.Doctors.Edit     (UpdateAsync — also syncs IdentityUser)
CaseEvaluation.Doctors.Delete   (DeleteAsync)
```

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/Doctors/DoctorConsts.cs` | Max lengths, default sort |
| Domain.Shared | `src/.../Domain.Shared/Enums/Gender.cs` | Male(1), Female(2), Other(3) |
| Domain | `src/.../Domain/Doctors/Doctor.cs` | Aggregate root with collection management methods |
| Domain | `src/.../Domain/Doctors/DoctorAppointmentType.cs` | Join entity (composite key) |
| Domain | `src/.../Domain/Doctors/DoctorLocation.cs` | Join entity (composite key) |
| Domain | `src/.../Domain/Doctors/DoctorManager.cs` | DomainService — create/update with collection sync |
| Domain | `src/.../Domain/Doctors/IDoctorRepository.cs` | Custom repo interface |
| Contracts | `src/.../Application.Contracts/Doctors/` | DTOs, filter input, service interface |
| Application | `src/.../Application/Doctors/DoctorsAppService.cs` | CRUD + lookups + IdentityUser sync |
| Application | `src/.../Application/Doctors/DoctorTenantAppService.cs` | Tenant provisioning flow |
| EF Core | `src/.../EntityFrameworkCore/Doctors/EfCoreDoctorRepository.cs` | Complex joins repo |
| HttpApi | `src/.../HttpApi/Controllers/Doctors/DoctorController.cs` | 10 endpoints at `api/app/doctors` |
| Angular | `angular/src/app/doctors/` | List + tabbed detail modal (Doctor/Types/Locations tabs) |
| Proxy | `angular/src/app/proxy/doctors/` | Two proxy services (doctor + doctor-tenant) |

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `DoctorAppointmentType.DoctorId` | Cascade (host) / NoAction (tenant) | No | Join table — M2M with AppointmentType |
| `DoctorLocation.DoctorId` | Cascade (host) / NoAction (tenant) | No | Join table — M2M with Location |

No direct FK from DoctorAvailability or Appointment to Doctor. Availability slots are linked to doctors implicitly via tenant scoping.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| DoctorComponent | `angular/src/app/doctors/doctor/components/doctor.component.ts` | `/doctor-management/doctors` | List view with filters |
| AbstractDoctorComponent | `angular/src/app/doctors/doctor/components/doctor.abstract.component.ts` | — | Base directive with CRUD wiring |
| DoctorDetailModalComponent | `angular/src/app/doctors/doctor/components/doctor-detail.component.ts` | — | Tabbed modal (Doctor, AppointmentTypes, Locations) |

**Pattern:** ABP Suite abstract/concrete (`AbstractDoctorComponent` → `DoctorComponent`). Detail modal uses 3 tabs.

**Forms:**
- Tab 1 (Doctor): firstName (max 50, req), lastName (max 50, req), email (max 49, req), gender (select, req), identityUserId (lookup, optional)
- Tab 2: appointmentTypeIds — M2M lookup typeahead
- Tab 3: locationIds — M2M lookup typeahead
- Filters: firstName, lastName, email, identityUserId, appointmentTypeId, locationId

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.Doctors`)
- `*abpPermission="'CaseEvaluation.Doctors.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.Doctors.Delete'"` — delete action

**Services injected:**
- `ListService`, `DoctorViewService`, `DoctorDetailViewService`, `PermissionService`, `DoctorService` (proxy)

## Related Features

- [Appointment Types](../appointment-types/overview.md) — M2M via `DoctorAppointmentType` join table
- [Locations](../locations/overview.md) — M2M via `DoctorLocation` join table

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
