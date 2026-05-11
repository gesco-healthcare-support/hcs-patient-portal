---
feature: internal-user-reports
old-source:
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentRequestReportController.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentRequestReportSearchController.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-request-report\
old-docs:
  - socal-project-overview.md (lines 595-613)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: StaffSupervisor / ITAdmin (per overview permission matrix)
depends-on:
  - external-user-appointment-request
required-by: []
---

# Internal user -- Reports

## Purpose

Internal users (Supervisor + IT Admin) generate three reports:

1. **Schedule Report** -- merged data of all doctors' websites (multi-tenant aggregate via the read-only replicated DB; Phase 2)
2. **Appointment Request Report** -- detailed per-appointment report with all intake form fields
3. **Excel ODBC Link** -- 3-4 views exposed via ODBC for ad-hoc Excel reporting

All reports are HTML-tabular, exportable to Excel + PDF.

**Strict parity with OLD.** Schedule Report is Phase 2 (multi-tenant); Appointment Request Report + ODBC are Phase 1.

## OLD behavior (binding)

### Schedule Report (Phase 2)

Per spec line 599: shows merged data from all 3 doctor websites via replication of "selective, non-sensitive appointment booking records" into a separate read-only DB. Filters: Date Range, Location, Appointment Type, Status, Patient Name, Doctor Name. Defer to Phase 2 multi-tenancy.

### Appointment Request Report (Phase 1)

Per spec line 603-607:

- Filters: Appointment Date Range, Location, Appointment Type, Status, Patient Name, Doctor Name.
- Includes ALL fields of patient intake form.
- Tabular HTML with export to Excel + PDF.

### Excel ODBC Link (Phase 1)

Per spec line 609-611: exposes 3-4 views via ODBC for ad-hoc Excel external-data-source reporting. SQL views queryable from Excel.

### CSV Export

Per API matrix (CSVExport controller):

- `GET /api/CsvExport/{key}` -- retrieve previously generated CSV by export key
- `GET /api/CsvExport/{appointmentId}/{downloadType}` -- generate CSV for specific appointment + type
- `POST /api/CsvExport` -- initiate new CSV export based on filter body

Export is async: POST starts export, returns key; GET retrieves by key.

### Note: AppointmentRequestReport "Not in Use"

Per OLD `socal-api-documentation.md` (line 209): the controller is annotated `"AppointmentRequestReport -> Not in Use"`. May be deprecated in OLD. **Strict parity:** since OLD marks this Not In Use, we don't port the unused endpoint -- but we DO port the report functionality via the search-based generation pattern.

### Critical OLD behaviors

- **HTML reports, no Crystal Reports** -- per spec line 613.
- **Async CSV export** -- POST returns key, GET retrieves.
- **Per-appointment CSV download** -- alternative to bulk export.
- **Permission:** Supervisor + IT Admin (per spec permission matrix).

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Api/Controllers/.../AppointmentRequestReportController.cs` | Report endpoints (marked Not In Use in OLD docs) |
| `PatientAppointment.Api/Controllers/.../AppointmentRequestReportSearchController.cs` | Search-based report generation |
| `PatientAppointment.Api/Controllers/.../CSVExportController.cs` | CSV export |
| `patientappointment-portal/.../appointment-request/appointment-request-report/...` | Report UI |
| `Infrastructure/.../ClosedXML` (per Tools list) | Excel generation lib (replace with `EPPlus` or ABP's Excel utility) |

## NEW current state

- TO VERIFY: NEW likely has CSV export utility.
- ABP Commercial has `Volo.Abp.Reporting` module options.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Appointment Request Report | OLD | TO VERIFY | **Add `IAppointmentReportsAppService.GenerateAppointmentRequestReportAsync(filterDto)`** -- returns tabular DTO; UI renders HTML; export buttons trigger Excel/PDF generation | I |
| Filters: date range, location, type, status, patient name, doctor name | OLD | -- | **Match in DTO** | I |
| Excel export | OLD: ClosedXML | -- | **Use `Volo.Abp.MimeTypes` + EPPlus** (Apache 2.0) or DocumentFormat.OpenXml; replace ClosedXML | -- |
| PDF export | OLD: iTextSharp | -- | **Use `QuestPDF`** (Apache 2.0; modern .NET PDF lib) replacing iTextSharp | -- |
| CSV export async pattern | OLD: POST start + key + GET retrieve | -- | **Match async pattern**: `IExportAppService.StartCsvExportAsync(filterDto)` returns export key; `GetCsvAsync(key)` returns the file. Use Hangfire for the async generation. | I |
| Per-appointment CSV | OLD: GET /api/CsvExport/{appointmentId}/{downloadType} | -- | **Add similar endpoint** | I |
| Excel ODBC link | OLD: SQL views | -- | **Defer to Phase 2** -- Phase 1 use the in-app reports; ODBC views can be added later as a separate slice | -- |
| Schedule Report (multi-tenant) | OLD spec line 599 | -- | **Defer to Phase 2** -- requires the replicated read-only DB design | -- |
| Permission gates | OLD: Supervisor + IT Admin | -- | **`CaseEvaluation.Reports.{Default, Generate, Export}`** -- gate to Supervisor + IT Admin | I |

## Internal dependencies surfaced

- `IBlobStorage` for storing generated export files (key -> blob URL).
- Hangfire for async report generation.

## Branding/theming touchpoints

- Report UI (logo, primary color, table styling).
- Excel/PDF export branding (header, logo, footer).

## Replication notes

### ABP wiring

- `IAppointmentReportsAppService` + manual controller.
- Async export via Hangfire job: queues generation, writes blob, returns key.
- Excel generation via EPPlus (or `Volo.Abp.Excel`).
- PDF generation via QuestPDF.
- Permissions: `CaseEvaluation.Reports.*` granted to Supervisor + IT Admin.

### Things NOT to port

- ClosedXML -> EPPlus.
- iTextSharp -> QuestPDF.
- Stored procs / SQL views -- LINQ-to-EF unless ODBC slice is in Phase 2 scope.
- The Not-In-Use AppointmentRequestReport CRUD endpoints -- skip; only port the functionality.

### Verification

1. Supervisor opens Reports page -> sees Appointment Request Report option
2. Set filters + Generate -> tabular report renders
3. Export to Excel -> file downloads
4. Export to PDF -> file downloads
5. Per-appointment CSV: from list page, "Download CSV" -> file downloads
6. Async export with bulk filters -> POST returns key + spinner; GET retrieves when ready
