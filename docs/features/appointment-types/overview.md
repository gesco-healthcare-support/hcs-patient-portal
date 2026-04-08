<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/CLAUDE.md on 2026-04-08 -->

# Appointment Types

Host-scoped lookup table for types of IME appointments (e.g., orthopedic, neurological). Referenced by `Appointment.AppointmentTypeId` and linked to doctors via the `DoctorAppointmentType` join table. Has Angular UI.

## Entity Shape

```
AppointmentType : FullAuditedEntity<Guid>     (NO IMultiTenant — host-scoped)
├── Name                  : string [max 100, required]
├── Description           : string? [max 200]
└── DoctorAppointmentTypes : ICollection<DoctorAppointmentType>  (M2M with Doctor)
```

## Relationships

**Inbound FKs:**
- `Appointment.AppointmentTypeId` -- NoAction
- `DoctorAvailability.AppointmentTypeId` -- SetNull
- `Location.AppointmentTypeId` -- SetNull
- `DoctorAppointmentType.AppointmentTypeId` -- Cascade

## Multi-tenancy

**IMultiTenant: No.** Host-scoped. DbContext config inside `IsHostDatabase()`.

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `DoctorAppointmentType.AppointmentTypeId` | Cascade | Yes (IsHostDatabase guard) | M2M join with Doctor |
| `Location.AppointmentTypeId` | SetNull | Yes (IsHostDatabase guard) | Optional default type for location |
| `DoctorAvailability.AppointmentTypeId` | SetNull | No | Optional type for availability slot |
| `Appointment.AppointmentTypeId` | NoAction | No | Required FK on appointment |

## Mapper Configuration

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `AppointmentTypeToAppointmentTypeDtoMappers` | Entity → DTO | No |
| `AppointmentTypeToAppointmentTypeExcelDtoMappers` | Entity → ExcelDto | No |
| `AppointmentTypeToLookupDtoGuidMapper` | Entity → LookupDto | Yes -- `DisplayName = source.Name` |

## Permissions

```
CaseEvaluation.AppointmentTypes          (Default)
CaseEvaluation.AppointmentTypes.Create
CaseEvaluation.AppointmentTypes.Edit
CaseEvaluation.AppointmentTypes.Delete
```

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| AppointmentTypeComponent | `angular/src/app/appointment-types/appointment-type/components/appointment-type.component.ts` | `/appointment-management/appointment-types` | List view with bulk delete |
| AbstractAppointmentTypeComponent | `angular/src/app/appointment-types/appointment-type/components/appointment-type.abstract.component.ts` | -- | Base directive with bulk selection |
| AppointmentTypeDetailModalComponent | `angular/src/app/appointment-types/appointment-type/components/appointment-type-detail.component.ts` | -- | Modal for create/edit |

**Pattern:** ABP Suite abstract/concrete with bulk operations (`bulkDelete()`, `selectAll()`, `selectedCount()`)

**Forms:**
- name: text (maxLength: 100, required)
- description: text (maxLength: 200, optional)

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.AppointmentTypes`)
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Create'"` -- create button
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Edit'"` -- edit action
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Delete'"` -- delete (single and bulk)

**Services injected:**
- `ListService`, `AppointmentTypeViewService`, `AppointmentTypeDetailViewService`, `PermissionService`, `AbpWindowService`

## Known Gotchas

1. **Has Description field** -- unlike most lookup entities which only have Name
2. **No tests**

## Related Features

- [Appointments](../appointments/overview.md) -- `Appointment.AppointmentTypeId` references this entity
- [Doctor Availabilities](../doctor-availabilities/overview.md) -- `DoctorAvailability.AppointmentTypeId` references this entity (SetNull)
- [Locations](../locations/overview.md) -- `Location.AppointmentTypeId` references this entity (SetNull)
- [Doctors](../doctors/overview.md) -- M2M via `DoctorAppointmentType` join table

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
