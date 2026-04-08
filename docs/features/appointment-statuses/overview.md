<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/CLAUDE.md on 2026-04-08 -->

# Appointment Statuses

Host-scoped lookup table for appointment status labels. **Note:** This is separate from the `AppointmentStatusType` enum (which defines the 13-state lifecycle). This entity stores display names for statuses and is not directly referenced by Appointment. Has Angular UI, bulk delete, and Excel export.

## Entity Shape

```
AppointmentStatus : FullAuditedEntity<Guid>     (NO IMultiTenant — host-scoped)
└── Name : string [max 100, required]
```

## Multi-tenancy

**IMultiTenant: No.** Host-scoped. DbContext config inside `IsHostDatabase()`.

## Permissions

```
CaseEvaluation.AppointmentStatuses          (Default)
CaseEvaluation.AppointmentStatuses.Create
CaseEvaluation.AppointmentStatuses.Edit
CaseEvaluation.AppointmentStatuses.Delete   (covers DeleteAsync, DeleteByIdsAsync, DeleteAllAsync)
```

## Mapper Configuration

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `AppointmentStatusToAppointmentStatusDtoMappers` | Entity → DTO | No |
| `AppointmentStatusToLookupDtoGuidMapper` | Entity → LookupDto | Yes -- `DisplayName = source.Name` |

## Business Rules

1. **Bulk delete** -- `DeleteByIdsAsync` and `DeleteAllAsync` available.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| AppointmentStatusComponent | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status.component.ts` | `/appointment-management/appointment-statuses` | List view |
| AbstractAppointmentStatusComponent | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status.abstract.component.ts` | -- | Base directive |
| AppointmentStatusDetailModalComponent | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status-detail.component.ts` | -- | Modal for create/edit |

**Pattern:** ABP Suite abstract/concrete (`AbstractAppointmentStatusComponent` → `AppointmentStatusComponent`)

**Forms:**
- name: text (maxLength: 100, required)

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.AppointmentStatuses`)
- `*abpPermission="'CaseEvaluation.AppointmentStatuses.Create'"` -- create button
- `*abpPermission="'CaseEvaluation.AppointmentStatuses.Edit'"` -- edit action
- `*abpPermission="'CaseEvaluation.AppointmentStatuses.Delete'"` -- delete action

**Services injected:**
- `ListService`, `AppointmentStatusViewService`, `AppointmentStatusDetailViewService`, `PermissionService`

## Known Gotchas

1. **Confusing naming** -- `AppointmentStatus` entity vs `AppointmentStatusType` enum. The entity is a lookup table; the enum is the actual state machine used on Appointment.
2. **No FK from Appointment** -- Appointment uses `AppointmentStatusType` enum, not this entity
3. **No tests**

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
