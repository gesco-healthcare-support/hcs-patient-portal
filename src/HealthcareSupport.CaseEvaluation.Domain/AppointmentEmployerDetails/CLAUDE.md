# AppointmentEmployerDetails -- employer info captured at booking time

One record per appointment (1:1). Entered by whoever books (patient, attorney, claim
examiner). Contains employer name, occupation, and optional contact/address fields.
Whether the employer is a notification recipient depends on carrier/self-insured status --
see Business Rules.

## What lives here

| Layer | Key file |
|---|---|
| Domain.Shared | `AppointmentEmployerDetailConsts.cs` -- max lengths (EmployerName=255, Occupation=255, PhoneNumber=12, Street/City=255, ZipCode=10) |
| Domain | `AppointmentEmployerDetail.cs` -- `FullAuditedAggregateRoot<Guid>`, `IMultiTenant`; FKs: `AppointmentId` (required), `StateId?` |
| Domain | `AppointmentEmployerDetailManager.cs` -- `CreateAsync`/`UpdateAsync` with `Check.Length` on every string field |
| Domain | `IAppointmentEmployerDetailRepository.cs` -- `GetWithNavigationPropertiesAsync`, `GetListWithNavigationPropertiesAsync` |
| Application | `AppointmentEmployerDetailsAppService.cs` -- 8 methods; mixed auth (see Gotchas) |
| HttpApi | `AppointmentEmployerDetailController.cs` -- `api/app/appointment-employer-details` |

`AppointmentEmployerDetailWithNavigationProperties` bundles the entity + `Appointment` + `State`
for list/detail reads.

## Conventions

- **Triple-layer length enforcement.** `Check.Length` in the manager, `[StringLength]` on DTOs,
  `HasMaxLength` in the DbContext. This is intentional ABP Suite scaffold -- do not drop any layer.
- **Concurrency stamping.** `UpdateAsync` calls `SetConcurrencyStampIfNotNull` -- last-write-wins
  is rejected when stamps disagree.
- **No state machine.** Lifecycle is governed by the parent Appointment's submit lock, not an enum
  on this entity.
- **Constructor covers 4 fields.** `PhoneNumber`, `Street`, `City`, `ZipCode` are set
  post-construction by the Manager. Standard ABP Suite pattern.

## Permissions

Four constants (`Default`, `Create`, `Edit`, `Delete`) in `CaseEvaluationPermissions.cs`
(lines 102+). In practice: `GetListAsync` uses bare `[Authorize]`, reads use `.Default`,
Delete uses `.Delete`. Create and Update are mixed -- see Gotchas.

## Business Rules

- `EmployerName` and `Occupation` are `[Required]`; all other fields are optional.
- `AppointmentId == Guid.Empty` throws `UserFriendlyException` on Create/Update.
- Self-insured or legally-party employers receive all-parties notifications; employer data
  is stored-only when a carrier/TPA handles the claim end-to-end. The entity currently has
  no Email column or notify-employer flag, so this branching is not yet implementable.

## Gotchas

1. **Submit-lock not enforced here.** Intent: data locks at submit (same as all booking-form
   fields). Code: `UpdateAsync` accepts any `[Authorize]` caller. The gate must be enforced
   upstream (booking-flow controller / submit-state check) -- it does not exist yet.
2. **No Email field.** Employer notification requires an Email column that does not exist.
   Employers cannot be emailed from this entity's current schema.
3. **Mixed auth on Create/Update.** Both use bare `[Authorize]` instead of the typed
   `.Create`/`.Edit` permissions. Only `DeleteAsync` uses its specific permission.
   Swap the attribute when a Create/Edit gate is wanted.
4. **Mapper class name typo.** `AppointmentEmployerDetailToAppointmentEmployerDetailDtoMappers`
   has a trailing `s` -- unique among feature mappers. Cosmetic only.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
