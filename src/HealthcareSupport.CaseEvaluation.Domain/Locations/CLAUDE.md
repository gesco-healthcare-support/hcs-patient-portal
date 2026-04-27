# Locations

Physical examination locations (offices, clinics) where IME appointments take place. Host-scoped master data shared across all tenants -- managed by Host admins, referenced by tenant-scoped DoctorAvailabilities and Appointments via NoAction FKs.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Locations/LocationConsts.cs` | Max lengths (Name=50, Address=100, City=50, ZipCode=15) and default sort helper |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Locations/Location.cs` | Aggregate root entity -- `FullAuditedAggregateRoot<Guid>`, NOT IMultiTenant, host-scoped |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Locations/LocationManager.cs` | DomainService -- create/update with length validation, sets concurrency stamp |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Locations/LocationWithNavigationProperties.cs` | Projection wrapper -- carries State + AppointmentType nav props |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Locations/ILocationRepository.cs` | Custom repo interface -- nav-prop queries, DeleteAllAsync, filtered list/count |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/LocationCreateDto.cs` | Creation input -- Name [Required], IsActive defaults to true |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/LocationUpdateDto.cs` | Update input -- implements IHasConcurrencyStamp |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/LocationDto.cs` | Full output DTO with concurrency stamp |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/LocationWithNavigationPropertiesDto.cs` | Rich output with State + AppointmentType nav props |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/GetLocationsInput.cs` | Filter input -- name, city, zipCode, parkingFee range, isActive, stateId, appointmentTypeId |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/ILocationsAppService.cs` | Service interface -- 10 methods including DeleteByIds and DeleteAll |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/Locations/LocationsAppService.cs` | CRUD + lookups + bulk delete, permission-gated |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Locations/EfCoreLocationRepository.cs` | 2-way LEFT JOIN (State, AppointmentType), DeleteAllAsync via filter |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Locations/LocationController.cs` | Manual controller (10 endpoints) at `api/app/locations` |
| Angular | `angular/src/app/locations/location/components/location.abstract.component.ts` | Abstract directive -- list wiring + permission visibility |
| Angular | `angular/src/app/locations/location/components/location.component.ts` | Concrete list page (template + styles in adjacent .html) |
| Angular | `angular/src/app/locations/location/components/location-detail.component.ts` | Modal form for create/edit (template in adjacent .html) |
| Angular | `angular/src/app/locations/location/services/location.abstract.service.ts` | Abstract list view service -- paging, selection, bulk delete |
| Angular | `angular/src/app/locations/location/services/location.service.ts` | Concrete LocationViewService (extends abstract) |
| Angular | `angular/src/app/locations/location/services/location-detail.abstract.service.ts` | Abstract detail view service -- form build, create/update routing |
| Angular | `angular/src/app/locations/location/services/location-detail.service.ts` | Concrete LocationDetailViewService (extends abstract) |
| Angular | `angular/src/app/locations/location/location-routes.ts` | Standalone route definition with authGuard + permissionGuard |
| Angular | `angular/src/app/locations/location/providers/location-base.routes.ts` | Menu route metadata (Doctor Management > Locations) |
| Angular | `angular/src/app/locations/location/providers/location-route.provider.ts` | App initializer registering LOCATION_BASE_ROUTES with RoutesService |
| Proxy | `angular/src/app/proxy/locations/location.service.ts` | Auto-generated REST client (10 methods) |
| Proxy | `angular/src/app/proxy/locations/models.ts` | Auto-generated DTO interfaces |
| Tests | `test/HealthcareSupport.CaseEvaluation.Application.Tests/Locations/LocationsAppServiceTests.cs` | xUnit + Shouldly suite -- 11 facts (9 active, 2 skipped FK-edge) |

## Entity Shape

```
Location : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant -- host-scoped)
|-- Name              : string  [max 50, required]
|-- Address           : string? [max 100]
|-- City              : string? [max 50]
|-- ZipCode           : string? [max 15]
|-- ParkingFee        : decimal                    (parking cost for patients, non-nullable)
|-- IsActive          : bool                       (active/inactive toggle)
|-- StateId           : Guid?                      (FK -> State, optional)
|-- AppointmentTypeId : Guid?                      (FK -> AppointmentType, optional)
|-- DoctorLocations   : ICollection<DoctorLocation>  (many-to-many with Doctor)
```

No status/state enum fields -- IsActive is the only state toggle.

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `StateId` | State | SetNull | Optional. Host-scoped lookup |
| `AppointmentTypeId` | AppointmentType | SetNull | Optional. Host-scoped lookup |

