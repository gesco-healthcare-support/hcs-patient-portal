<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Locations/CLAUDE.md on 2026-04-08 -->

# Locations

Physical examination locations (offices, clinics) where IME appointments take place. Host-scoped master data shared across all tenants. Each location has address information, a parking fee, an active/inactive flag, and optional links to a State and AppointmentType. Doctors are associated with locations via the `DoctorLocation` join table.

## Entity Shape

```
Location : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant — host-scoped)
├── Name              : string [max 50, required]  (location name)
├── Address           : string? [max 100]          (street address)
├── City              : string? [max 50]           (city)
├── ZipCode           : string? [max 15]           (postal code)
├── ParkingFee        : decimal                    (parking cost for patients)
├── IsActive          : bool                       (active/inactive toggle)
├── StateId           : Guid?                      (FK → State, optional)
├── AppointmentTypeId : Guid?                      (FK → AppointmentType, optional)
└── DoctorLocations   : ICollection<DoctorLocation> (many-to-many with Doctor)
```

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `StateId` | State | SetNull | Optional. Host-scoped lookup |
| `AppointmentTypeId` | AppointmentType | SetNull | Optional. Host-scoped lookup |

**Many-to-many:** `DoctorLocation` join table (composite key `{DoctorId, LocationId}`, both cascade)

**Inbound FKs:**
- `DoctorAvailability.LocationId` → NoAction (cannot delete location with existing slots)
- `Appointment.LocationId` → NoAction (cannot delete location with existing appointments)

## Multi-tenancy

**IMultiTenant: No.** Location is intentionally host-scoped — shared reference data across all tenants.

