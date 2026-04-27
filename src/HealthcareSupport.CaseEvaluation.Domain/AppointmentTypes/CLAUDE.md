# AppointmentTypes

Host-scoped lookup table for IME appointment categories (e.g. orthopedic, neurological). Referenced by `Appointment.AppointmentTypeId` (required), filtered into `DoctorAvailability` and `Location` defaults, and joined to doctors via the `DoctorAppointmentType` M2M table.

<!-- TODO: product-intent input needed (deferred to T8 lookup-cluster interview) -->

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentTypes/AppointmentTypeConsts.cs` | NameMaxLength=100, DescriptionMaxLength=200, default sort `CreationTime desc` |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/AppointmentType.cs` | Entity (`FullAuditedEntity<Guid>`, no `IMultiTenant`); owns `DoctorAppointmentTypes` collection |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/AppointmentTypeManager.cs` | DomainService -- CreateAsync, UpdateAsync (Check.NotNullOrWhiteSpace + Check.Length) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/IAppointmentTypeRepository.cs` | Custom repository: GetListAsync (filterText/name/sort/page) + GetCountAsync |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentTypes/IAppointmentTypesAppService.cs` | App service contract: GetList, Get, Create, Update, Delete |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentTypes/AppointmentTypeDto.cs` | Read DTO (`FullAuditedEntityDto<Guid>`) -- Name, Description |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentTypes/AppointmentTypeCreateDto.cs` | Create DTO -- `[Required]` Name, optional Description; `[StringLength]` from Consts |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentTypes/AppointmentTypeUpdateDto.cs` | Update DTO -- same shape as Create |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentTypes/GetAppointmentTypesInput.cs` | Paged input -- FilterText, Name |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentTypes/AppointmentTypeExcelDto.cs` | Excel row DTO -- Name, Description |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentTypes/AppointmentTypeExcelDownloadDto.cs` | Excel download token + filter inputs |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/AppointmentTypes/AppointmentTypesAppService.cs` | CRUD via Manager; class-level `[Authorize]`, specific perms on Create/Edit/Delete; `[RemoteService(IsEnabled = false)]` |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/AppointmentTypes/AppointmentTypeDownloadTokenCacheItem.cs` | Distributed-cache token shape for Excel download (Token: string) |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` (lines 60-129) | Riok.Mapperly partial classes for Entity->Dto, Entity->ExcelDto, Entity->LookupDto |
| Permissions | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs` | Nested `AppointmentTypes` constants (Default/Create/Edit/Delete) |
| Permissions | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` | Registers AppointmentTypes permission tree |
| EF / DbContext | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` (host-side) | `builder.Entity<AppointmentType>` inside `IsHostDatabase()` block |
| EF / DbContext | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` | Host shadow registration (DbSet + builder.Entity) for tenant-context queries |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentTypes/AppointmentTypeController.cs` | 5 endpoints under `api/app/appointment-types` (GET list, GET by id, POST, PUT, DELETE) |
| Angular | `angular/src/app/appointment-types/appointment-type/appointment-type-routes.ts` | Lazy route, `authGuard` + `permissionGuard` |
| Angular | `angular/src/app/appointment-types/appointment-type/providers/appointment-type-base.routes.ts` | ABP menu registration (`/appointment-management/appointment-types`, requiredPolicy `CaseEvaluation.AppointmentTypes`) |
| Angular | `angular/src/app/appointment-types/appointment-type/providers/appointment-type-route.provider.ts` | `provideAppInitializer` wiring base routes into `RoutesService` |
| Angular | `angular/src/app/appointment-types/appointment-type/components/appointment-type.abstract.component.ts` | Abstract directive -- list state, create/update/delete delegation, action-button visibility |
| Angular | `angular/src/app/appointment-types/appointment-type/components/appointment-type.component.ts` | Concrete list page (template + inline styles in adjacent `.html`) |
| Angular | `angular/src/app/appointment-types/appointment-type/components/appointment-type-detail.component.ts` | Create/edit modal (template in adjacent `.html`) |
| Angular | `angular/src/app/appointment-types/appointment-type/services/appointment-type.abstract.service.ts` | Generated abstract list-service (filters, hookToQuery, bulk delete) |
| Angular | `angular/src/app/appointment-types/appointment-type/services/appointment-type.service.ts` | Concrete list-service binding |
| Angular | `angular/src/app/appointment-types/appointment-type/services/appointment-type-detail.abstract.service.ts` | Generated abstract detail-service (form, submit, lookup loading) |
| Angular | `angular/src/app/appointment-types/appointment-type/services/appointment-type-detail.service.ts` | Concrete detail-service binding |

Tests: not found (coverage gap). `docs/product/appointment-types/` not present (deferred to T8).

## Entity Shape

```
AppointmentType : FullAuditedEntity<Guid>          (NO IMultiTenant -- host-scoped lookup)
  Name                    : string  [required, max 100]
  Description             : string? [optional, max 200]
  DoctorAppointmentTypes  : ICollection<DoctorAppointmentType>   (M2M with Doctor)
