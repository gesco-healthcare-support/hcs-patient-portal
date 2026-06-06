# Reports -- Appointment Request Report (Group M)

Read-only, internal-only cross-appointment worklist replicating the legacy
Appointment Request Report (G-08-01), plus its PDF export (G-08-03). NOT a CRUD
entity: there is no `Report` table -- it is a masked projection over the existing
appointment query. See ADR-015.

## What lives here

| File | Purpose |
|---|---|
| `ReportsAppService.cs` | Runs the shared appointment query (filters + paging), validates, redacts every row, returns the page (`GetListAsync`); also renders the full filtered set to PDF (`GetReportPdfAsync`). `[RemoteService(false)]`, `[Authorize(Reports.Default)]`; the export overrides to `[Authorize(Reports.Export)]`. |
| `ReportFilterValidator.cs` | Pure: at-least-one-filter + both-or-neither/From<=To date guards, and the default sort (`RequestConfirmationNumber desc`). Unit-tested. |
| `ReportRowRedactor.cs` | Pure: `ReportRowSource` (raw, never leaves the layer) -> masked `AppointmentReportRowDto`. The single PHI seam shared by the grid and the PDF. Unit-tested. |
| `Pdf/AppointmentReportPdfDocument.cs` | The first QuestPDF render in the codebase: a landscape A4 table of the ten columns with a repeating green header. Formats already-masked rows -- it never masks. Smoke-tested. |

Masking helper `DobVisibility` (birth-year-only) lives in `Patients/` beside
`SsnVisibility` (last-4). DTOs + `Reports`/`Reports.Export` permissions live in
`Application.Contracts/Reports/` + `Permissions/`. Manual controller:
`HttpApi/Controllers/Reports/ReportController.cs` (`api/app/reports`,
`api/app/reports/export-pdf`).

## Key facts

- **No custom query.** Reuses `IAppointmentRepository.GetListWithNavigationPropertiesAsync`
  / `GetCountAsync`. Internal callers see every appointment in their tenant (no
  visibility narrowing) -- the Reports permission is internal-only by seeding.
- **PHI is masked server-side, in one seam.** SSN -> last 4; DOB -> birth year;
  name / email / phone shown in full (internal worklist). Full SSN is never
  emitted here -- only via the audited `Patients.RevealSsn` endpoint (ADR-009).
  The grid and the PDF feed through the same `ReportRowRedactor`, so masking
  cannot diverge.
- **PDF export = the full filtered set (no paging).** `GetReportPdfAsync` runs the
  same filters/guards as the grid, unpaged, redacts via the same seam, and renders
  with `AppointmentReportPdfDocument`. QuestPDF's Community license is set
  process-globally in `CaseEvaluationDomainModule`; the unit tests set it too.
- **Default sort must be passed explicitly.** The shared repository applies its own
  `CreationTime desc` default when `sorting` is blank, so the report resolves
  `Appointment.RequestConfirmationNumber desc` via `ReportFilterValidator` first.
- **Patient-name filter is folded into the quick search.** `FilterText` already
  spans patient first/last name (+ panel + confirmation number), so the legacy
  dedicated "Patient Name" advanced filter is not reproduced separately.
- **Angular:** `reports/appointment-report.component.*` -- quick search + type /
  location / status / date filters, paged ten-column grid, confirmation-number
  link, and an Export-to-PDF button. Route + nav gated on `CaseEvaluation.Reports`.

## Gotchas

- The grid stays empty until the user searches (legacy parity). The
  at-least-one-filter guard runs both client-side (immediate feedback) and
  server-side (`UserFriendlyException`); the export enforces the same guards.
- The AppService maps rows manually via `ReportRowSource` (NOT a Mapperly mapper
  or `ObjectMapper.Map`) so masking lives in exactly one place; the raw SSN/DOB
  never populate the wire DTO.
- The PDF download follows the packet split: the controller's interface member
  `GetReportPdfAsync` is `[NonAction]`; the routed `ExportPdfAsync` action returns
  `File(...)`. The Angular downloads via an authenticated `HttpClient` blob request
  + anchor click -- NEVER `window.open` (a new tab carries no Bearer token).
- Integration test seam: `EfCoreReportsAppServiceTests` (concrete, in
  `EntityFrameworkCore.Tests`) drives the abstract `ReportsAppServiceTests` in
  `Application.Tests` -- the established split for EF-backed AppService tests.
