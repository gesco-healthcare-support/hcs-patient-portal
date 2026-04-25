# WcabOffices

Host-scoped lookup table for Workers' Compensation Appeals Board (WCAB) offices used by appointment workflows for jurisdiction tracking. Showcase pattern entity: demonstrates bulk delete + Excel export + download-token CSRF in a single feature.

<!-- TODO: product-intent input needed (deferred to T8 lookup-cluster interview) -->

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/WcabOffices/WcabOfficeConsts.cs` | Max-length constants (Name=50, Abbreviation=50, Address=100, City=50, ZipCode=15) and default sorting |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/WcabOffice.cs` | FullAuditedAggregateRoot, NOT IMultiTenant; constructor enforces Check.Length on all string fields |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/WcabOfficeManager.cs` | DomainService -- CreateAsync / UpdateAsync; Update applies SetConcurrencyStampIfNotNull |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/IWcabOfficeRepository.cs` | Custom repo: GetList, GetCount, GetWithNavigationProperties, DeleteAll (filtered) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/WcabOfficeWithNavigationProperties.cs` | Compound type: WcabOffice + State navigation |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/WcabOffices/IWcabOfficesAppService.cs` | App service interface (11 methods) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/WcabOffices/WcabOfficeDto.cs` | Read DTO |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/WcabOffices/WcabOfficeCreateDto.cs` | Create input |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/WcabOffices/WcabOfficeUpdateDto.cs` | Update input (with ConcurrencyStamp) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/WcabOffices/WcabOfficeWithNavigationPropertiesDto.cs` | DTO with State nav |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/WcabOffices/GetWcabOfficesInput.cs` | Paged/filter input |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/WcabOffices/WcabOfficeExcelDto.cs` | Excel row shape |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/WcabOffices/WcabOfficeExcelDownloadDto.cs` | Excel input + DownloadToken |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/WcabOffices/WcabOfficesAppService.cs` | CRUD + bulk delete + Excel export + download-token issue/validate |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/WcabOffices/WcabOfficeDownloadTokenCacheItem.cs` | Distributed-cache item; underlies CSRF protection for Excel download |
| EFCore | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/WcabOffices/EfCoreWcabOfficeRepository.cs` | Repository implementation |
| EFCore | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` | OnModelCreating: WcabOffice config inside `IsHostDatabase()`; `HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(SetNull)` |
| EFCore | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260202193114_Added_WcabOffice.cs` | Initial migration |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/WcabOffices/WcabOfficeController.cs` | 11 endpoints under `api/app/wcab-offices` |
| Angular | `angular/src/app/wcab-offices/wcab-office/wcab-office-routes.ts` | Standalone route with `authGuard` + `permissionGuard` |
| Angular | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.component.ts` | Concrete standalone component (template + styles in adjacent .html / inline `styles`) |
| Angular | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.abstract.component.ts` | Abstract directive: list / showForm / create / update / delete / exportToExcel / permission visibility |
| Angular | `angular/src/app/wcab-offices/wcab-office/components/wcab-office-detail.component.ts` | Detail/edit modal (template in adjacent .html) |
| Angular | `angular/src/app/wcab-offices/wcab-office/services/` | `wcab-office.service.ts` (list view), `wcab-office-detail.service.ts` (detail view) |
| Angular | `angular/src/app/wcab-offices/wcab-office/providers/` | DI providers for list/detail services |

## Entity Shape

```
WcabOffice : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant -- host-scoped)
- Name         : string  [required, max 50]
- Abbreviation : string  [required, max 50]
- Address      : string? [max 100]
- City         : string? [max 50]
- ZipCode      : string? [max 15]
- IsActive     : bool
- StateId      : Guid?   (FK -> State, optional)
```

No state-machine enum; `IsActive` is a simple toggle.

## Relationships

| FK Property | Target Entity | Delete Behavior | Configured In |
|---|---|---|---|
| `StateId` | `State` | `SetNull` | `CaseEvaluationDbContext.OnModelCreating` inside `IsHostDatabase()` block |

`WcabOfficeWithNavigationProperties` exposes the `State` navigation alongside the entity.

## Multi-tenancy

**IMultiTenant: No.** Host-scoped reference data shared across tenants. DbContext configuration lives in `CaseEvaluationDbContext.OnModelCreating()` inside an `if (builder.IsHostDatabase())` guard, so the table is created only on the host database. `DbSet<WcabOffice>` is declared on `CaseEvaluationDbContext` only; no entry on the tenant context.

## Mapper Configuration

Riok.Mapperly partial classes in `CaseEvaluationApplicationMappers.cs`:

| Mapper Class | Source -> Destination | AfterMap? |
|---|---|---|
| `WcabOfficeToWcabOfficeDtoMappers` | `WcabOffice` -> `WcabOfficeDto` | No AfterMap |
| `WcabOfficeToWcabOfficeExcelDtoMappers` | `WcabOffice` -> `WcabOfficeExcelDto` | No AfterMap |
| `WcabOfficeWithNavigationPropertiesToWcabOfficeWithNavigationPropertiesDtoMapper` | `WcabOfficeWithNavigationProperties` -> `WcabOfficeWithNavigationPropertiesDto` | No AfterMap |

No `LookupDto<Guid>` mapper for WcabOffice itself (the AppService exposes `GetStateLookupAsync`, which uses the State entity's mapper).

## Permissions

```
CaseEvaluation.WcabOffices          (Default -- group permission)
CaseEvaluation.WcabOffices.Create
CaseEvaluation.WcabOffices.Edit
CaseEvaluation.WcabOffices.Delete   (covers DeleteAsync, DeleteByIdsAsync, DeleteAllAsync)
```

Class-level `[Authorize(CaseEvaluationPermissions.WcabOffices.Default)]` on the AppService; per-method `[Authorize(...Create|Edit|Delete)]` on the mutating endpoints. `GetListAsExcelFileAsync` is decorated `[AllowAnonymous]` -- access is gated by a short-lived download token instead.

## Business Rules

- **Download-token CSRF pattern (showcase)**: `GetDownloadTokenAsync` issues a 30-second `Guid.NewGuid().ToString("N")` token via `IDistributedCache<WcabOfficeDownloadTokenCacheItem, string>`. `GetListAsExcelFileAsync` is `[AllowAnonymous]` but rejects with `AbpAuthorizationException` unless `input.DownloadToken` matches the cached entry. The anonymous endpoint is the deliberate trade-off that lets the browser hit the URL directly without an Authorization header; the token replaces auth for that single request.
- **Bulk delete (showcase)**: `DeleteByIdsAsync(List<Guid>)` and `DeleteAllAsync(GetWcabOfficesInput)` both gated by `WcabOffices.Delete`. `DeleteAllAsync` honors the same filter set as `GetListAsync` so callers cannot accidentally wipe the table by passing an empty filter without intent.
- **Excel export (showcase)**: `GetListAsExcelFileAsync` projects to an anonymous shape `{ Name, Abbreviation, Address, City, ZipCode, IsActive, State = item.State?.Name }` and streams via `MiniExcel`. State name is flattened from the navigation property.
- **Length validation is double-enforced**: entity constructor calls `Check.Length` for Name / Abbreviation / Address / City / ZipCode AND `WcabOfficeManager` repeats the same checks before constructing. EF column metadata also applies `HasMaxLength`. Three layers must be kept in sync if a max length changes.
- **Concurrency**: `UpdateAsync` calls `SetConcurrencyStampIfNotNull(concurrencyStamp)` so optimistic-concurrency conflicts surface to the client when the stamp is provided.
- **No uniqueness check**: neither `Name` nor `Abbreviation` is enforced unique by the AppService or DbContext; duplicates are allowed.
- **`IsActive` is not filtered server-side by default**: the AppService passes through whatever `input.IsActive` the caller supplies; there is no implicit "only active" lookup endpoint.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| `WcabOfficeComponent` | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.component.ts` | `/...` (lazy-loaded via `WCAB_OFFICE_ROUTES`) | List view: filters, bulk delete, Excel export, create/edit modal |
| `AbstractWcabOfficeComponent` | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.abstract.component.ts` | -- | Abstract directive holding list / detail / permission logic |
| `WcabOfficeDetailModalComponent` | `angular/src/app/wcab-offices/wcab-office/components/wcab-office-detail.component.ts` | -- | Modal for create / edit |

**Pattern detection**: `WcabOfficeComponent` does NOT declare `standalone: true` explicitly, but uses an `imports: [...]` array on the `@Component` decorator (Angular 20 default-standalone behavior) and is `loadComponent`'ed directly in `WCAB_OFFICE_ROUTES`. There is also an `Abstract...Component` + concrete pair. This is a hybrid: ABP Suite scaffold style (abstract/concrete) running on Angular 20 standalone APIs, with no NgModule.

**Forms** (per `wcab-office-detail.component.html` and DTO field shapes):
- `name`: text -- required, maxLength 50
- `abbreviation`: text -- required, maxLength 50
- `address`: text -- maxLength 100
- `city`: text -- maxLength 50
- `zipCode`: text -- maxLength 15
- `isActive`: checkbox
- `stateId`: lookup-select bound to `GetStateLookupAsync`

**Permission guards**:
- Route: `canActivate: [authGuard, permissionGuard]`. `permissionGuard` reads the route's required permission (configured via ABP route metadata, typically `CaseEvaluation.WcabOffices`).
- Template directives (HTML uses `*abpPermission`): `'CaseEvaluation.WcabOffices.Create'` (create button), `'CaseEvaluation.WcabOffices.Edit'` (edit action), `'CaseEvaluation.WcabOffices.Delete'` (delete + bulk delete).
- `AbstractWcabOfficeComponent.checkActionButtonVisibility()` calls `permissionService.getGrantedPolicy('CaseEvaluation.WcabOffices.Edit' | '.Delete')` to hide the action column when neither is granted.

**Services injected** (via `inject()` in the abstract component):
- `ListService` (ABP list state)
- `WcabOfficeViewService` (list view glue)
- `WcabOfficeDetailViewService` (detail/modal glue, owns `selected`, `showForm`, `update`)
- `PermissionService` (action-column visibility)

## Known Gotchas

- **No automated tests** (deferred). Coverage gap surfaces in MVP gap-analysis (lookup cluster).
- **No `docs/product/` page yet** -- product-intent narrative deferred to T8 lookup-cluster interview (see banner above).
- **Anonymous Excel endpoint relies on cache**: if the distributed cache is unavailable or the token entry expires before the browser request fires (30 s TTL), `GetListAsExcelFileAsync` throws `AbpAuthorizationException`. Reissue via `GetDownloadTokenAsync` and retry. This is by design but easy to misread as auth failure.
- **No uniqueness constraint on Name or Abbreviation**: duplicates are accepted by the API. If product wants uniqueness, it must be added to both `WcabOfficeManager` and a unique index in EF config.
- **Constructor sets 8/8 settable fields** (Id, StateId, Name, Abbreviation, IsActive, Address, City, ZipCode); no post-construction property assignment in `WcabOfficeManager.CreateAsync`. No code-gen artifact here.
- **Abstract / concrete split with Angular 20 standalone**: the abstract directive is not itself standalone; it is used purely as a base class. Adding `standalone: true` to the abstract directive would be a no-op but could mislead future readers.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Docs: -- (no `docs/product/wcab-offices/` page yet; T8 lookup-cluster interview pending)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
