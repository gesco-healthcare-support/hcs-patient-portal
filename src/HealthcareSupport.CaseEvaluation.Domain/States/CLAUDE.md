# States

Host-scoped lookup table for US states. Referenced by Location, WcabOffice, Patient, ApplicantAttorney, and AppointmentEmployerDetail (5 inbound FKs, all SetNull). Simplest lookup entity in the codebase -- a single `Name` field with no max-length constraint. Surfaced in Angular under the Configurations menu.

<!-- TODO: product-intent input needed (deferred to T8 lookup-cluster interview) -->

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/States/StateConsts.cs` | Default sorting helper -- no max-length constants defined |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/States/State.cs` | Aggregate root -- `FullAuditedAggregateRoot<Guid>`, no `IMultiTenant` |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/States/StateManager.cs` | DomainService -- `CreateAsync(name)`, `UpdateAsync(id, name, concurrencyStamp)` |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/States/IStateRepository.cs` | Custom repo interface -- `GetListAsync` with name filter, `GetCountAsync` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/States/StateCreateDto.cs` | Create input -- `Name` `[Required]` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/States/StateUpdateDto.cs` | Update input -- `Name` `[Required]` + `IHasConcurrencyStamp` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/States/StateDto.cs` | Output DTO -- `FullAuditedEntityDto<Guid>`, `IHasConcurrencyStamp` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/States/GetStatesInput.cs` | Filter input -- `PagedAndSortedResultRequestDto` with `FilterText` and `Name` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/States/IStatesAppService.cs` | Service interface -- 5 methods (GetList, Get, Create, Update, Delete) |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/States/StatesAppService.cs` | CRUD AppService -- delegates to `StateManager`, uses `ObjectMapper.Map` |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/States/EfCoreStateRepository.cs` | Repository implementation with name filtering |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/States/StateController.cs` | Manual controller -- 5 endpoints at `api/app/states` |
| Angular | `angular/src/app/states/state/components/state.component.ts` | List page -- extends `AbstractStateComponent` (concrete component, .ts/.html/.scss triplet) |
| Angular | `angular/src/app/states/state/components/state.abstract.component.ts` | Abstract base directive -- CRUD wiring, permission checks |
| Angular | `angular/src/app/states/state/components/state-detail.component.ts` | Modal -- single Name field |
| Angular | `angular/src/app/states/state/services/state.abstract.service.ts` | List data service |
| Angular | `angular/src/app/states/state/services/state-detail.abstract.service.ts` | Modal form service |
| Angular | `angular/src/app/states/state/providers/state-base.routes.ts` | Menu config -- under `Configurations` parent, icon `fas fa-flag` |
| Angular | `angular/src/app/states/state/providers/state-route.provider.ts` | Registers routes via ABP `RoutesService` |
| Angular | `angular/src/app/states/state/state-routes.ts` | Route definition -- `authGuard + permissionGuard` |
| Proxy | `angular/src/app/proxy/states/state.service.ts` | Auto-generated REST client |
| Proxy | `angular/src/app/proxy/states/models.ts` | Auto-generated TypeScript interfaces |

## Entity Shape

```
State : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant -- host-scoped)
+-- Name : string [required, no max length constraint in StateConsts]
```

Constructor: `State(Guid id, string name)` -- validates `Check.NotNull(name)`. The parameterless `protected State()` constructor exists for EF Core materialization.

No status/state enum -- this is a flat lookup with a single mutable field.

## Relationships

**Outbound:** None -- `State` has no FK references to other entities.

**Inbound:** 5 entities reference `State` via `StateId` (see Inbound FKs section below).

## Multi-tenancy

**IMultiTenant: No.** Intentionally host-scoped -- US states are shared reference data across all tenants.

- DbContext config: inside `if (builder.IsHostDatabase())` guard in `CaseEvaluationDbContext.cs`
- Not configured in `CaseEvaluationTenantDbContext.cs` (host-only)
- Table: `AppStates` (prefixed with `CaseEvaluationConsts.DbTablePrefix`)

## Mapper Configuration