- DbContext config is **inside** `if (builder.IsHostDatabase())` block in `CaseEvaluationDbContext`
- NOT configured in `CaseEvaluationTenantDbContext` (tenant contexts reference it via FKs from tenant-scoped entities)
- The `DoctorLocation` join table IS configured outside `IsHostDatabase()` — it exists in both contexts

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `LocationToLocationDtoMappers` | `Location` → `LocationDto` | No |
| `LocationWithNavigationPropertiesToLocationWithNavigationPropertiesDtoMapper` | `LocationWithNavigationProperties` → `LocationWithNavigationPropertiesDto` | No |
| `LocationToLookupDtoGuidMapper` | `Location` → `LookupDto<Guid>` | Yes — sets `DisplayName = source.Name` |

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.Locations          (Default — list/get + lookups)
CaseEvaluation.Locations.Create   (CreateAsync)
CaseEvaluation.Locations.Edit     (UpdateAsync)
CaseEvaluation.Locations.Delete   (DeleteAsync, DeleteByIdsAsync, DeleteAllAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children. All CRUD permissions properly enforced in AppService.

## Business Rules

1. **Name is required** — the only required string field. `[Required]` on CreateDto and UpdateDto.

2. **IsActive defaults to true** — `LocationCreateDto.IsActive` defaults to `true`. Can be toggled to deactivate a location without deleting it.

3. **Bulk delete operations** — `DeleteByIdsAsync` deletes multiple locations by ID list. `DeleteAllAsync` deletes all matching a filter. Both require Delete permission.

4. **ParkingFee is always present** — non-nullable decimal, no default noted. Must be supplied on create.

5. **Lookups search by Name** — both `GetStateLookupAsync` and `GetAppointmentTypeLookupAsync` filter by `Name.Contains(filter)`.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| LocationComponent | `angular/src/app/locations/location/components/location.component.ts` | `/doctor-management/locations` | List view with bulk delete and selection |
| AbstractLocationComponent | `angular/src/app/locations/location/components/location.abstract.component.ts` | — | Base directive with CRUD wiring |
| LocationDetailModalComponent | `angular/src/app/locations/location/components/location-detail.component.ts` | — | Modal form for create/edit |

**Pattern:** ABP Suite abstract/concrete (`AbstractLocationComponent` → `LocationComponent`). Uses Angular Signals for bulk selection state.

**Forms:**
- name: text (maxLength: 50, required)
- address: text (maxLength: 100)
- city: text (maxLength: 50)
- zipCode: text (maxLength: 15)
- parkingFee: number (required)
- isActive: checkbox
- stateId: lookup select
- appointmentTypeId: lookup select

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.Locations`)
- `*abpPermission="'CaseEvaluation.Locations.Create'"` — create button
- `*abpPermission="'CaseEvaluation.Locations.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.Locations.Delete'"` — delete (single and bulk)

**Services injected:**
- `ListService`, `LocationViewService`, `LocationDetailViewService`, `PermissionService`, `LocationService` (proxy)

## Known Gotchas

1. **No controller for Location** — Actually there IS a controller at `api/app/locations`, but the Phase 0C exploration initially classified this as having no controller because the glob pattern expected `HttpApi/Controllers/Locations/` (confirmed it exists).

2. **Cascade delete risk** — Deleting a Location cascades to `DoctorLocation` join table entries, removing the doctor-location association. But `DoctorAvailability` and `Appointment` FKs use NoAction, so delete will fail if slots or appointments reference the location.

3. **No tests** — no test files found for Locations.

4. **AppointmentTypeId on Location is unusual** — a single optional FK linking a Location to one AppointmentType. This seems to represent a "default" or "primary" type for the location, but no business logic enforces or uses this relationship beyond storing it.

## Related Features

- [States](../states/overview.md) — `StateId` FK for address state lookup (host-scoped)
- [Appointment Types](../appointment-types/overview.md) — `AppointmentTypeId` FK for default IME type (host-scoped)
- [Doctor Availabilities](../doctor-availabilities/overview.md) — inbound FK `DoctorAvailability.LocationId` references this location
- [Appointments](../appointments/overview.md) — inbound FK `Appointment.LocationId` references this location
- [Doctors](../doctors/overview.md) — many-to-many via `DoctorLocation` join table

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/Locations/LocationConsts.cs` | Max lengths (Name=50, Address=100, City=50, ZipCode=15), default sort |
| Domain | `src/.../Domain/Locations/Location.cs` | Aggregate root entity — NOT multi-tenant, host-scoped, has DoctorLocations collection |
| Domain | `src/.../Domain/Locations/LocationManager.cs` | DomainService — create/update with length validation |
| Domain | `src/.../Domain/Locations/LocationWithNavigationProperties.cs` | Projection wrapper — State + AppointmentType nav props |
| Domain | `src/.../Domain/Locations/ILocationRepository.cs` | Custom repo interface — nav-prop queries, DeleteAllAsync, filter by name/city/zip/parkingFee/isActive |
| Contracts | `src/.../Application.Contracts/Locations/LocationCreateDto.cs` | Creation input — Name [Required], IsActive defaults to true |
| Contracts | `src/.../Application.Contracts/Locations/LocationUpdateDto.cs` | Update input — implements IHasConcurrencyStamp |
| Contracts | `src/.../Application.Contracts/Locations/LocationDto.cs` | Full output DTO with concurrency stamp |
| Contracts | `src/.../Application.Contracts/Locations/LocationWithNavigationPropertiesDto.cs` | Rich output with State + AppointmentType nav props |
| Contracts | `src/.../Application.Contracts/Locations/GetLocationsInput.cs` | Filter input — name, city, zipCode, parkingFee range, isActive, stateId, appointmentTypeId |
| Contracts | `src/.../Application.Contracts/Locations/ILocationsAppService.cs` | Service interface — 10 methods including DeleteByIds and DeleteAll |
| Application | `src/.../Application/Locations/LocationsAppService.cs` | CRUD + bulk delete, permission-gated |
| EF Core | `src/.../EntityFrameworkCore/Locations/EfCoreLocationRepository.cs` | 2-way LEFT JOIN (State, AppointmentType), DeleteAllAsync |
| HttpApi | `src/.../HttpApi/Controllers/Locations/LocationController.cs` | Manual controller (10 endpoints) at `api/app/locations` |
| Angular | `angular/src/app/locations/` | List page with datatable + detail modal (abstract/concrete pattern) |
| Proxy | `angular/src/app/proxy/locations/` | Auto-generated REST client |

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/Locations/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