**Many-to-many via join table:**
- `DoctorLocation` -- composite key `{DoctorId, LocationId}`. Configured in BOTH host (`CaseEvaluationDbContext`, inside `IsHostDatabase()`) and tenant (`CaseEvaluationTenantDbContext`, top-level) contexts. Both FKs cascade on delete.

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `DoctorAvailability.LocationId` | NoAction | No -- configured in `CaseEvaluationDbContext` (line 158) and `CaseEvaluationTenantDbContext` (line 79); required FK | Cannot delete a Location with availability slots referencing it |
| `Appointment.LocationId` | NoAction | No -- configured in `CaseEvaluationDbContext` (line 208) and `CaseEvaluationTenantDbContext` (line 129); required FK | Cannot delete a Location with appointments referencing it |
| `DoctorLocation.LocationId` | Cascade | Mixed -- host context is inside `IsHostDatabase()`; tenant context is top-level | Removing a Location auto-removes the doctor-location association rows |

## Multi-tenancy

**IMultiTenant: No.** `Location` is intentionally host-scoped -- shared reference data across all tenants.

- DbContext config is **inside** `if (builder.IsHostDatabase())` in `CaseEvaluationDbContext.OnModelCreating` (lines 51-66).
- NOT configured in `CaseEvaluationTenantDbContext` for the entity itself; tenant-scoped entities (DoctorAvailability, Appointment) reference it via cross-context FKs declared in their own host/tenant configs.
- The `LocationsAreVisible_FromTenantContext` test confirms tenant users can read host-owned Locations via `GetListAsync` without TenantId filtering.

## Mapper Configuration

In `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source -> Destination | AfterMap? |
|---|---|---|
| `LocationToLocationDtoMappers` | `Location` -> `LocationDto` | No |
| `LocationWithNavigationPropertiesToLocationWithNavigationPropertiesDtoMapper` | `LocationWithNavigationProperties` -> `LocationWithNavigationPropertiesDto` | No |
| `LocationToLookupDtoGuidMapper` | `Location` -> `LookupDto<Guid>` | Yes -- `destination.DisplayName = source.Name` |

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.Locations           (Default -- list/get + lookups)
CaseEvaluation.Locations.Create    (CreateAsync)
CaseEvaluation.Locations.Edit      (UpdateAsync)
CaseEvaluation.Locations.Delete    (DeleteAsync, DeleteByIdsAsync, DeleteAllAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` (lines 35-38) as parent + 3 children. AppService class is decorated `[Authorize(Locations.Default)]`; mutating methods carry per-action `[Authorize(...)]` decorators. The Angular routes use `permissionGuard` requiring `CaseEvaluation.Locations`, and templates gate buttons via `*abpPermission`.

## Business Rules

1. **Name is required** -- only required string field; `[Required]` + `[StringLength(50)]` on both Create/Update DTOs; reinforced by `Check.NotNullOrWhiteSpace` + `Check.Length` in `LocationManager.CreateAsync`/`UpdateAsync` and the `Location` constructor.
2. **IsActive defaults to true** on `LocationCreateDto` (`IsActive { get; set; } = true;`). Toggling lets staff retire a location without deleting it.
3. **ParkingFee is mandatory** -- non-nullable decimal. The Angular form marks it required (`Validators.required`); no DB default, so create calls must supply it.
4. **Lookups filter by Name `.Contains`** -- both `GetStateLookupAsync` and `GetAppointmentTypeLookupAsync` apply `WhereIf` on `Name.Contains(filter)`; no isActive filtering applied to lookup results.
5. **Bulk delete operations** -- `DeleteByIdsAsync` removes multiple locations by ID list; `DeleteAllAsync` removes all rows matching a `GetLocationsInput` filter (uses the nav-properties query before projecting IDs). Both require `Locations.Delete`.
6. **No uniqueness constraint** -- AppService and Manager do not check for duplicate `Name` (or any other field) before insert.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| LocationComponent | `angular/src/app/locations/location/components/location.component.ts` | `/doctor-management/locations` | List page with filters, datatable, bulk select/delete |
| AbstractLocationComponent | `angular/src/app/locations/location/components/location.abstract.component.ts` | -- | Base directive -- list service wiring, action-button visibility |
| LocationDetailModalComponent | `angular/src/app/locations/location/components/location-detail.component.ts` | -- | Standalone modal form for create/edit |

**Pattern:** Hybrid -- ABP Suite abstract/concrete pair (`AbstractLocationComponent` -> `LocationComponent`) for the list page, plus a separate **standalone** `LocationDetailModalComponent` (no abstract) for the create/edit modal. Both concrete components declare `imports: [...]` arrays (Angular 14+ standalone-style configuration in Angular 20). The list view uses Angular Signals (`signal`, `computed`) for selection state.

**Forms (detail modal):**
- name: text input -- required, maxlength=50
- address: text input -- maxlength=100
- city: text input -- maxlength=50
- zipCode: text input -- maxlength=15
- parkingFee: number input -- required
- isActive: checkbox (defaults to true)
- stateId: `abp-lookup-select` bound to `getStateLookup`
- appointmentTypeId: `abp-lookup-select` bound to `getAppointmentTypeLookup`

