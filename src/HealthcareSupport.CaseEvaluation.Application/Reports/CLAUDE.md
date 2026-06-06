# Reports -- Appointment Request Report (Group M)

Read-only, internal-only cross-appointment worklist replicating the legacy
Appointment Request Report (G-08-01). NOT a CRUD entity: there is no `Report`
table -- it is a masked projection over the existing appointment query. See
ADR-015.

## What lives here

| File | Purpose |
|---|---|
| `ReportsAppService.cs` | Runs the shared appointment query (filters + paging), validates, redacts every row, returns the page. `[RemoteService(false)]`, `[Authorize(Reports.Default)]`. |
| `ReportFilterValidator.cs` | Pure: at-least-one-filter + both-or-neither/From<=To date guards, and the default sort (`RequestConfirmationNumber desc`). Unit-tested. |
| `ReportRowRedactor.cs` | Pure: `ReportRowSource` (raw, never leaves the layer) -> masked `AppointmentReportRowDto`. The single PHI seam shared with the PDF export. Unit-tested. |

Masking helper `DobVisibility` (birth-year-only) lives in `Patients/` beside
`SsnVisibility` (last-4). DTOs + `Reports` permission live in
`Application.Contracts/Reports/` + `Permissions/`. Manual controller:
`HttpApi/Controllers/Reports/ReportController.cs` (`api/app/reports`).

## Key facts

- **No custom query.** Reuses `IAppointmentRepository.GetListWithNavigationPropertiesAsync`
  / `GetCountAsync`. Internal callers see every appointment in their tenant (no
  visibility narrowing) -- the Reports permission is internal-only by seeding.
- **PHI is masked server-side, in one seam.** SSN -> last 4; DOB -> birth year;
  name / email / phone shown in full (internal worklist). Full SSN is never
  emitted here -- only via the audited `Patients.RevealSsn` endpoint (ADR-009).
  The same redactor feeds the report-table PDF, so masking cannot diverge.
- **Default sort must be passed explicitly.** The shared repository applies its
  own `CreationTime desc` default when `sorting` is blank, so the report resolves
  `Appointment.RequestConfirmationNumber desc` via `ReportFilterValidator` before
  calling the repo.
- **Patient-name filter is folded into the quick search.** `FilterText` already
  spans patient first/last name (+ panel + confirmation number), so the legacy
  dedicated "Patient Name" advanced filter is not reproduced separately.
- **Angular:** `reports/appointment-report.component.*` -- quick search + type /
  location / status / date filters, paged ten-column grid, confirmation-number
  link to the appointment view. Route + nav entry gated on `CaseEvaluation.Reports`.

## Gotchas

- The grid stays empty until the user searches (legacy parity). The
  at-least-one-filter guard runs both client-side (immediate feedback) and
  server-side (`UserFriendlyException`).
- The AppService maps rows manually via `ReportRowSource` (NOT a Mapperly mapper
  or `ObjectMapper.Map`) so masking lives in exactly one place; the raw SSN/DOB
  never populate the wire DTO.
- Integration test seam: `EfCoreReportsAppServiceTests` (concrete, in
  `EntityFrameworkCore.Tests`) drives the abstract `ReportsAppServiceTests` in
  `Application.Tests` -- the established split for EF-backed AppService tests.
