# Doctors

Doctor profiles for IME (Independent Medical Examination) physicians. Each tenant represents exactly one doctor (one-doctor-per-tenant model -- see `docs/product/doctors.md`); the entity carries name, email, gender, an optional IdentityUser link, and many-to-many collections of AppointmentTypes and Locations the practice serves. Two AppServices are involved: `DoctorsAppService` for standard CRUD plus IdentityUser sync, and `DoctorTenantAppService` which overrides ABP SaaS tenant creation to provision tenant + admin user + Doctor profile in one operation.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Doctors/DoctorConsts.cs` | FirstName/LastName max 50, Email max 49 (typo, see Gotchas), default sort |
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/Gender.cs` | Enum: Male=1, Female=2, Other=3 |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs` | Aggregate root, FullAuditedAggregateRoot<Guid> + IMultiTenant, owns AppointmentTypes / Locations collections with Add/Remove/RemoveAllExcept methods |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/DoctorAppointmentType.cs` | Join entity (Entity, no Guid id) -- composite key {DoctorId, AppointmentTypeId} |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/DoctorLocation.cs` | Join entity (Entity, no Guid id) -- composite key {DoctorId, LocationId} |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/DoctorManager.cs` | DomainService -- Create/Update with replace-all collection sync |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/DoctorWithNavigationProperties.cs` | Projection wrapper (Doctor + IdentityUser? + List<AppointmentType> + List<Location>) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/IDoctorRepository.cs` | Custom repo interface -- nav-prop queries, filter by name/email/identityUserId/appointmentTypeId/locationId |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/DoctorCreateDto.cs` | Create input -- FirstName/LastName/Email [Required + StringLength], Gender (defaults to first enum value), optional IdentityUserId, AppointmentTypeIds, LocationIds |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/DoctorUpdateDto.cs` | Update input -- same fields + IHasConcurrencyStamp |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/DoctorDto.cs` | Output -- FullAuditedEntityDto<Guid>, IHasConcurrencyStamp |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/DoctorWithNavigationPropertiesDto.cs` | Rich output -- DoctorDto + IdentityUserDto? + List<AppointmentTypeDto> + List<LocationDto> |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/GetDoctorsInput.cs` | PagedAndSortedResultRequestDto + FilterText/FirstName/LastName/Email/IdentityUserId/AppointmentTypeId/LocationId |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/IDoctorsAppService.cs` | Interface -- 10 methods (CRUD + 4 lookup endpoints + GetWithNavigationPropertiesAsync) |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs` | CRUD + lookups; UpdateAsync also syncs IdentityUser Name/Surname/Email; uses IDataFilter<IMultiTenant> in GetListAsync when host |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs` | Overrides SaaS TenantAppService.CreateAsync -- tenant + admin user + Doctor profile + "Doctor" role provisioning |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Doctors/EfCoreDoctorRepository.cs` | Implements IDoctorRepository -- LINQ joins for IdentityUser + AppointmentTypes + Locations |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Doctors/DoctorController.cs` | Manual controller at `api/app/doctors` -- 10 endpoints proxy to IDoctorsAppService |
| Angular | `angular/src/app/doctors/doctor/components/doctor.component.ts` | List page (loadComponent target) |
| Angular | `angular/src/app/doctors/doctor/components/doctor.abstract.component.ts` | Abstract base class for list logic |
| Angular | `angular/src/app/doctors/doctor/components/doctor-detail.component.ts` | Tabbed create/edit modal (Doctor / AppointmentTypes / Locations) |
| Angular | `angular/src/app/doctors/doctor/doctor-routes.ts` | Route definition with authGuard + permissionGuard |
| Angular | `angular/src/app/doctors/doctor/providers/doctor-base.routes.ts` | Base route metadata provider |
| Angular | `angular/src/app/doctors/doctor/providers/doctor-route.provider.ts` | Concrete route provider extending the base |
| Angular | `angular/src/app/doctors/doctor/services/doctor.abstract.service.ts` | Abstract list/data service |
| Angular | `angular/src/app/doctors/doctor/services/doctor.service.ts` | Concrete list/data service |
| Angular | `angular/src/app/doctors/doctor/services/doctor-detail.abstract.service.ts` | Abstract form service |
| Angular | `angular/src/app/doctors/doctor/services/doctor-detail.service.ts` | Concrete form service |
| Proxy | `angular/src/app/proxy/doctors/doctor.service.ts` | Auto-generated REST client for IDoctorsAppService |
| Proxy | `angular/src/app/proxy/doctors/doctor-tenant.service.ts` | Auto-generated REST client for DoctorTenantAppService |
| Proxy | `angular/src/app/proxy/doctors/models.ts` | DTO shapes (DoctorDto, DoctorCreateDto, etc.) |
| Proxy | `angular/src/app/proxy/doctors/index.ts` | Barrel export |

