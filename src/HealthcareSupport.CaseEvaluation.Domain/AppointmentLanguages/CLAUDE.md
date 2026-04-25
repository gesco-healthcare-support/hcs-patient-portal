# AppointmentLanguages

<!-- TODO: product-intent input needed (deferred to T8 lookup-cluster interview) -->

Lookup table for the spoken language a patient prefers at appointments (used to flag interpreter requirements). Configured in BOTH the host DbContext (inside an `IsHostDatabase()` guard) AND the tenant DbContext, but the entity itself is NOT `IMultiTenant` -- effectively a host-shared lookup duplicated in tenant schemas. Referenced by `Patient.AppointmentLanguageId` (SetNull on delete).

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentLanguages/AppointmentLanguageConsts.cs` | `NameMaxLength = 50`; default sorting helper (`{prefix}CreationTime desc`) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/AppointmentLanguage.cs` | Entity (`FullAuditedEntity<Guid>` -- NOT `AggregateRoot`, NOT `IMultiTenant`) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/AppointmentLanguageManager.cs` | `DomainService` -- `CreateAsync(name)` and `UpdateAsync(id, name)` |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/IAppointmentLanguageRepository.cs` | Repo interface adding `GetListAsync(filterText, sorting, paging)` and `GetCountAsync(filterText)` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentLanguages/AppointmentLanguageDto.cs` | Output DTO (`FullAuditedEntityDto<Guid>` + `Name`) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentLanguages/AppointmentLanguageCreateDto.cs` | Create DTO -- `[Required]`, `[StringLength(50)]`, `Name` defaults to `"English"` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentLanguages/AppointmentLanguageUpdateDto.cs` | Update DTO -- `[Required]`, `[StringLength(50)]` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentLanguages/GetAppointmentLanguagesInput.cs` | Paged/sorted query (`PagedAndSortedResultRequestDto` + `FilterText`) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentLanguages/IAppointmentLanguagesAppService.cs` | App service interface (5 methods: list/get/create/update/delete) |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/AppointmentLanguages/AppointmentLanguagesAppService.cs` | CRUD AppService -- class-level `[Authorize(.Default)]`, method-level Create/Edit/Delete; `[RemoteService(IsEnabled = false)]` |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentLanguages/AppointmentLanguageController.cs` | 5 endpoints under `api/app/appointment-languages` (delegating to AppService) |
| Mapper | `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` | `AppointmentLanguageToAppointmentLanguageDtoMappers` and `AppointmentLanguageToLookupDtoGuidMapper` (Riok.Mapperly) |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` | `DbSet<AppointmentLanguage>`; entity config inside `IsHostDatabase()` guard |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` | `DbSet<AppointmentLanguage>`; entity config also present (NOT host-guarded here) |
| Localization | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` | Keys: `Permission:AppointmentLanguages`, `AppointmentLanguages`, `NewAppointmentLanguage`, `AppointmentLanguage`, `Menu:AppointmentLanguages` |
| Angular | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.abstract.component.ts` | Abstract `@Directive` -- list/edit/delete handlers, permission probe |
| Angular | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.component.ts` | Concrete component (template + styles in adjacent `.html`/inline `styles`) |
| Angular | `angular/src/app/appointment-languages/appointment-language/components/appointment-language-detail.component.ts` | Detail/edit modal component (template in adjacent `.html`) |
| Angular | `angular/src/app/appointment-languages/appointment-language/services/appointment-language.abstract.service.ts` | Abstract list/view service |
| Angular | `angular/src/app/appointment-languages/appointment-language/services/appointment-language.service.ts` | Concrete list/view service |
| Angular | `angular/src/app/appointment-languages/appointment-language/services/appointment-language-detail.abstract.service.ts` | Abstract detail/form service |
| Angular | `angular/src/app/appointment-languages/appointment-language/services/appointment-language-detail.service.ts` | Concrete detail/form service |
| Angular | `angular/src/app/appointment-languages/appointment-language/appointment-language-routes.ts` | `APPOINTMENT_LANGUAGE_ROUTES` with `authGuard`, `permissionGuard` |
| Angular | `angular/src/app/appointment-languages/appointment-language/providers/appointment-language-base.routes.ts` | Menu registration (`/appointment-management/appointment-languages`, requires `CaseEvaluation.AppointmentLanguages`) |
| Angular | `angular/src/app/appointment-languages/appointment-language/providers/appointment-language-route.provider.ts` | Route provider wiring base routes into the shell |
| Angular (proxy) | `angular/src/app/proxy/appointment-languages/appointment-language.service.ts` | Generated AppService proxy |
| Angular (proxy) | `angular/src/app/proxy/appointment-languages/models.ts` | Generated DTOs (`AppointmentLanguageDto`, Create/Update DTOs, input) |
| Angular (proxy) | `angular/src/app/proxy/appointment-languages/index.ts` | Proxy barrel export |
| Tests | -- | Not found (no test coverage for this entity) |
| Product docs | `docs/product/appointment-languages.md` | Not found (deferred to T8 lookup-cluster interview) |

