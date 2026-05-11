---
feature: internal-user-dashboard
old-source:
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\DashboardController.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\dashboard\
old-docs:
  - socal-project-overview.md (lines 247-255)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: ClinicStaff / StaffSupervisor / ITAdmin
depends-on:
  - external-user-appointment-request   # appointments are the data source for counters
  - clinic-staff-appointment-approval   # approval state drives "pending review" counter
required-by: []
---

# Internal user dashboard

## Purpose

Landing page for internal users (Clinic Staff / Staff Supervisor / IT Admin) showing 4-6 counters of pending work and 2-4 widgets for ease of operations. Each counter/widget drills down to the respective list page.

**Strict parity with OLD.**

## OLD behavior (binding)

### Counters (per spec line 251)

- Pending appointment requests (count of `AppointmentStatus = Pending`)
- Pending Joint Declaration Forms (AME appointments where JDF not yet uploaded)
- (Plus 2-4 more, TBD by reading OLD `DashboardController.cs` and `dashboard.component.ts`)

### Widgets (per spec line 253)

- Upcoming Appointments (next N days)
- Appointments with pending Declaration Form
- User count by type (counts of Patient / Adjuster / Applicant Attorney / Defense Attorney users)
- (Plus 1 more, TBD)

### Endpoint

- `POST /api/Dashboard/post` -- single endpoint that returns all dashboard data (per API matrix CSV line 142).
- Body likely contains role + user filter; response is a structured object with all counters + widgets.

### Critical OLD behaviors

- **Single round-trip** -- all dashboard data loaded in one POST call.
- **Drill-down navigation** -- clicking counter routes to corresponding list view filtered to the same scope.
- **Role-aware filtering** -- Clinic Staff sees their assigned appointments; Supervisor sees all.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Api/Controllers/Api/Core/DashboardController.cs` | Single POST endpoint |
| `PatientAppointment.Domain/...` | TO LOCATE -- likely `Core/DashboardDomain.cs` (not in glob result -- may be inlined in controller) |
| `patientappointment-portal/.../dashboard/dashboard.{ts,html,service}` | UI |

## NEW current state

- `Application.Contracts/Dashboards/DashboardCountersDto.cs` exists.
- `Application.Contracts/Dashboards/IDashboardAppService.cs` exists.
- Likely `IDashboardAppService.GetCountersAsync()` already partially implemented.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| `IDashboardAppService` exists | -- | YES | None | -- |
| Counters: pending appointments | -- | TO VERIFY | **Verify counter present + role-scoped** | I |
| Counters: pending JDF (AME) | OLD spec | TO VERIFY | **Add if missing** | I |
| Widgets: upcoming appointments | OLD spec | TO VERIFY | **Add if missing** | I |
| Widgets: pending declaration form | OLD spec | TO VERIFY | **Add if missing** | I |
| Widgets: user count by type | OLD spec | TO VERIFY | **Add if missing** | I |
| Single POST endpoint vs ABP standard GET | OLD: POST | ABP convention: GET | **Strict parity exception:** use GET in NEW (framework convention; functionally equivalent). Document as "framework deviation". | -- |
| Drill-down navigation from counters | OLD UI | -- | **Verify Angular dashboard component links to filtered list pages** | I |
| Role-scoping of counters | OLD: clinic staff sees own; supervisor sees all | TO VERIFY | **Apply `IAppointmentAccessPolicy`** in counter queries | B |

## Internal dependencies surfaced

- View All Appointments list -- drill-down target.

## Branding/theming touchpoints

- Dashboard UI (logo, primary color, counter card styling, widget layouts).

## Replication notes

### ABP wiring

- `IDashboardAppService.GetMyDashboardAsync()` -- single GET returns counters + widgets DTO.
- Counters computed via `IAppointmentRepository.CountAsync(...)` with role-scope filter.
- Widgets via separate query methods in same AppService.
- Cache for ~1 minute via ABP `IDistributedCache` to avoid repeated count queries on every page load.

### Things NOT to port

- POST shape for read-only data -- use GET.

### Verification

1. Internal user logs in -> dashboard renders with N counters
2. Click "Pending appointments" -> routes to list page filtered to Pending
3. Clinic Staff sees own assigned appointments only; Supervisor sees all
4. Counters refresh on dashboard reload (cache TTL respected)