```

Constructor: `AppointmentType(Guid id, string name, string? description = null)` -- runs `Check.NotNull` and `Check.Length` on both fields. No state/status enum, no derived state machine.

## Relationships

Outbound (this entity owns):
- `DoctorAppointmentTypes` -- collection navigation to the `DoctorAppointmentType` join entity (M2M with `Doctor`).

Inbound (other entities reference `AppointmentType.Id`):
- `Appointment.AppointmentTypeId` -- required FK, NoAction.
- `DoctorAvailability.AppointmentTypeId` -- optional FK, SetNull.
- `Location.AppointmentTypeId` -- optional FK (default appointment type for the location), SetNull.
- `DoctorAppointmentType.AppointmentTypeId` -- composite-key part, Cascade.

## Multi-tenancy

**IMultiTenant: No.** The entity is host-scoped: row data is shared across all tenants and managed only by host-level admins. `builder.Entity<AppointmentType>` is wrapped in `if (builder.IsHostDatabase())` in `CaseEvaluationDbContext.cs` (host context). The tenant-side `CaseEvaluationTenantDbContext.cs` also declares the entity and an unguarded `builder.Entity<AppointmentType>` block so EF can resolve the FKs from tenant-side aggregates (`Appointment`, `DoctorAvailability`); rows are never written from the tenant context.

## Mapper Configuration

Riok.Mapperly partial classes in `CaseEvaluationApplicationMappers.cs`:

| Mapper Class | Source -> Destination | AfterMap |
|---|---|---|
| `AppointmentTypeToAppointmentTypeDtoMappers` | `AppointmentType` -> `AppointmentTypeDto` | None |
| `AppointmentTypeToAppointmentTypeExcelDtoMappers` | `AppointmentType` -> `AppointmentTypeExcelDto` | None |
| `AppointmentTypeToLookupDtoGuidMapper` | `AppointmentType` -> `LookupDto<Guid>` | Yes -- `destination.DisplayName = source.Name` (verified in lines 125-128) |

`DisplayName` is `[MapperIgnoreTarget]`-decorated on both Map overloads so Mapperly does not try to bind it; the `AfterMap` override is the single source of truth.

## Permissions

Constants (`CaseEvaluationPermissions.cs`):

```
CaseEvaluation.AppointmentTypes           (Default)
CaseEvaluation.AppointmentTypes.Create
CaseEvaluation.AppointmentTypes.Edit
CaseEvaluation.AppointmentTypes.Delete
```

Registration (`CaseEvaluationPermissionDefinitionProvider.cs`): root permission added with `L("Permission:AppointmentTypes")`, then three child permissions for Create/Edit/Delete.

AppService surface:
- Class-level `[Authorize]` -- any authenticated user can `GetListAsync` and `GetAsync` (read).
- `[Authorize(CaseEvaluationPermissions.AppointmentTypes.Create)]` on `CreateAsync`.
- `[Authorize(CaseEvaluationPermissions.AppointmentTypes.Edit)]` on `UpdateAsync`.
- `[Authorize(CaseEvaluationPermissions.AppointmentTypes.Delete)]` on `DeleteAsync`.

Angular UI checks:
- Route guard: `permissionGuard` plus `requiredPolicy: 'CaseEvaluation.AppointmentTypes'` on the menu entry.
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Create'"` -- "New AppointmentType" toolbar button.
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Edit'"` -- row Edit action.
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Delete'"` -- row Delete action and bulk-delete button.

## Business Rules

Standard CRUD -- no validation beyond field-length checks, no uniqueness constraint, no computed fields, no frozen fields. Specifically:
- `CreateAsync` and `UpdateAsync` both call `Check.NotNullOrWhiteSpace(name)` plus `Check.Length(name)` and `Check.Length(description)` -- and that is the entire rule set.
- No `Any`/`FindAsync` uniqueness check on Name in `AppointmentTypeManager` or `AppointmentTypesAppService`.
- No DTO defaults beyond C# `null!` initialisers for non-nullable strings.
- No hardcoded numeric/format constraints in the AppService -- all length limits flow from `AppointmentTypeConsts`.
- `[RemoteService(IsEnabled = false)]` on the AppService is intentional: external callers must go through `AppointmentTypeController` (which delegates to the same AppService), not the auto-generated AppService endpoint.

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `DoctorAppointmentType.AppointmentTypeId` | Cascade | Yes (`IsHostDatabase()` guard in host DbContext) | Composite-key M2M with Doctor |
| `Location.AppointmentTypeId` | SetNull | Yes (`IsHostDatabase()` guard in host DbContext) | Optional default appointment type per location |
| `DoctorAvailability.AppointmentTypeId` | SetNull | No (configured outside the host guard) | Optional type for an availability slot |
| `Appointment.AppointmentTypeId` | NoAction | No (configured outside the host guard) | Required FK on every appointment |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| `AppointmentTypeComponent` | `angular/src/app/appointment-types/appointment-type/components/appointment-type.component.ts` | `/appointment-management/appointment-types` | Concrete list page (extends abstract); paged grid with bulk-select |
| `AbstractAppointmentTypeComponent` | `angular/src/app/appointment-types/appointment-type/components/appointment-type.abstract.component.ts` | -- | Generated base directive: list state, create/update/delete delegation, action-button visibility check |
| `AppointmentTypeDetailModalComponent` | `angular/src/app/appointment-types/appointment-type/components/appointment-type-detail.component.ts` | -- (modal) | Create/edit modal hosted by the list page |

