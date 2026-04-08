# Doctors

Doctor profiles for IME (Independent Medical Examination) physicians. Each doctor is linked to an IdentityUser account, has a Gender, and manages many-to-many collections of AppointmentTypes and Locations they serve. The system has two AppServices: `DoctorsAppService` for standard CRUD and `DoctorTenantAppService` for tenant provisioning (creating a tenant + admin user + doctor profile in one operation).

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/Doctors/DoctorConsts.cs` | Max lengths (FirstName=50, LastName=50, Email=49), default sort |
| Domain.Shared | `src/.../Domain.Shared/Enums/Gender.cs` | Enum: Male(1), Female(2), Other(3) |
| Domain | `src/.../Domain/Doctors/Doctor.cs` | Aggregate root — IMultiTenant, has Add/Remove methods for AppointmentTypes and Locations collections |
| Domain | `src/.../Domain/Doctors/DoctorAppointmentType.cs` | Join entity — composite key {DoctorId, AppointmentTypeId} |
| Domain | `src/.../Domain/Doctors/DoctorLocation.cs` | Join entity — composite key {DoctorId, LocationId} |
| Domain | `src/.../Domain/Doctors/DoctorManager.cs` | DomainService — create/update with collection sync via SetAppointmentTypesAsync/SetLocationsAsync |
| Domain | `src/.../Domain/Doctors/DoctorWithNavigationProperties.cs` | Projection wrapper — IdentityUser + List<AppointmentType> + List<Location> |
| Domain | `src/.../Domain/Doctors/IDoctorRepository.cs` | Custom repo interface — nav-prop queries, filter by firstName/lastName/email/identityUserId/appointmentTypeId/locationId |
| Contracts | `src/.../Application.Contracts/Doctors/DoctorCreateDto.cs` | Creation input — FirstName/LastName/Email [Required], Gender, IdentityUserId, AppointmentTypeIds, LocationIds |
| Contracts | `src/.../Application.Contracts/Doctors/DoctorUpdateDto.cs` | Update input — same fields + IHasConcurrencyStamp |
| Contracts | `src/.../Application.Contracts/Doctors/DoctorDto.cs` | Full output DTO with concurrency stamp |
| Contracts | `src/.../Application.Contracts/Doctors/DoctorWithNavigationPropertiesDto.cs` | Rich output — IdentityUser + Tenant + List<AppointmentType> + List<Location> |
| Contracts | `src/.../Application.Contracts/Doctors/GetDoctorsInput.cs` | Filter input — firstName, lastName, email, identityUserId, appointmentTypeId, locationId |
| Contracts | `src/.../Application.Contracts/Doctors/IDoctorsAppService.cs` | Service interface — 10 methods including tenant lookup |
| Application | `src/.../Application/Doctors/DoctorsAppService.cs` | CRUD + lookups, syncs IdentityUser on update, uses IDataFilter to disable tenant filter |
| Application | `src/.../Application/Doctors/DoctorTenantAppService.cs` | Tenant provisioning — creates tenant + admin user + doctor profile, extends TenantAppService |
| EF Core | `src/.../EntityFrameworkCore/Doctors/EfCoreDoctorRepository.cs` | Complex joins — IdentityUser + AppointmentTypes + Locations collections |
| HttpApi | `src/.../HttpApi/Controllers/Doctors/DoctorController.cs` | Manual controller (10 endpoints) at `api/app/doctors` |
| Angular | `angular/src/app/doctors/doctor/components/doctor.component.ts` | List page — datatable with filters, extends AbstractDoctorComponent |
| Angular | `angular/src/app/doctors/doctor/components/doctor.abstract.component.ts` | Base list logic — CRUD, permission checks |
| Angular | `angular/src/app/doctors/doctor/components/doctor-detail.component.ts` | Tabbed modal — Doctor info, AppointmentTypes (M2M), Locations (M2M) |
| Angular | `angular/src/app/doctors/doctor/services/doctor.abstract.service.ts` | List data service |
| Angular | `angular/src/app/doctors/doctor/services/doctor-detail.abstract.service.ts` | Form service — builds form with collection ID arrays |
| Proxy | `angular/src/app/proxy/doctors/doctor.service.ts` | Auto-generated REST client for DoctorsAppService |
| Proxy | `angular/src/app/proxy/doctors/doctor-tenant.service.ts` | Auto-generated REST client for DoctorTenantAppService |

## Entity Shape

```
Doctor : FullAuditedAggregateRoot<Guid>, IMultiTenant
├── TenantId        : Guid?                              (tenant isolation — one doctor per tenant)
├── FirstName       : string [max 50, required]          (doctor's first name)
├── LastName        : string [max 50, required]          (doctor's last name)
├── Email           : string [max 49, required]          (contact email — note unusual max 49)
├── Gender          : Gender                             (Male=1, Female=2, Other=3)
├── IdentityUserId  : Guid?                              (FK → IdentityUser, optional)
├── AppointmentTypes : ICollection<DoctorAppointmentType> (M2M with AppointmentType)
└── Locations        : ICollection<DoctorLocation>        (M2M with Location)
```

**Collection management methods on entity:**
- `AddAppointmentType(Guid)` / `RemoveAppointmentType(Guid)` / `RemoveAllAppointmentTypesExceptGivenIds(List<Guid>)` / `RemoveAllAppointmentTypes()`
- `AddLocation(Guid)` / `RemoveLocation(Guid)` / `RemoveAllLocationsExceptGivenIds(List<Guid>)` / `RemoveAllLocations()`

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `IdentityUserId` | IdentityUser | SetNull | Optional. Doctor's login account |
| `TenantId` | Tenant | SetNull | Optional. Links doctor to SaaS tenant |

**Many-to-many via join tables:**
- `DoctorAppointmentType` → composite key `{DoctorId, AppointmentTypeId}`, both cascade (host) / NoAction (tenant)
- `DoctorLocation` → composite key `{DoctorId, LocationId}`, both cascade (host) / NoAction (tenant)

**Inbound references:**
- `DoctorAvailability` → tenant-scoped slots belong to a doctor's tenant (no direct FK to Doctor)
- `Appointment` → no direct FK to Doctor (linked via DoctorAvailability)

## Multi-tenancy

**IMultiTenant: Yes.** Each doctor is scoped to a tenant — the multi-tenant model is "one doctor per tenant" (the doctor IS the tenant).

- DbContext config is **inside** `if (builder.IsHostDatabase())` block in `CaseEvaluationDbContext`, and also configured in `CaseEvaluationTenantDbContext`
- **Host vs Tenant DB difference:** Host DB uses `Cascade` for join table FKs; Tenant DB uses `NoAction`
- `DoctorsAppService` injects `IDataFilter<IMultiTenant>` and disables the tenant filter for lookups (State, AppointmentType, Location are host-scoped)
- `DoctorTenantAppService` uses `CurrentTenant.Change(tenantId)` to switch context during provisioning

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `DoctorToDoctorDtoMappers` | `Doctor` → `DoctorDto` | No |
| `DoctorWithNavigationPropertiesToDoctorWithNavigationPropertiesDtoMapper` | `DoctorWithNavigationProperties` → `DoctorWithNavigationPropertiesDto` | No |

**Note:** No `DoctorToLookupDtoGuidMapper` exists — doctors are not used as lookup values.

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.Doctors          (Default — list/get + all lookups)
CaseEvaluation.Doctors.Create   (CreateAsync)
CaseEvaluation.Doctors.Edit     (UpdateAsync)
CaseEvaluation.Doctors.Delete   (DeleteAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children. All CRUD permissions properly enforced.

## Business Rules

1. **UpdateAsync syncs IdentityUser** — when updating a doctor, `DoctorsAppService.UpdateAsync` also updates the linked IdentityUser's `Name`, `Surname`, and `Email` to match the doctor record. This is a side-effect not obvious from the method signature.

2. **Collection sync uses "except given IDs" pattern** — `DoctorManager.SetAppointmentTypesAsync` calls `RemoveAllAppointmentTypesExceptGivenIds(newIds)` then adds missing ones. Same for Locations. This means the full list must be sent on every update — omitting an ID removes it.

3. **Gender defaults to first enum value** — `DoctorCreateDto.Gender` defaults to `((Gender[])Enum.GetValues(typeof(Gender)))[0]` which is `Male(1)`. Not an explicit default, just the first enum member.

4. **Tenant provisioning flow** — `DoctorTenantAppService.CreateAsync` overrides the standard tenant creation to: (a) create the SaaS tenant, (b) create/find an admin IdentityUser with "Doctor" role, (c) create a Doctor profile linked to that user. This is the onboarding path for new doctor practices.

5. **IDataFilter usage for cross-tenant lookups** — `DoctorsAppService` disables `IMultiTenant` filter when querying host-scoped entities (AppointmentType, Location, State) so doctors can see all reference data regardless of tenant context.

6. **Email max length is 49** — unusual number (not 50, 100, or 255). Likely a code generation artifact.

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

## Known Gotchas

1. **Two separate AppServices** — `DoctorsAppService` (standard CRUD) and `DoctorTenantAppService` (tenant provisioning). The tenant service extends ABP's `TenantAppService`, not `CaseEvaluationAppService`. It has its own proxy service (`doctor-tenant.service.ts`).

2. **No explicit Tenant FK in entity** — the `TenantId` comes from `IMultiTenant`, but the DbContext also configures `b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull)`. This creates an explicit FK constraint that most IMultiTenant entities don't have.

3. **Host vs Tenant cascade behavior differs** — Host DB cascades join table deletes; Tenant DB uses NoAction. This means deleting a Doctor in host context cascades to DoctorAppointmentType/DoctorLocation, but in tenant context it would fail if join records exist.

4. **Tests exist** — `DoctorsDataSeedContributor` seeds 2 doctors with hardcoded GUIDs for testing. There are existing tests for Doctors (unlike most other features).

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Docs: [docs/features/doctors/overview.md](/docs/features/doctors/overview.md) (if exists)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