## Entity Shape

```
AppointmentLanguage : FullAuditedEntity<Guid>     (NOT AggregateRoot, NOT IMultiTenant)
  Name : string  -- required, max 50 chars (Check.NotNull + Check.Length in ctor)
```

Constructor: `AppointmentLanguage(Guid id, string name)`. Sets `Id` and `Name` only. Protected parameterless ctor for EF.

No status/state enum -- this is a flat lookup record.

## Relationships

- No outbound FKs (no Guid foreign-key properties on the entity itself).
- No navigation properties on the entity.

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `Patient.AppointmentLanguageId` | SetNull | No (configured in tenant DbContext, outside any `IsHostDatabase()` guard) | Patient's preferred spoken language for appointments / interpreter need |

## Multi-tenancy

- **IMultiTenant: No.** The entity does not implement `IMultiTenant`.
- DbContext placement is unusual: entity-config block exists in BOTH `CaseEvaluationDbContext` (inside `if (builder.IsHostDatabase())`) AND `CaseEvaluationTenantDbContext` (unguarded). Both contexts also expose `DbSet<AppointmentLanguage>`.
- The `Patient.AppointmentLanguageId` FK is declared in the tenant DbContext (the `Patient` entity block), so the FK target table must exist in tenant schemas -- which the second config block ensures.
- Net effect: a per-tenant copy of the lookup table exists, but rows are NOT tenant-isolated by `IMultiTenant` filtering. Operationally it behaves like a per-tenant lookup that staff in each tenant can independently maintain.

## Mapper Configuration

Riok.Mapperly partial classes in `CaseEvaluationApplicationMappers.cs`:

- `AppointmentLanguageToAppointmentLanguageDtoMappers : MapperBase<AppointmentLanguage, AppointmentLanguageDto>` -- standard entity-to-DTO map, no AfterMap.
- `AppointmentLanguageToLookupDtoGuidMapper : MapperBase<AppointmentLanguage, LookupDto<Guid>>` -- ignores `DisplayName` from the auto-map and assigns it in `AfterMap`:
  - `destination.DisplayName = source.Name;`

## Permissions

Defined in `CaseEvaluationPermissions.AppointmentLanguages`:

```
CaseEvaluation.AppointmentLanguages          (Default)
CaseEvaluation.AppointmentLanguages.Create
CaseEvaluation.AppointmentLanguages.Edit
CaseEvaluation.AppointmentLanguages.Delete
```

Registered in `CaseEvaluationPermissionDefinitionProvider` with localization keys `Permission:AppointmentLanguages`, `Permission:Create`, `Permission:Edit`, `Permission:Delete`.

UI checks (Angular):

- Route guard: `authGuard`, `permissionGuard` (menu requires `CaseEvaluation.AppointmentLanguages`).
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Create'"` -- new-record button.
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Edit'"` -- edit row action.
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Delete'"` -- delete row action.
- `permissionService.getGrantedPolicy(...)` probed in the abstract component to hide the actions column when the user has neither Edit nor Delete.

## Business Rules

