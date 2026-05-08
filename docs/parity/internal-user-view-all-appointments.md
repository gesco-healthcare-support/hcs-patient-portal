---
feature: internal-user-view-all-appointments
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs (Get list)
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsController.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsSearchController.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\list\
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\search\
old-docs:
  - socal-project-overview.md (lines 439-453)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: ClinicStaff / StaffSupervisor / ITAdmin
depends-on: []
required-by: []
---

# Internal user -- View all appointments

## Purpose

Internal users (all 3 roles) view + filter all appointment requests in the system. The primary operational workflow page for staff.

**Strict parity with OLD.**

## OLD behavior (binding)

### List endpoint + pagination

`GET /api/Appointments/{orderbycolumn}/{sortorder}/{pageindex}/{rowcount}` -- paginated list via stored proc `spAppointmentRequestList`. Filters: status, date, search query.

### Search endpoint (advanced filters)

`POST /api/appointments/search` with filter body. Per spec line 441-453:

- `RequestConfirmationNumber` (exact match)
- `AppointmentTypeId`
- `LocationId`
- `AppointmentStatusId`
- Package Document Status
- Joint Declaration Receipt Status

### List columns

Likely (TO VERIFY in OLD UI):

- Confirmation Number
- Patient Name
- Appointment Date + Time
- Appointment Type
- Location
- Status
- Created By (user)
- Documents Status
- JDF Status (for AME)

### Critical OLD behaviors

- **Internal-user-only view of all data** -- external users get owner+accessor-scoped list (covered in view-appointment audit).
- **Paginated server-side** -- offset + page size.
- **Sortable columns.**
- **Multiple filters combined with AND.**
- **Drill-down to appointment view/edit** on row click.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs` Get(...)  | Stored-proc list |
| `PatientAppointment.Api/Controllers/.../AppointmentsSearchController.cs` | POST /search |
| `patientappointment-portal/.../appointments/list/...` + `search/...` | List + search UI |

## NEW current state

Per `Appointments/CLAUDE.md`:

- `AppointmentsAppService.GetListAsync` exists with `GetAppointmentsInput` -- 7 filter fields (FilterText, PanelNumber, AppointmentDateMin/Max, IdentityUserId, AccessorIdentityUserId, AppointmentTypeId, LocationId).
- Angular `appointment.component.ts` (concrete list) extends abstract pattern; ngx-datatable + advanced filters.
- KNOWN gap from CLAUDE.md: proxy sends 18 filter params, server defines only 7 -- proxy out of sync.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Filter: RequestConfirmationNumber | OLD | NEW: not in DTO | **Add `RequestConfirmationNumber string?` to `GetAppointmentsInput`** | I |
| Filter: AppointmentStatus | OLD | NEW: not in DTO | **Add `AppointmentStatus AppointmentStatusType?`** | I |
| Filter: Package Document Status | OLD | -- | **Add `PackageDocumentStatus? DocumentStatuses`** -- aggregated state per appointment | I |
| Filter: JDF Status | OLD | -- | **Add `JointDeclarationStatus? DocumentStatuses`** | I |
| Filter: PanelNumber | OLD | NEW has it | None | -- |
| Filter: AppointmentDateMin/Max | OLD | NEW has it | None | -- |
| Filter: AppointmentTypeId | OLD | NEW has it | None | -- |
| Filter: LocationId | OLD | NEW has it | None | -- |
| Filter: PatientId | -- | NEW proxy sends it | **Add to DTO** if needed for parity (not in OLD spec, but may be useful) | C |
| Filter: DueDate range | -- | NEW proxy sends it | **Add to DTO** if needed | C |
| Stale proxy | -- | NEW: proxy out of sync | **Regenerate proxy** after DTO updates | I |
| Internal-user-only access | OLD | TO VERIFY whether NEW gates by role | **Apply role check** -- internal users only see all; external get scoped (covered in view-appointment audit) | I |
| Sort + paginate | OLD | NEW: ABP standard | None | -- |
| Drill-down to view/edit | OLD | NEW: TO VERIFY | **Verify row click routes to /appointments/view/:id** | C |

## Internal dependencies surfaced

- DocumentStatus aggregate computation (compute "Pending / Partial / Complete" per appointment based on child docs).

## Branding/theming touchpoints

- List UI styling.

## Replication notes

### ABP wiring

- Existing `GetListAsync` -- extend `GetAppointmentsInput` with missing filters.
- Aggregate doc status via subquery in repository.
- Regenerate proxy after DTO change.

### Verification

1. Internal user logs in -> sees all appointments
2. Filter by confirmation # -> matching row shown
3. Filter by status = Pending -> only Pending shown
4. Combined filters work as AND
5. Sort by appointment date -> sorted correctly
6. Click row -> view page loads