## Entity Shape

```
Doctor : FullAuditedAggregateRoot<Guid>, IMultiTenant
  TenantId         : Guid?                              (declared explicitly on the entity, not just inherited)
  FirstName        : string  [required, max 50]
  LastName         : string  [required, max 50]
  Email            : string  [required, max 49]         (typo -- see Gotchas)
  Gender           : Gender                             (Male=1 | Female=2 | Other=3)
  IdentityUserId   : Guid?                              (optional FK -> Volo.Abp.Identity.IdentityUser)
  AppointmentTypes : ICollection<DoctorAppointmentType> (M2M via join entity)
  Locations        : ICollection<DoctorLocation>        (M2M via join entity)
```

Constructor takes (id, identityUserId, firstName, lastName, email, gender) and validates name/email lengths via `Check.NotNull` + `Check.Length`. Collections are initialised empty in the constructor and populated via the Manager's collection-sync helpers.

Collection management on the entity:
- `AddAppointmentType(id)` / `RemoveAppointmentType(id)` / `RemoveAllAppointmentTypesExceptGivenIds(ids)` / `RemoveAllAppointmentTypes()`
- `AddLocation(id)` / `RemoveLocation(id)` / `RemoveAllLocationsExceptGivenIds(ids)` / `RemoveAllLocations()`

No state machine -- Doctor has no status enum.

## Relationships

| FK / Nav | Target | Delete Behavior | Notes |
|---|---|---|---|
| `Doctor.IdentityUserId` | `IdentityUser` | SetNull (host + tenant) | Optional login account link |
| `Doctor.TenantId` | `Tenant` | SetNull (host only) | Explicit `HasOne<Tenant>().HasForeignKey(x => x.TenantId)` -- unusual for IMultiTenant |
| `DoctorAppointmentType.DoctorId` -> `Doctor` | M2M join | Cascade (host) / NoAction (tenant) | Owned via `b.HasOne(x => x.Doctor).WithMany(x => x.AppointmentTypes)` |
| `DoctorAppointmentType.AppointmentTypeId` -> `AppointmentType` | lookup | Cascade (host) / NoAction (tenant) | |
| `DoctorLocation.DoctorId` -> `Doctor` | M2M join | Cascade (host) / NoAction (tenant) | |
| `DoctorLocation.LocationId` -> `Location` | lookup | Cascade (host) / NoAction (tenant) | |

`DoctorWithNavigationProperties` projection adds `IdentityUser? IdentityUser`, `List<AppointmentType>`, `List<Location>` for read endpoints.

## Multi-tenancy

IMultiTenant: yes. Doctor is tenant-scoped, but the product intent is "exactly one Doctor per tenant" (see `docs/product/doctors.md`); the multiplicity is conceptually 1:1, even though the schema permits N:1.

- Configured in BOTH DbContexts:
  - `CaseEvaluationDbContext.OnModelCreating` -- INSIDE an `if (builder.IsHostDatabase())` block. Host wiring uses `Cascade` deletes for the join tables and adds the explicit `HasOne<Tenant>()` FK (line 96).
  - `CaseEvaluationTenantDbContext.OnModelCreating` -- OUTSIDE any host guard. Tenant wiring uses `NoAction` deletes for the join tables and does NOT add a `HasOne<Tenant>()` FK; it instead defines the parent->child collections via `b.HasMany(x => x.AppointmentTypes).WithOne()` / `b.HasMany(x => x.Locations).WithOne()` with `OnDelete(DeleteBehavior.NoAction)`.
- `DoctorsAppService.GetListAsync` disables the `IMultiTenant` filter via `IDataFilter<IMultiTenant>.Disable()` ONLY when `CurrentTenant.Id == null` (host context); other read paths (`GetAsync`, `UpdateAsync`) do not disable the filter, so tests must enter the tenant context to reach a tenant-scoped Doctor (see `DoctorsAppServiceTests.GetAsync`).
- `DoctorTenantAppService.CreateAsync` uses `CurrentTenant.Change(tenantId)` to seed the Doctor profile in the new tenant.

## Mapper Configuration

