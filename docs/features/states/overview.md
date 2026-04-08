<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/States/CLAUDE.md on 2026-04-07 -->

# States

Host-scoped lookup table for US states. Referenced by Location, WcabOffice, Patient, ApplicantAttorney, and AppointmentEmployerDetail (5 inbound FKs, all with SetNull). Simplest lookup entity -- just a Name field. Has Angular UI under the Configurations menu.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/States/StateConsts.cs` | Default sort -- no max length constants defined |
| Domain | `src/.../Domain/States/State.cs` | Aggregate root -- `FullAuditedAggregateRoot<Guid>`, no `IMultiTenant` |
| Domain | `src/.../Domain/States/StateManager.cs` | DomainService -- `CreateAsync(name)`, `UpdateAsync(id, name, concurrencyStamp)` |
| Domain | `src/.../Domain/States/IStateRepository.cs` | Custom repo interface -- `GetListAsync` with name filter, `GetCountAsync` |
| Contracts | `src/.../Application.Contracts/States/StateCreateDto.cs` | Create input -- Name [Required] |
| Contracts | `src/.../Application.Contracts/States/StateUpdateDto.cs` | Update input -- Name [Required] + `IHasConcurrencyStamp` |
| Contracts | `src/.../Application.Contracts/States/StateDto.cs` | Output DTO -- `FullAuditedEntityDto<Guid>`, `IHasConcurrencyStamp` |
| Contracts | `src/.../Application.Contracts/States/GetStatesInput.cs` | Filter input -- `PagedAndSortedResultRequestDto` with Name filter |
| Contracts | `src/.../Application.Contracts/States/IStatesAppService.cs` | Service interface -- 5 methods (GetList, Get, Create, Update, Delete) |
| Application | `src/.../Application/States/StatesAppService.cs` | CRUD AppService -- delegates to StateManager, uses `ObjectMapper.Map` |
| EF Core | `src/.../EntityFrameworkCore/States/EfCoreStateRepository.cs` | Repository implementation with name filtering |
| HttpApi | `src/.../HttpApi/Controllers/States/StateController.cs` | Manual controller -- 5 endpoints at `api/app/states` |

## Entity Shape

```
State : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant -- host-scoped)
+-- Name : string [required, no max length constraint in StateConsts]
```

Constructor: `State(Guid id, string name)` -- validates `Check.NotNull(name)`.

## Relationships

**Outbound:** None -- State has no FK references to other entities.

**Inbound:** 5 entities reference State via `StateId` (see Inbound FKs section below).

## Multi-tenancy

**IMultiTenant: No.** Intentionally host-scoped -- US states are shared reference data across all tenants.

- DbContext config: inside `if (builder.IsHostDatabase())` guard in `CaseEvaluationDbContext.cs`
- Not configured in `CaseEvaluationTenantDbContext.cs` (host-only)
- Table: `AppStates` (prefixed with `CaseEvaluationConsts.DbTablePrefix`)

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source -> Destination | AfterMap? |
|---|---|---|
| `StateToStateDtoMappers` | `State` -> `StateDto` | No |
| `StateToLookupDtoGuidMapper` | `State` -> `LookupDto<Guid>` | Yes -- `destination.DisplayName = source.Name` |

**Note:** `StatesAppService` uses `ObjectMapper.Map<List<State>, List<StateDto>>` rather than directly invoking the Mapperly mapper. ABP's `ObjectMapper` delegates to the registered mapper under the hood.

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.States          (Default -- list/get + menu visibility)
CaseEvaluation.States.Create   (CreateAsync)
CaseEvaluation.States.Edit     (UpdateAsync)
CaseEvaluation.States.Delete   (DeleteAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children.
All CRUD permissions properly enforced on AppService methods via `[Authorize]`.

## Business Rules

Standard CRUD -- no validation, uniqueness, or computed fields.

- `StateManager.CreateAsync` validates `Check.NotNullOrWhiteSpace(name)` -- no uniqueness check
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

## Known Gotchas

1. **Most-referenced entity** -- 5 other entities have FK to State (all SetNull). Deleting a state sets those FKs to null rather than cascading or blocking.

2. **No NameMaxLength** -- unlike other lookup entities (Location, WcabOffice), `StateConsts` does not define a max length constant. The Name column in SQL Server has no explicit max length constraint in the EF config (`IsRequired()` only, no `HasMaxLength()`).

3. **No tests** -- no seed contributor, no AppService tests, no EF Core tests.

## Related Features

- [Locations](../locations/overview.md) -- `Location.StateId` references State
- [WCAB Offices](../wcab-offices/overview.md) -- `WcabOffice.StateId` references State
- [Patients](../patients/overview.md) -- `Patient.StateId` references State
- [Applicant Attorneys](../applicant-attorneys/overview.md) -- `ApplicantAttorney.StateId` references State
- [Appointment Employer Details](../appointment-employer-details/overview.md) -- `AppointmentEmployerDetail.StateId` references State

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/States/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