**Permission guards:**
- Route (`location-routes.ts`): `canActivate: [authGuard, permissionGuard]`; menu route requires `CaseEvaluation.Locations`.
- `*abpPermission="'CaseEvaluation.Locations.Create'"` -- "New Location" button.
- `*abpPermission="'CaseEvaluation.Locations.Edit'"` -- row "Edit" action.
- `*abpPermission="'CaseEvaluation.Locations.Delete'"` -- row "Delete" + bulk Delete button.

**Services injected:**
- `AbstractLocationComponent`: `ListService`, `LocationViewService`, `LocationDetailViewService`, `PermissionService` (all via `inject()`).
- `AbstractLocationViewService`: `LocationService` (proxy), `ConfirmationService`, `ListService`.
- `AbstractLocationDetailViewService`: `FormBuilder`, `TrackByService`, `LocationService` (proxy), `ListService`.
- `LocationDetailModalComponent`: `LocationDetailViewService`.

## Test Coverage

`test/HealthcareSupport.CaseEvaluation.Application.Tests/Locations/LocationsAppServiceTests.cs` -- abstract `LocationsAppServiceTests<TStartupModule>` with 11 `[Fact]` methods (9 active, 2 skipped):

CRUD + filter:
- `GetAsync_ReturnsSeededLocation`
- `GetListAsync_WithNoFilter_ReturnsAllThreeSeededLocations`
- `CreateAsync_PersistsNewLocation`
- `UpdateAsync_ChangesMutableFields`
- `DeleteAsync_RemovesLocation`
- `GetListAsync_FilterByName_ReturnsMatchingLocation`
- `GetListAsync_FilterByParkingFeeRange_ReturnsOnlyLocationsInRange`
- `GetListAsync_FilterByIsActive_ReturnsOnlyInactiveLocations`

Nav-prop join:
- `GetWithNavigationPropertiesAsync_ReturnsLocationWithPopulatedNavProps`

Bulk delete:
- `DeleteByIdsAsync_RemovesMultipleLocations`
- `DeleteAllAsync_RemovesOnlyLocationsMatchingFilter`

Host-only scoping:
- `LocationsAreVisible_FromTenantContext`

Skipped (KNOWN GAP -- SQLite in-memory FK enforcement, tracked in `docs/issues/INCOMPLETE-FEATURES.md#test-fk-enforcement`):
- `DeleteAsync_WhenLocationReferencedByDoctorAvailability_Throws`
- `DeleteAsync_WhenLocationReferencedByAppointment_Throws`

All test data flows through `LocationsTestData`, `TenantsTestData`, `PatientsTestData`, and `IdentityUsersTestData` -- synthetic only, no PHI.

## Known Gotchas

1. **No uniqueness check on `Name`** -- two Locations may share the same name. If business expects uniqueness, the AppService/Manager would need an explicit `AnyAsync` guard before insert.
2. **`AppointmentTypeId` on `Location` has unclear semantics** -- a single optional FK linking a Location to one AppointmentType. No business logic enforces or consumes this beyond storage; intended use (e.g., "preferred type per location") is undocumented.
3. **Cascade-delete vs. NoAction asymmetry** -- deleting a Location cascades to `DoctorLocation` join rows (removes doctor associations) but is BLOCKED by `DoctorAvailability` and `Appointment` (NoAction). A Location with availability slots cannot be deleted at all; the UI offers no preview of what would block.
4. **Constructor completeness** -- `Location(Guid id, Guid? stateId, Guid? appointmentTypeId, string name, decimal parkingFee, bool isActive, string? address, string? city, string? zipCode)` accepts all 9 settable non-audit fields. No post-construction property setting in the Manager. Code-gen complete.
5. **FK-edge tests are skipped** -- the two `DeleteAsync_WhenLocationReferencedBy*` tests are marked `Skip = "KNOWN GAP: ..."` because the shared SQLite in-memory test connection ignores FK enforcement even after `Foreign Keys=True` and `PRAGMA foreign_keys = ON`. Tracked under `docs/issues/INCOMPLETE-FEATURES.md#test-fk-enforcement`. Test bodies encode target behaviour and will flip live once test infra is fixed.
6. **`LocationToLocationDtoMappers` is plural** -- class name ends in `Mappers` (plural) while sibling mappers like `LocationWithNavigationPropertiesToLocationWithNavigationPropertiesDtoMapper` are singular. Cosmetic inconsistency from the ABP Suite generator; safe to leave.
7. **`LocationsAppService` injects `IRepository<State>` and `IRepository<AppointmentType>` directly** -- bypasses the dedicated `IStateRepository`/`IAppointmentTypeRepository` patterns used elsewhere. Functional but inconsistent.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Docs: [docs/features/locations/overview.md](/docs/features/locations/overview.md) (if exists)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
