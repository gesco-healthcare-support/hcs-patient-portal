# Reports -- Appointment Request Report + demographics PDF (Group M)

Read-only, internal-only reporting for the OLD app's "Reports" area: the
Appointment Request Report grid (G-08-01) + its PDF export (G-08-03), and the
per-appointment Patient Demographics intake sheet PDF (G-08-04). NOT a CRUD
entity: there is no `Report` table -- the grid is a masked projection over the
existing appointment query, and the demographics PDF reuses the appointment read
path. See ADR-015.

## What lives here

| File | Purpose |
|---|---|
| `ReportsAppService.cs` | Report grid (`GetListAsync`, paged) + report-table PDF (`GetReportPdfAsync`, full filtered set). `[RemoteService(false)]`, `[Authorize(Reports.Default)]`; export overrides to `[Authorize(Reports.Export)]`. |
| `AppointmentDemographicsAppService.cs` | Per-appointment demographics PDF (`GetPdfAsync`). Reuses `IAppointmentsAppService.GetWithNavigationPropertiesAsync` (read guard + SSN mask); `[RemoteService(false)]`, `[Authorize(Reports.Default)]` (internal-only). |
| `ReportFilterValidator.cs` | Pure: at-least-one-filter + both-or-neither/From<=To date guards, and the default sort (`RequestConfirmationNumber desc`). Unit-tested. |
| `ReportRowRedactor.cs` | Pure: `ReportRowSource` (raw, never leaves the layer) -> masked `AppointmentReportRowDto`. The single PHI seam shared by the grid and the report PDF. Unit-tested. |
| `Pdf/AppointmentReportPdfDocument.cs` | QuestPDF landscape table of the ten report columns (repeating green header). Formats already-masked rows. |
| `Pdf/AppointmentDemographicsPdfDocument.cs` | QuestPDF per-appointment intake sheet (appointment / patient / employer / per-injury / attorney sections). Masks DOB to birth year; SSN arrives masked. |

Masking helpers `SsnVisibility` (last-4) + `DobVisibility` (birth-year-only) live
in `Patients/`. DTOs + `Reports`/`Reports.Export` permissions live in
`Application.Contracts/Reports/` + `Permissions/`. Manual controllers (HttpApi):
`ReportController` (`api/app/reports`, `.../export-pdf`) and
`AppointmentDemographicsController` (`api/app/appointment-demographics/{id}`).

## Key facts

- **No custom query.** The grid reuses `IAppointmentRepository.GetListWithNavigationPropertiesAsync`
  / `GetCountAsync`; the demographics PDF reuses `IAppointmentsAppService.GetWithNavigationPropertiesAsync`.
  Internal callers see every appointment in their tenant (the Reports permission is internal-only by seeding).
- **PHI is masked server-side, in shared seams.** SSN -> last 4; DOB -> birth year;
  name / email / phone shown in full (internal worklist). Full SSN is never emitted
  here -- only via the audited `Patients.RevealSsn` endpoint (ADR-009). The grid +
  report PDF share `ReportRowRedactor`; the demographics PDF inherits the
  appointment service's SSN mask and applies `DobVisibility` in the render.
- **All PDFs render in-process via QuestPDF** (license set process-globally in
  `CaseEvaluationDomainModule`; package referenced in the Application project). The
  report export runs the full filtered set unpaged; the demographics PDF is per-appointment.
- **Default report sort must be passed explicitly** -- the shared repository
  otherwise applies its own `CreationTime desc`. `ReportFilterValidator` resolves
  `Appointment.RequestConfirmationNumber desc`.
- **Patient-name filter is folded into the quick search** (`FilterText` already spans patient name).
- **Angular:** `reports/appointment-report.component.*` (grid + Export-to-PDF); the
  demographics download is an internal-only button on `appointments/.../appointment-view`,
  gated by `*abpPermission="'CaseEvaluation.Reports'"`. Both downloads use an
  authenticated `HttpClient` blob request + anchor click -- NEVER `window.open`.

## Gotchas

- The grid stays empty until the user searches (legacy parity); the
  at-least-one-filter guard runs client-side AND server-side (export included).
- The AppService maps report rows manually via `ReportRowSource` (NOT a Mapperly
  mapper or `ObjectMapper.Map`) so masking lives in one place.
- File downloads follow the packet split: the controller's interface member is
  `[NonAction]` (report PDF) or the controller is a plain `AbpController`
  (demographics); the routed action returns `File(...)`. The Angular uses a raw
  authenticated blob request, not the generated proxy method.
- **Demographics PDF deferrals (PF-004):** the Appointment Accessors section
  (read DTO has rights + a user id, not the invitee name/email) and the Custom
  Form section (no custom-field values on the read graph) are omitted, pending a
  read-DTO extension.
- Integration test seam: `EfCoreReportsAppServiceTests` (concrete, in
  `EntityFrameworkCore.Tests`) drives the abstract `ReportsAppServiceTests` in
  `Application.Tests` -- the established split for EF-backed AppService tests.
