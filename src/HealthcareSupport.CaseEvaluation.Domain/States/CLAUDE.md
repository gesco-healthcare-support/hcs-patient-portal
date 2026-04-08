# States

Host-scoped lookup table for US states. Referenced by Location, WcabOffice, Patient, ApplicantAttorney, and AppointmentEmployerDetail (5 inbound FKs, all with SetNull). Simplest lookup entity — just a Name field. Has Angular UI under the Configurations menu.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/States/StateConsts.cs` | Default sort — no max length constants defined |
| Domain | `src/.../Domain/States/State.cs` | Aggregate root — `FullAuditedAggregateRoot<Guid>`, no `IMultiTenant` |
| Domain | `src/.../Domain/States/StateManager.cs` | DomainService — `CreateAsync(name)`, `UpdateAsync(id, name, concurrencyStamp)` |
| Domain | `src/.../Domain/States/IStateRepository.cs` | Custom repo interface — `GetListAsync` with name filter, `GetCountAsync` |
| Contracts | `src/.../Application.Contracts/States/StateCreateDto.cs` | Create input — Name [Required] |
| Contracts | `src/.../Application.Contracts/States/StateUpdateDto.cs` | Update input — Name [Required] + `IHasConcurrencyStamp` |
| Contracts | `src/.../Application.Contracts/States/StateDto.cs` | Output DTO — `FullAuditedEntityDto<Guid>`, `IHasConcurrencyStamp` |
| Contracts | `src/.../Application.Contracts/States/GetStatesInput.cs` | Filter input — `PagedAndSortedResultRequestDto` with Name filter |
| Contracts | `src/.../Application.Contracts/States/IStatesAppService.cs` | Service interface — 5 methods (GetList, Get, Create, Update, Delete) |
| Application | `src/.../Application/States/StatesAppService.cs` | CRUD AppService — delegates to StateManager, uses `ObjectMapper.Map` |
| EF Core | `src/.../EntityFrameworkCore/States/EfCoreStateRepository.cs` | Repository implementation with name filtering |
| HttpApi | `src/.../HttpApi/Controllers/States/StateController.cs` | Manual controller — 5 endpoints at `api/app/states` |
| Angular | `angular/src/app/states/state/components/state.component.ts` | List page — extends `AbstractStateComponent` |
| Angular | `angular/src/app/states/state/components/state.abstract.component.ts` | Base directive — CRUD wiring, permission checks |
| Angular | `angular/src/app/states/state/components/state-detail.component.ts` | Modal — single Name field |
| Angular | `angular/src/app/states/state/services/state.abstract.service.ts` | List data service |
| Angular | `angular/src/app/states/state/services/state-detail.abstract.service.ts` | Modal form service |
| Angular | `angular/src/app/states/state/providers/state-base.routes.ts` | Menu config — under `Configurations` parent, icon `fas fa-flag` |
| Angular | `angular/src/app/states/state/providers/state-route.provider.ts` | Registers routes via ABP RoutesService |
| Angular | `angular/src/app/states/state/state-routes.ts` | Route definition — `authGuard + permissionGuard` |
| Proxy | `angular/src/app/proxy/states/state.service.ts` | Auto-generated REST client |
| Proxy | `angular/src/app/proxy/states/models.ts` | Auto-generated TypeScript interfaces |

## Entity Shape

```
State : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant — host-scoped)
└── Name : string [required, no max length constraint in StateConsts]
```

Constructor: `State(Guid id, string name)` — validates `Check.NotNull(name)`.

## Relationships

**Outbound:** None — State has no FK references to other entities.

**Inbound:** 5 entities reference State via `StateId` (see Inbound FKs section below).

## Multi-tenancy

**IMultiTenant: No.** Intentionally host-scoped — US states are shared reference data across all tenants.

- DbContext config: inside `if (builder.IsHostDatabase())` guard in `CaseEvaluationDbContext.cs`
- Not configured in `CaseEvaluationTenantDbContext.cs` (host-only)
- Table: `AppStates` (prefixed with `CaseEvaluationConsts.DbTablePrefix`)

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `StateToStateDtoMappers` | `State` → `StateDto` | No |
| `StateToLookupDtoGuidMapper` | `State` → `LookupDto<Guid>` | Yes — `destination.DisplayName = source.Name` |

**Note:** `StatesAppService` uses `ObjectMapper.Map<List<State>, List<StateDto>>` rather than directly invoking the Mapperly mapper. ABP's `ObjectMapper` delegates to the registered mapper under the hood.

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.States          (Default — list/get + menu visibility)
CaseEvaluation.States.Create   (CreateAsync)
CaseEvaluation.States.Edit     (UpdateAsync)
CaseEvaluation.States.Delete   (DeleteAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children.
All CRUD permissions properly enforced on AppService methods via `[Authorize]`.

**Angular usage:**
- Route: `permissionGuard` requires `CaseEvaluation.States`
- Menu: `requiredPolicy: 'CaseEvaluation.States'` in base routes
- Actions: `.Edit` and `.Delete` checked via `PermissionService.getGrantedPolicy()`

## Business Rules

Standard CRUD — no validation, uniqueness, or computed fields.

- `StateManager.CreateAsync` validates `Check.NotNullOrWhiteSpace(name)` — no uniqueness check
- `StateManager.UpdateAsync` validates same, plus sets concurrency stamp
- No default values in `StateCreateDto` beyond `null!` initialization
- No numeric limits or computed fields

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
| StateComponent | `angular/src/app/states/state/components/state.component.ts` | `/configurations/states` | List view |
| AbstractStateComponent | `angular/src/app/states/state/components/state.abstract.component.ts` | — | Base directive with CRUD wiring |
| StateDetailModalComponent | `angular/src/app/states/state/components/state-detail.component.ts` | — | Modal for create/edit |

**Pattern:** ABP Suite abstract/concrete (`AbstractStateComponent` → `StateComponent`). Simplest UI — single Name field, no lookups, no bulk operations.

**Forms:**
- name: text input (required, no max length constraint in UI or backend consts)

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.States`)
- `CaseEvaluation.States.Create` — checked in abstract component for "New" button visibility
- `CaseEvaluation.States.Edit` — checked for edit action visibility
- `CaseEvaluation.States.Delete` — checked for delete action visibility

**Services injected:**
- `ListService`, `StateViewService`, `StateDetailViewService`, `PermissionService`

## Known Gotchas

1. **Most-referenced entity** — 5 other entities have FK to State (all SetNull). Deleting a state sets those FKs to null rather than cascading or blocking.

2. **No NameMaxLength** — unlike other lookup entities (Location, WcabOffice), `StateConsts` does not define a max length constant. The Name column in SQL Server has no explicit max length constraint in the EF config (`IsRequired()` only, no `HasMaxLength()`).

3. **No tests** — no seed contributor, no AppService tests, no EF Core tests.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Docs: [docs/features/states/overview.md](/docs/features/states/overview.md)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
