# AppointmentStatuses

Lookup table of appointment status display labels, managed via Angular admin UI under Appointment Management. **Important:** this entity is NOT the source of truth for appointment lifecycle state -- the live state machine is the `AppointmentStatusType` enum stored directly on `Appointment.AppointmentStatus`. The two are disconnected.

<!-- TODO: product-intent input needed (deferred to T8 lookup-cluster interview) -->

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentStatuses/AppointmentStatusConsts.cs` | NameMaxLength=100, default sort helper |
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs` | 13-state enum (Pending through CancellationRequested) -- separate from this entity |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/AppointmentStatus.cs` | Entity (FullAuditedEntity<Guid>) -- no IMultiTenant marker |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/AppointmentStatusManager.cs` | DomainService -- thin Create/Update with name validation |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/IAppointmentStatusRepository.cs` | Repo with DeleteAllAsync, GetListAsync, GetCountAsync (filter by name) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentStatuses/AppointmentStatusDto.cs` | DTO (FullAuditedEntityDto<Guid>) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentStatuses/AppointmentStatusCreateDto.cs` | Create DTO (Name required, max 100) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentStatuses/AppointmentStatusUpdateDto.cs` | Update DTO (Name required, max 100) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentStatuses/GetAppointmentStatusesInput.cs` | Paged/sorted input with FilterText |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentStatuses/IAppointmentStatusesAppService.cs` | AppService interface (7 methods) |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/AppointmentStatuses/AppointmentStatusesAppService.cs` | CRUD + DeleteByIds + DeleteAll, `[RemoteService(IsEnabled=false)]` |
| EntityFrameworkCore | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/AppointmentStatuses/EfCoreAppointmentStatusRepository.cs` | EF repo with Name-contains filter |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentStatuses/AppointmentStatusController.cs` | 7 endpoints at `api/app/appointment-statuses` |
| Angular | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status.component.ts` | Concrete list component (template + abstract base) |
| Angular | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status.abstract.component.ts` | Abstract base directive with list/CRUD wiring |
| Angular | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status-detail.component.ts` | Create/edit modal |
| Angular | `angular/src/app/appointment-statuses/appointment-status/services/appointment-status.service.ts` | List view service (concrete) |
| Angular | `angular/src/app/appointment-statuses/appointment-status/services/appointment-status.abstract.service.ts` | List view service (abstract base) |
| Angular | `angular/src/app/appointment-statuses/appointment-status/services/appointment-status-detail.service.ts` | Detail/modal service (concrete) |
| Angular | `angular/src/app/appointment-statuses/appointment-status/services/appointment-status-detail.abstract.service.ts` | Detail service (abstract base) |
| Angular | `angular/src/app/appointment-statuses/appointment-status/appointment-status-routes.ts` | Standalone Routes (authGuard, permissionGuard) |
| Angular | `angular/src/app/appointment-statuses/appointment-status/providers/appointment-status-base.routes.ts` | ABP menu registration (Appointment Management group) |
| Angular | `angular/src/app/appointment-statuses/appointment-status/providers/appointment-status-route.provider.ts` | Route provider |

## Entity Shape

```
AppointmentStatus : FullAuditedEntity<Guid>     (no IMultiTenant marker)
  Name : string  -- required, max 100
```

This entity has NO state field of its own. The lifecycle state lives on
`Appointment.AppointmentStatus` as the `AppointmentStatusType` enum:

```
AppointmentStatusType (no enforced transitions):
  Pending(1) -> Approved(2) -> CheckedIn(9) -> CheckedOut(10) -> Billed(11)
  Pending(1) -> Rejected(3) | NoShow(4)
  Pending(1) -> CancelledNoBill(5) | CancelledLate(6)
  Pending(1) -> RescheduledNoBill(7) | RescheduledLate(8)
  Pending(1) -> RescheduleRequested(12) | CancellationRequested(13)
```

Transitions are NOT enforced anywhere; any value can move to any other value.
See FEAT-01 (open) for the missing state-machine work.

## Relationships

None. The `AppointmentStatus` entity has no FKs out, and the live DbContext does
NOT include an FK from `Appointment` back to it (the migration history shows an
FK was briefly introduced in 2026-02 then dropped when `Appointment.AppointmentStatus`
was switched to the enum column). See DAT-05 for the disconnect.

## Multi-tenancy

**IMultiTenant: No** -- the entity class does not implement `IMultiTenant`.

DbContext registration (verified directly):
- `CaseEvaluationDbContext.cs` line 120 -- inside `if (builder.IsHostDatabase())` (host context)
- `CaseEvaluationTenantDbContext.cs` line 58 -- unconditional (tenant context)

This means the entity is mapped in BOTH host and tenant databases, even though
it carries no tenant marker. Deviation from the host-only lookup pattern used
by AppointmentTypes.

## Mapper Configuration

