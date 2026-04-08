# AppointmentAccessors

Grants specific users (typically attorneys) View or Edit access to individual appointments. Used for attorney-scoped filtering in the appointment list — the `EfCoreAppointmentRepository` queries the `AppointmentAccessor` table to determine which appointments an external user can see. No standalone UI; managed programmatically during the appointment booking flow.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/AppointmentAccessors/AppointmentAccessorConsts.cs` | Default sort |
| Domain.Shared | `src/.../Domain.Shared/Enums/AccessType.cs` | Enum: View(23), Edit(24) |
| Domain | `src/.../Domain/AppointmentAccessors/AppointmentAccessor.cs` | Entity (FullAuditedEntity, NOT AggregateRoot) — IMultiTenant |
| Domain | `src/.../Domain/AppointmentAccessors/AppointmentAccessorManager.cs` | DomainService — create/update |
| Domain | `src/.../Domain/AppointmentAccessors/IAppointmentAccessorRepository.cs` | Custom repo interface |
| Contracts | `src/.../Application.Contracts/AppointmentAccessors/` | DTOs, filter input, service interface |
| Application | `src/.../Application/AppointmentAccessors/AppointmentAccessorsAppService.cs` | CRUD — only `[Authorize]`, no feature-specific permissions on methods |
| HttpApi | `src/.../HttpApi/Controllers/AppointmentAccessors/AppointmentAccessorController.cs` | 8 endpoints at `api/app/appointment-accessors` |

## Entity Shape

```
AppointmentAccessor : FullAuditedEntity<Guid>, IMultiTenant
├── TenantId       : Guid?       (tenant isolation)
├── AccessTypeId   : AccessType  (View=23, Edit=24)
├── IdentityUserId : Guid        (FK → IdentityUser, required — the user granted access)
└── AppointmentId  : Guid        (FK → Appointment, required — the appointment being shared)
```

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `IdentityUserId` | IdentityUser | NoAction | Required |
| `AppointmentId` | Appointment | NoAction | Required |

## Multi-tenancy

**IMultiTenant: Yes.** DbContext config outside `IsHostDatabase()` — both contexts.

## Permissions

```
CaseEvaluation.AppointmentAccessors          (Default)
CaseEvaluation.AppointmentAccessors.Create
CaseEvaluation.AppointmentAccessors.Edit
CaseEvaluation.AppointmentAccessors.Delete
```

**Gotcha:** Permissions are defined but NOT used on AppService methods — all methods only require generic `[Authorize]`.

## Business Rules

Standard CRUD — no special business rules.

## Angular UI Surface

No Angular UI — this entity is managed via API only during the appointment booking flow.

## Known Gotchas

1. **FullAuditedEntity, not AggregateRoot** — unlike most entities in this project
2. **Permissions defined but not enforced** — AppService uses generic `[Authorize]` instead of feature-specific permissions
3. **No Angular UI** — managed programmatically via API only
4. **No tests**

## Mapper Configuration

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `AppointmentAccessorToAppointmentAccessorDtoMappers` | Entity → DTO | No |
| `AppointmentAccessorWithNavProps...DtoMapper` | NavProps → NavPropsDto | No |

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
