<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentAccessors/CLAUDE.md on 2026-04-08 -->

# Appointment Accessors

Grants specific users (typically attorneys) View or Edit access to individual appointments. Used for attorney-scoped filtering in the appointment list -- the `EfCoreAppointmentRepository` queries the `AppointmentAccessor` table to determine which appointments an external user can see. No standalone UI; managed programmatically during the appointment booking flow.

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

**IMultiTenant: Yes.** DbContext config outside `IsHostDatabase()` -- both contexts.

## Permissions

```
CaseEvaluation.AppointmentAccessors          (Default)
CaseEvaluation.AppointmentAccessors.Create
CaseEvaluation.AppointmentAccessors.Edit
CaseEvaluation.AppointmentAccessors.Delete
```

**Note:** Permissions are defined but NOT used on AppService methods -- all methods only require generic `[Authorize]`.

## Mapper Configuration

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `AppointmentAccessorToAppointmentAccessorDtoMappers` | Entity → DTO | No |
| `AppointmentAccessorWithNavProps...DtoMapper` | NavProps → NavPropsDto | No |

## Known Gotchas

1. **FullAuditedEntity, not AggregateRoot** -- unlike most entities in this project
2. **Permissions defined but not enforced** -- AppService uses generic `[Authorize]` instead of feature-specific permissions
3. **No Angular UI** -- managed programmatically via API only
4. **No tests**

## Related Features

- [Appointments](../appointments/overview.md) -- `AppointmentId` FK references Appointment

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentAccessors/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