Riok.Mapperly partial classes in `CaseEvaluationApplicationMappers.cs` (verified):

| Mapper Class | Source -> Destination | AfterMap? |
|---|---|---|
| `AppointmentStatusToAppointmentStatusDtoMappers` | `AppointmentStatus` -> `AppointmentStatusDto` | No AfterMap |
| `AppointmentStatusToLookupDtoGuidMapper` | `AppointmentStatus` -> `LookupDto<Guid>` | Yes -- `destination.DisplayName = source.Name` |

## Permissions

From `CaseEvaluationPermissions.cs`:

```
CaseEvaluation.AppointmentStatuses          (Default)
CaseEvaluation.AppointmentStatuses.Create
CaseEvaluation.AppointmentStatuses.Edit
CaseEvaluation.AppointmentStatuses.Delete
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` lines 27-30 with
localization keys `Permission:AppointmentStatuses` / `Permission:Create` / `Permission:Edit`
/ `Permission:Delete`.

AppService enforcement (`AppointmentStatusesAppService.cs`):
- Class-level `[Authorize(...AppointmentStatuses.Default)]`
- `CreateAsync` -> `.Create`
- `UpdateAsync` -> `.Edit`
- `DeleteAsync`, `DeleteByIdsAsync`, `DeleteAllAsync` -> `.Delete`
- `GetAsync` and `GetListAsync` rely on the class-level Default permission

## Business Rules

Standard CRUD over a single string field. No uniqueness check, no computed
defaults, no frozen fields. The only validation is:
- `Name` is required and capped at 100 chars (enforced by `Check.NotNullOrWhiteSpace`
  + `Check.Length` in the Manager, by `[Required]` + `[StringLength(100)]` on the
  DTOs, and by `HasMaxLength(100)` in EF config -- triple-enforced).
- `DeleteAllAsync` deletes everything that matches the current `FilterText` (or
  the entire table if no filter is supplied). No safeguard against accidental
  full-table wipe beyond the `.Delete` permission.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| `AppointmentStatusComponent` | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status.component.ts` | `/appointment-management/appointment-statuses` | Concrete list view (extends abstract) |
| `AbstractAppointmentStatusComponent` | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status.abstract.component.ts` | -- | Base directive (list, create, update, delete) |
| `AppointmentStatusDetailModalComponent` | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status-detail.component.ts` | -- | Create/edit modal |

**Pattern:** ABP Suite scaffold (`AbstractXxxComponent` -> `XxxComponent`). The
concrete component is decorated `@Component` (no `standalone: true` flag set
explicitly, but `imports:` array is populated -- ABP Suite hybrid output).

**Forms:**
- `name`: text input, `maxLength=100`, required (enforced by template + DTO)

**Permission guards:**
- Route (`appointment-status-routes.ts`): `authGuard`, `permissionGuard`
- Menu (`appointment-status-base.routes.ts`): `requiredPolicy: 'CaseEvaluation.AppointmentStatuses'`
- Template directives in `appointment-status.component.html`:
  - `*abpPermission="'CaseEvaluation.AppointmentStatuses.Create'"` -- new button
  - `*abpPermission="'CaseEvaluation.AppointmentStatuses.Edit'"` -- row edit
  - `*abpPermission="'CaseEvaluation.AppointmentStatuses.Delete'"` -- row delete

**Services injected (abstract component):**
- `ListService`, `AppointmentStatusViewService`, `AppointmentStatusDetailViewService`,
  `PermissionService`

## Known Gotchas

1. **Entity is not the appointment state machine.** The live state for an
   appointment is the `AppointmentStatusType` enum stored directly on
   `Appointment.AppointmentStatus`. This `AppointmentStatus` entity is a
   separate, free-text lookup table that nothing in the appointment booking
   flow currently reads. Editing rows here has no effect on appointment state.
   See **DAT-05** for the disconnect.
2. **No state-machine enforcement on `AppointmentStatusType`.** Any of the 13
   enum values can transition to any other value -- there is no domain
   validation. Tracked as **FEAT-01** (open).
3. **DbContext registration is inconsistent.** The entity is configured in the
   host `CaseEvaluationDbContext` inside an `IsHostDatabase()` guard AND
   unconditionally in `CaseEvaluationTenantDbContext`. Other appointment
   lookups (e.g. `AppointmentType`) follow the same pattern, but the entity
   does not implement `IMultiTenant`, so per-tenant rows behave like host data
   without the marker. Confirm intended scope during T8.
4. **`DeleteAllAsync` has no full-wipe guard.** A caller with `.Delete` and an
   empty `FilterText` deletes every row in the table.
5. **No tests.** No unit, integration, or AppService tests exist for this entity.
6. **No `docs/product/` coverage.** Product-intent documentation deferred to T8
   lookup-cluster interview.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Open issue: FEAT-01 (state-machine enforcement)
- Data gap: DAT-05 (lookup table vs enum disconnect)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