**Pattern:** ABP Suite scaffold (abstract + concrete pair). The concrete components are NOT marked `standalone: true` directly, but each `@Component` decorator declares its own `imports: [...]`, indicating the project is on the standalone-import-style pattern that Angular 20 / ABP Commercial 10.0.2 generates. There is no per-feature `*.module.ts`.

**Forms (detail modal):**
- `name`: text input, `formControlName="name"`, `maxlength="100"`, `autofocus`. Marked required in template via `*` and validated via `[Required]` on `AppointmentTypeCreateDto`/`UpdateDto`.
- `description`: text input, `formControlName="description"`, `maxlength="200"`, optional.

**Permission guards:**
- Route: `canActivate: [authGuard, permissionGuard]` (in `appointment-type-routes.ts`); menu `requiredPolicy: 'CaseEvaluation.AppointmentTypes'` (in `appointment-type-base.routes.ts`).
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Create'"` -- New button.
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Edit'"` -- per-row Edit action.
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Delete'"` -- per-row Delete and bulk-delete button.
- `AbstractAppointmentTypeComponent.checkActionButtonVisibility()` calls `PermissionService.getGrantedPolicy('CaseEvaluation.AppointmentTypes.Edit'|'.Delete')` to hide the Actions column entirely when both are denied.

**Services injected:**
- Abstract component (`inject(...)`): `ListService`, `AppointmentTypeViewService`, `AppointmentTypeDetailViewService`, `PermissionService`.
- Concrete component: same chain via `extends AbstractAppointmentTypeComponent`; provides `ListService`, `AppointmentTypeViewService`, `AppointmentTypeDetailViewService`, plus `NgbDateAdapter`/`NgbTimeAdapter` adapter overrides in `providers`.
- Detail modal: `inject(AppointmentTypeDetailViewService)`.

**Imports of note (concrete list component):** `NgxDatatableModule`, `NgxDatatableDefaultDirective`, `NgxDatatableListDirective`, `PermissionDirective`, `LocalizationPipe`, `PageComponent`, `PageToolbarContainerComponent`, `AdvancedEntityFiltersComponent`, `AdvancedEntityFiltersFormComponent`, `AppointmentTypeDetailModalComponent`.

**Imports of note (detail modal):** `ModalComponent`, `ModalCloseDirective`, `ButtonComponent`, `AutofocusDirective`, `LocalizationPipe`, `NgxValidateCoreModule`, `NgbNavModule`.

## Known Gotchas

1. **`Description` field present.** Most lookup entities in this codebase carry only `Name`; `AppointmentType` is one of the few lookups with a free-text `Description`. Watch out when copy-pasting templates from `AppointmentStatus` / `AppointmentLanguage` (Name-only).
2. **No tests.** No `AppointmentType*Tests.cs` exists in the test projects -- coverage gap to track.
3. **Tenant-side DbContext duplicates the entity registration.** `CaseEvaluationTenantDbContext.cs` declares `DbSet<AppointmentType>` and a `builder.Entity<AppointmentType>` block outside any `IsHostDatabase()` guard. This is required for the tenant context to resolve FKs from `Appointment.AppointmentTypeId` and `DoctorAvailability.AppointmentTypeId`, but a future refactor should not interpret it as "AppointmentType is tenant-writable" -- writes only happen against the host DbContext.
4. **Constructor coverage is complete.** `AppointmentType(Guid id, string name, string? description = null)` covers every settable non-audit, non-collection property; nothing is set post-construction by the Manager. (Documented to confirm this is NOT a code-gen artifact.)
5. **`[RemoteService(IsEnabled = false)]` on the AppService.** The AppService is exposed only via the explicit `AppointmentTypeController`; the auto-generated AppService endpoint is suppressed. Do not enable it without coordinating with the controller route.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Docs: `docs/product/appointment-types/` (deferred to T8 lookup-cluster interview; not present yet)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