- **CreateDto defaults `Name` to `"English"`** -- a class-level field initializer (`public string Name { get; set; } = "English";`). The AppService and Manager do NOT override or validate this default; whatever the client posts is what is saved (subject to `[Required]` + `[StringLength(50)]`).
- **No uniqueness check on `Name`** -- the AppService/Manager do not query for an existing record before insert; duplicate language names are allowed at the application layer (no DB unique index either).
- **Length enforced in three places** -- `Check.Length(name, ..., 50, 0)` in the entity ctor, `Check.Length(name, ..., NameMaxLength)` in `Manager.CreateAsync` and `Manager.UpdateAsync`, `[StringLength(50)]` on both DTOs, and `HasMaxLength(50)` in the EF Core entity config.
- **Permission decoration is consistent** -- class-level `[Authorize(.Default)]` covers `GetListAsync`/`GetAsync`; `Create`/`Update`/`Delete` are individually decorated.
- **`[RemoteService(IsEnabled = false)]`** on the AppService -- the auto-API-controller is suppressed; the explicit `AppointmentLanguageController` is the only HTTP surface.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| `AbstractAppointmentLanguageComponent` | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.abstract.component.ts` | -- | Abstract `@Directive` -- list state, action handlers, permission gating |
| `AppointmentLanguageComponent` | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.component.ts` | `/appointment-management/appointment-languages` | Concrete list page (template + inline styles in adjacent `.html`/`styles`) |
| `AppointmentLanguageDetailModalComponent` | `angular/src/app/appointment-languages/appointment-language/components/appointment-language-detail.component.ts` | -- | Modal for create/edit (template in adjacent `.html`) |

**Pattern:** ABP Suite scaffold (abstract `@Directive` + concrete `@Component`). Both component files use `imports: [...]` arrays (Angular 14+ standalone API) -- `standalone` is not explicitly declared but the imports-array shape and the lazy-loaded `loadComponent` route confirm standalone usage.

**Forms (detail modal `.html`):**

- `name`: text input (`formControlName="name"`, `maxlength="50"`, `autofocus`) -- the only field.

**Permission guards:**

- Route: `authGuard`, `permissionGuard` (menu node requires policy `CaseEvaluation.AppointmentLanguages`).
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Create'"` -- new-record button in the list page.
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Edit'"` -- edit row action.
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Delete'"` -- delete row action.

**Services injected (abstract component, via `inject()`):**

- `ListService` (paged list state)
- `AppointmentLanguageViewService` (list view-model)
- `AppointmentLanguageDetailViewService` (detail/modal view-model)
- `PermissionService` (action-button visibility)

The detail modal injects `AppointmentLanguageDetailViewService`.

## Known Gotchas

1. **No `IDataSeedContributor` for AppointmentLanguages.** Per `docs/gap-analysis/01-domain-foundation.md:138`, no `AppointmentLanguageDataSeedContributor` exists. With no seed, the patient-form language dropdown shows "No data" until rows are created via the admin UI. Staff must manually create at least one row before patient intake can capture an `AppointmentLanguageId`. (Verified: zero matches for `AppointmentLanguageDataSeedContributor` under `src/`; only `Saas`, `OpenIddict`, `ExternalUserRole`, and `BookStoreDataSeederContributor` are present.)
2. **`FullAuditedEntity<Guid>`, not `FullAuditedAggregateRoot<Guid>`.** Lighter than the Reference Pattern (Appointments). Fine for a flat lookup, but inconsistent with peers like `AppointmentStatus` -- worth flagging during the T8 lookup-cluster review.
3. **Dual DbContext configuration.** Same entity-config block lives in both DbContexts. The host block is `IsHostDatabase()`-guarded; the tenant block is not. This is the inverse of the typical "configure once, share via host" pattern -- intentional because `Patient.AppointmentLanguageId` is a tenant-side FK, so the table must exist in tenant schemas too.
4. **No tests.** No xUnit project references this entity. Coverage gap.
5. **No uniqueness on `Name`.** Two rows with `Name = "English"` are accepted by the AppService and DB. Not necessarily a bug, but downstream lookup-dropdown UX will show duplicate options.
6. **Constructor coverage.** Constructor sets 1/1 settable business field (`Name`). No code-gen artifact -- complete.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Gap analysis: `docs/gap-analysis/01-domain-foundation.md` (line 138 -- missing seed contributor)
- Product intent: -- (deferred to T8 lookup-cluster interview)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
