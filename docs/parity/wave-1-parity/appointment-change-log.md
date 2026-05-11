---
feature: appointment-change-log
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentChangeLogModule\AppointmentChangeLogDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentChangeLog\AppointmentChangeLogsController.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentChangeLog\AppointmentChangeLogsSearchController.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-change-log\
old-docs:
  - socal-project-overview.md (lines 455-469)
  - data-dictionary-table.md (AppointmentChangeLogs)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: ClinicStaff / StaffSupervisor / ITAdmin (any internal user views; system writes)
depends-on: []
required-by:
  - external-user-appointment-request           # writes change log on every appointment field change
  - clinic-staff-appointment-approval           # writes on status changes
  - external-user-appointment-cancellation      # writes on status + reason changes
  - external-user-appointment-rescheduling      # writes on status + slot changes
---

# Appointment change log

## Purpose

Per-field audit trail for every change made to an Appointment after creation. Records: what field changed, who, when, old value, new value. Internal users can view + filter the log. Used for compliance, dispute resolution, and operational visibility.

**Strict parity with OLD.**

## OLD behavior (binding)

### Schema (`AppointmentChangeLogs` table)

- `AppointmentChangeLogId` PK
- `AppointmentId` FK
- `FieldName` (varchar 50) -- the property that changed
- `OldValue` (varchar 100, nullable)
- `NewValue` (varchar 100, nullable)
- `ChangedDate` (datetime)
- `ChangedById` (int, nullable)
- `TableName` (varchar 50, nullable) -- the entity the field belongs to
- `IsMailSent` (bit, nullable) -- did this change trigger an email?
- `IsInternalUserUpdate` (bit, nullable) -- was the change made by an internal user?

### Write path (`AppointmentChangeLogDomain.ChangeLogs`)

Per `AppointmentChangeLogDomain.cs` lines 90-100:

- Reflection-based diff: iterates `EntityEntry.Properties` from EF ChangeTracker.
- For each property where `oldValue != newValue`: insert one `AppointmentChangeLog` row.
- Called from AppointmentDomain on Update + from AppointmentChangeRequest accept paths.

### Read path

- `GET /api/AppointmentChangeLogs/{orderbycolumn}/{sortorder}/{pageindex}/{rowcount}` -- paginated via stored proc `spAppointmentChangeLogs`.
- `POST /api/appointmentchangelogs/search` -- advanced filter via search controller.
- Filter (per spec line 469): field name, date range, user.

### Critical OLD behaviors

- **Per-field granularity** -- one row per changed property, not per save.
- **Includes nested entities** (`TableName` field captures which child entity the field belongs to).
- **`IsMailSent` + `IsInternalUserUpdate` flags** for downstream filtering and notification dedup.
- **Internal users only** can view the log (no external user access).

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentChangeLogModule/AppointmentChangeLogDomain.cs` (418 lines) | Reflection diff + write + read |
| `PatientAppointment.Api/Controllers/.../AppointmentChangeLogsController.cs` | List + CRUD endpoints |
| `PatientAppointment.Api/Controllers/.../AppointmentChangeLogsSearchController.cs` | POST /search filter endpoint |
| `patientappointment-portal/.../appointment-change-log/...` | Read-only list UI |

## NEW current state

- ABP provides automatic auditing via `[Audited]` attribute + `EntityHistory` -- captures property-level changes automatically.
- Per `Appointments/CLAUDE.md`, `Appointment` entity has `[Audited]` already.
- ABP's audit logs live in `AbpAuditLogs` + `AbpEntityChanges` + `AbpEntityPropertyChanges` -- ABP's standard audit infrastructure.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Per-field change capture | OLD reflection diff | NEW ABP auto-audit covers it | None -- ABP equivalent | -- |
| Field-name + table-name visibility | OLD captures both | ABP `EntityPropertyChange.PropertyName` + `EntityChange.EntityTypeFullName` | Map to OLD shape in DTO when surfacing to UI | I |
| Old value + new value | OLD captures | ABP captures `OriginalValue` + `NewValue` | None | -- |
| ChangedById | OLD field | ABP `AuditLog.UserId` | None -- equivalent | -- |
| IsMailSent flag | OLD: derived/post-write | -- | **Strict parity:** add as a domain-event-set flag on the change log entry, not in ABP audit. Decision: skip the flag in NEW since ABP audit + IDistributedEventBus tracking is sufficient and cleaner. **Document as strict-parity exception: replaced by event subscription pattern.** | I |
| IsInternalUserUpdate flag | OLD field | -- | **Strict parity:** ABP audit captures user; can derive role at read time. Skip in NEW. | I |
| List endpoint with filters | OLD: stored proc + search controller | -- | **Add `IAppointmentChangeLogsAppService.GetListAsync(GetChangeLogsInput { AppointmentId, FieldName, DateMin, DateMax, ChangedById })`** -- queries ABP audit tables filtered to Appointment + child entities | B |
| Per-feature visibility | Internal users only | -- | **`[Authorize(...)]` with internal-role check** | B |
| Display UI | OLD: list with filters | -- | **Add Angular component** (read-only list + filter form) | I |

## Internal dependencies surfaced

- ABP audit infrastructure (no new dependencies; built-in).

## Branding/theming touchpoints

- List page UI (logo, primary color, status indicators).

## Replication notes

### ABP wiring

- Use ABP's built-in audit logs. Add `[Audited]` to `Appointment` and all child entities.
- AppService reads `IAuditLogRepository` filtered to entity types matching Appointment + children.
- DTO maps ABP audit shape to OLD-style `AppointmentChangeLogDto` for UI consistency.

### Things NOT to port

- Reflection diff code -- ABP already does it.
- Stored proc -- LINQ-to-EF on ABP audit tables.
- `IsMailSent` / `IsInternalUserUpdate` flags -- replaced by event-subscription pattern + role derivation.

### Verification (manual test plan)

1. Update an Appointment field -> change log row created automatically
2. View change log for an appointment -> see all field-level changes
3. Filter by field name, date range, user -> works
4. External user tries to view -> 403