In `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly, NOT AutoMapper):

| Mapper Class | Source -> Destination | AfterMap? |
|---|---|---|
| `StateToStateDtoMappers` | `State` -> `StateDto` | No |
| `StateToLookupDtoGuidMapper` | `State` -> `LookupDto<Guid>` | Yes -- `destination.DisplayName = source.Name` |

**Note:** `StatesAppService` uses `ObjectMapper.Map<List<State>, List<StateDto>>` rather than directly invoking the Mapperly mapper. ABP's `ObjectMapper` delegates to the registered Mapperly mapper under the hood.

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.States          (Default -- list/get + menu visibility)
CaseEvaluation.States.Create   (CreateAsync)
CaseEvaluation.States.Edit     (UpdateAsync)
CaseEvaluation.States.Delete   (DeleteAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children.

`StatesAppService` is decorated with `[Authorize(CaseEvaluationPermissions.States.Default)]` at the class level; `Create`, `Edit`, `Delete` carry their own per-method `[Authorize(...)]` overrides. `GetListAsync` and `GetAsync` inherit the class-level Default check.

**Angular usage:**
- Route: `permissionGuard` requires `CaseEvaluation.States`
- Menu: `requiredPolicy: 'CaseEvaluation.States'` in base routes
- Actions: `.Edit` and `.Delete` checked via `PermissionService.getGrantedPolicy()`

## Business Rules

Standard CRUD -- no validation, uniqueness, or computed fields.

- `StateManager.CreateAsync` validates `Check.NotNullOrWhiteSpace(name)` -- no uniqueness check against existing states
- `StateManager.UpdateAsync` validates the same name guard, then sets concurrency stamp via `state.SetConcurrencyStampIfNotNull(concurrencyStamp)`
- `StateCreateDto.Name` is `[Required]` and initialized to `null!` -- no default seed value
- `StateUpdateDto` carries `ConcurrencyStamp` for optimistic concurrency
- No numeric caps, no auto-generated codes, no computed fields
- No frozen fields -- `Name` is the only mutable property and is settable on both create and update paths
- No lookup-filtering endpoint on `IStatesAppService` (no `GetLookupAsync` defined here despite `StateToLookupDtoGuidMapper` being configured -- lookup consumption happens via the generic ABP lookup service infrastructure)

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `Location.StateId` | SetNull | Yes (IsHostDatabase guard) | Exam location address state |
| `WcabOffice.StateId` | SetNull | Yes (IsHostDatabase guard) | WCAB office address state |
| `Patient.StateId` | SetNull | No (both contexts) | Patient address state |
| `ApplicantAttorney.StateId` | SetNull | No (both contexts) | Attorney address state |
| `AppointmentEmployerDetail.StateId` | SetNull | No (both contexts) | Employer address state |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| StateComponent | `angular/src/app/states/state/components/state.component.ts` | `/configurations/states` | List view (concrete component) |
| AbstractStateComponent | `angular/src/app/states/state/components/state.abstract.component.ts` | -- | Abstract base directive with CRUD wiring |
| StateDetailModalComponent | `angular/src/app/states/state/components/state-detail.component.ts` | -- | Modal for create / edit |

**Pattern:** ABP Suite scaffold (abstract / concrete). `AbstractStateComponent` -> `StateComponent`. Simplest UI in the codebase -- single `Name` field, no lookups, no bulk operations, no `standalone: true` flag.

### ABP Suite scaffold (abstract/concrete)

**Forms:**
- `name`: text input (required; no max length constraint enforced in UI or backend Consts)

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.States`)
- `CaseEvaluation.States.Create` -- checked in abstract component for "New" button visibility
- `CaseEvaluation.States.Edit` -- checked for edit action visibility
- `CaseEvaluation.States.Delete` -- checked for delete action visibility

**Services injected:**
- `ListService`, `StateViewService`, `StateDetailViewService`, `PermissionService`

## Known Gotchas

1. **Most-referenced lookup entity** -- 5 other entities have `StateId` FKs to `State` (all `SetNull`). Deleting a state silently null-outs those FKs rather than cascading or blocking the delete. Confirm with product whether this is desired vs. blocking the delete when references exist.

2. **No `NameMaxLength` constant** -- unlike most other lookup entities (Location, WcabOffice, etc.), `StateConsts` does NOT define a `NameMaxLength` constant. The `Name` column has no explicit `HasMaxLength()` in the EF configuration (effectively `nvarchar(max)` on SQL Server). Inputs of arbitrary length will be accepted.

3. **No uniqueness check** -- two states with identical `Name` values can be created. `StateManager.CreateAsync` does not call `IStateRepository.FindByNameAsync` (no such method exists) or perform any duplicate check.

4. **No tests, no seed data** -- no `StatesDataSeedContributor.cs`, no AppService tests, no EF Core repository tests. Initial state population is left to runtime data entry.

5. **Constructor field coverage** -- `State(Guid id, string name)` covers the only non-audit settable field (`Name`). No code-gen artifact here; this is intentional and complete.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Docs: [docs/features/states/overview.md](/docs/features/states/overview.md) (deferred to T8 lookup-cluster work)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