In `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly, NOT AutoMapper):

| Mapper class | Source -> Destination | AfterMap |
|---|---|---|
| `DoctorToDoctorDtoMappers` | `Doctor` -> `DoctorDto` | No AfterMap -- DisplayName not set (does not target LookupDto) |
| `DoctorWithNavigationPropertiesToDoctorWithNavigationPropertiesDtoMapper` | `DoctorWithNavigationProperties` -> `DoctorWithNavigationPropertiesDto` | No AfterMap |

No `DoctorToLookupDtoGuidMapper` exists -- Doctor is never the source of a lookup endpoint. (The four lookup endpoints on `DoctorsAppService` map FROM IdentityUser / Tenant / AppointmentType / Location to LookupDto, which are owned by those features' mappers.)

## Permissions

Defined in `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs` (lines 70-76):

```
CaseEvaluation.Doctors          (Default -- list, get, all 4 lookup endpoints)
CaseEvaluation.Doctors.Create   (CreateAsync)
CaseEvaluation.Doctors.Edit     (UpdateAsync)
CaseEvaluation.Doctors.Delete   (DeleteAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` (lines 43-46):

```csharp
var doctorPermission = myGroup.AddPermission(CaseEvaluationPermissions.Doctors.Default, L("Permission:Doctors"));
doctorPermission.AddChild(CaseEvaluationPermissions.Doctors.Create, L("Permission:Create"));
doctorPermission.AddChild(CaseEvaluationPermissions.Doctors.Edit,   L("Permission:Edit"));
doctorPermission.AddChild(CaseEvaluationPermissions.Doctors.Delete, L("Permission:Delete"));
```

`DoctorsAppService` has `[Authorize(CaseEvaluationPermissions.Doctors.Default)]` at the class level; Create/Edit/Delete add their child permission as a method-level `[Authorize]`. `DoctorTenantAppService` has no `[Authorize]` decoration -- access is governed by the inherited SaaS host permissions.

## Business Rules

1. One-doctor-per-tenant model (intent). The tenant IS the doctor; downstream entities (`DoctorAvailability`, `Appointment`) intentionally have NO `DoctorId` FK and rely on tenant scope to identify the doctor. Source: `docs/product/doctors.md` (Adrian-confirmed 2026-04-24).
2. UpdateAsync syncs the linked IdentityUser. When `input.IdentityUserId.HasValue`, `DoctorsAppService.UpdateAsync` calls `_userManager.FindByIdAsync(...)`, sets `user.Name = FirstName`, `user.Surname = LastName`, `user.SetEmailAsync(Email)`, and `_userManager.UpdateAsync(user)`. Failures throw `UserFriendlyException`. Side-effect not visible in the method signature.
3. Collection sync is replace-all. `DoctorManager.SetAppointmentTypesAsync` and `SetLocationsAsync` first verify each ID exists in the lookup table, then call `RemoveAllExceptGivenIds(...)` followed by `Add(...)` for each ID. Sending a partial list on update REMOVES the omitted IDs. Empty/null list -> calls `RemoveAll...()` (clears the collection). If every supplied ID is missing from the lookup table, the method returns without changing anything (does NOT clear).
4. Gender defaults to first enum value on Create. `DoctorCreateDto.Gender = Enum.GetValues<Gender>()[0]` resolves to `Gender.Male` (=1). Code-gen artifact, not a deliberate semantic.
5. Tenant provisioning override. `DoctorTenantAppService.CreateAsync(SaasTenantCreateDto)` calls `base.CreateAsync(input)` in a separate non-transactional UoW, then under `CurrentTenant.Change(tenant.Id)`: creates or updates an admin IdentityUser with the tenant name, creates a Doctor profile (`firstName = input.Name`, `lastName = ""`, `gender = Male`), and ensures a tenant-scoped "Doctor" role exists. If a Doctor with that IdentityUserId already exists, it is updated rather than re-created.
6. Cross-tenant lookups. `GetIdentityUserLookup`, `GetTenantLookup`, `GetAppointmentTypeLookup`, `GetLocationLookup` query host-scoped reference tables and are NOT tenant-filtered (`GetListAsync` disables the filter for host; the lookup methods themselves rely on the source repositories' default scope).
7. No uniqueness checks. Neither `DoctorManager.CreateAsync` nor `DoctorsAppService.CreateAsync` checks for duplicate Email or IdentityUserId. Two Doctor rows pointing at the same IdentityUser are permitted.
8. Frozen fields on Update: none -- `UpdateAsync` accepts every field that `CreateAsync` accepts, plus `concurrencyStamp`.

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `DoctorAppointmentType.DoctorId` | Cascade (host) / NoAction (tenant) | No | M2M join; configured in both DbContexts |
| `DoctorLocation.DoctorId` | Cascade (host) / NoAction (tenant) | No | M2M join; configured in both DbContexts |

No FK from `Appointment` or `DoctorAvailability` to `Doctor` -- those entities reach the doctor via tenant scope, consistent with the one-doctor-per-tenant model.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| DoctorComponent | `angular/src/app/doctors/doctor/components/doctor.component.ts` | matched via `DOCTOR_ROUTES` lazy `loadComponent`; mounted at the `doctors` segment in the parent module | List page |
| AbstractDoctorComponent | `angular/src/app/doctors/doctor/components/doctor.abstract.component.ts` | -- | Abstract base for list logic |
| DoctorDetailModalComponent | `angular/src/app/doctors/doctor/components/doctor-detail.component.ts` | -- | Tabbed create/edit modal |

Pattern detection: `DOCTOR_ROUTES` uses `loadComponent` (Angular standalone-route style) but the components themselves follow the ABP Suite `Abstract*Component` + `*Component` pair plus abstract/concrete services -- this is the ABP Suite scaffold pattern wired through standalone route loading.

Forms (Detail modal, three tabs):
- Tab 1 -- Doctor: `firstName` (text, required, max 50), `lastName` (text, required, max 50), `email` (email, required, max 49), `gender` (select), `identityUserId` (lookup, optional)
- Tab 2 -- AppointmentTypes: `appointmentTypeIds` (multi-select lookup, M2M)
- Tab 3 -- Locations: `locationIds` (multi-select lookup, M2M)

Filters on list page: `filterText`, `firstName`, `lastName`, `email`, `identityUserId`, `appointmentTypeId`, `locationId`.

Permission guards:
- Route: `canActivate: [authGuard, permissionGuard]` in `doctor-routes.ts`. The required permission name is configured in the parent route metadata via `doctor-route.provider.ts` (resolves to `CaseEvaluation.Doctors`).
- Template-level: `*abpPermission="'CaseEvaluation.Doctors.Create'"` for the new button, `'CaseEvaluation.Doctors.Edit'` for edit, `'CaseEvaluation.Doctors.Delete'` for delete (typical ABP Suite scaffold).

Services injected (abstract + concrete component pair): `ListService`, `DoctorService` (proxy), `PermissionService`, `DoctorViewService`, `DoctorDetailViewService`.

## Known Gotchas

1. Email max length is 49, not 50/100/256. `DoctorConsts.EmailMaxLength = 49`. ASP.NET Identity convention is 256, Patient uses 50. Code-gen artifact; tracked in `docs/issues/research/Q-07.md` for fix during MVP. Not blocking.
2. Explicit `HasOne<Tenant>()` FK on Doctor (host DB only). `CaseEvaluationDbContext` line 96 adds `b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull)`. Most `IMultiTenant` entities rely solely on the convention (`TenantId` column without an FK constraint). The tenant DbContext does NOT add this FK.
3. Host vs Tenant cascade differs. Host DB uses `Cascade` for join-table FKs; Tenant DB uses `NoAction`. Deleting a Doctor in tenant context will fail at the DB level if any join rows exist; in host context the join rows are deleted automatically.
4. Two AppServices, two proxies. `DoctorsAppService` (CRUD) and `DoctorTenantAppService` (tenant provisioning, extends `Volo.Saas.Host.TenantAppService`). The tenant service has no permission decoration of its own and lives at a different controller surface (`doctor-tenant.service.ts` proxy).
5. `GetAsync` and `UpdateAsync` do NOT disable the `IMultiTenant` filter. Tests confirm: looking up a tenant-scoped Doctor from host context fails unless wrapped in `_currentTenant.Change(...)`. Only `GetListAsync` disables the filter, and only when `CurrentTenant.Id == null`.
6. `DoctorTenantAppService.CreateAsync` seeds `lastName = ""`. The Doctor entity allows empty strings for `LastName` (the `Check.Length` minimum is 0). Seeded record will fail the standard create form validation if re-saved without a real lastName.
7. Constructor completeness. The `Doctor(Guid id, Guid? identityUserId, string firstName, string lastName, string email, Gender gender)` constructor sets 5 of 5 settable scalar fields plus IdentityUserId; the M2M collections are not constructor parameters and are populated post-construction by the Manager's `SetAppointmentTypesAsync` / `SetLocationsAsync` (idiomatic for collections, not a gap).
8. No uniqueness validation on Email or IdentityUserId at create time. Duplicates are not blocked at the AppService or domain layer.
9. Tests exist (unusual in this codebase). `DoctorApplicationTests` (5 facts) and `DoctorRepositoryTests` (2 facts) cover CRUD and filtered list/count. They use synthetic random-hex test data and TenantA seed scope.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Product intent: [docs/product/doctors.md](/docs/product/doctors.md)
- Docs: [docs/features/doctors/overview.md](/docs/features/doctors/overview.md) (if exists)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
