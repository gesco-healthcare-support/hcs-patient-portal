# 08. Reporting & export -- OLD vs NEW behavioral parity

## Coverage

Scope: every report a user could run and every data-export path in the
OLD Patient Portal -- the Appointment Request Report (HTML table + Excel
+ PDF export), the per-appointment Patient-Demographics print-to-PDF, the
async export-key handshake, the spec'd-but-unbuilt Schedule Report, and
the spec'd-but-unbuilt Excel ODBC pivot link. The whole area is expected
to be MISSING in NEW; this audit documents each lost capability so the
owner can decide scope and priority.

OLD anchors read in full:
- `PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentRequestReportController.cs`
  (CRUD over the `AppointmentRequestReport` entity -- generic scaffold).
- `PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentRequestReportSearchController.cs`
  (`POST /api/appointmentrequestreport/search` -> `EXEC spm.spAppointmentRequestReport @Query,@UserId`).
- `PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs`
  (the async export-key handshake + Excel/PDF render + per-appointment
  demographics PDF; uses ClosedXML + iTextSharp).
- `PatientAppointment.DbEntities\Models\AppointmentRequestReport.cs`
  (`: Appointment` -- empty subclass; report binds to a stored proc, not
  this entity).
- `PatientAppointment.Models\ViewModels\ExportViewModel.cs`,
  `ExportKeyViewModel.cs`, `ReportViewModel.cs`.
- `PatientAppointment.Api\Controllers\Api\Core\DashboardController.cs`
  (counter aggregate only -- belongs to Audit 09, listed here to confirm
  it is NOT a report/export).
- `patientappointment-portal\src\app\components\appointment-request\appointment-request-report\`
  (module, routing, service, domain, models) +
  `search\appointment-request-report-search.component.{ts,html}`.
- `patientappointment-portal\src\app\view-models\appointment-request-report-view-report.ts`
  (filter view model).
- `patientappointment-portal\src\app\components\login\login.service.ts:35`
  (`printPDF(appointmentId)` -> `GET api/CsvExport/{id}/1`).
- `PatientAppointment.Api\AppConfiguration\AllowedApis.cs:22,36`
  (`CsvExportController` on the auth + authorization BYPASS lists).
- `Documents_and_Diagrams\Architecture\SoCal Project Overview Document.pdf`
  p.14 "View Reports" (authoritative spec) + `_local\stub-procs.sql:319`
  (`spm.spAppointmentRequestReport` -- a `WHERE 1=0` stub in this copy).

NEW anchors checked (to confirm absence):
- `src\HealthcareSupport.CaseEvaluation.Application\Dashboards\`,
  `...Application.Contracts\Dashboards\`,
  `...HttpApi\Controllers\Dashboards\` -- only the dashboard counter
  AppService exists; no report or export AppService/controller.
- Grep across `src\` for `report|export|csv|excel|ClosedXML|QuestPDF|iText|spreadsheetml|.xlsx`:
  every hit is incidental (migration files, DTO names like
  `DoctorAvailabilityCreateRangeResultDto`, the packet-generation
  pipeline). No report/export class exists.
- `angular\src\app\app.routes.ts` (full) -- no `report` or `export`
  route. `angular\src\app\` has no `report/` or `export/` feature
  directory. The only "Patient Demographics" string in the SPA is an
  editable form section heading on `appointment-view.component.html:197`,
  not a printable report.

### Key structural findings

**1. Schedule Report and Excel ODBC Link were specified but never built
in OLD.** The "View Reports" spec (Overview p.14) lists three reports:
Schedule Report, Appointment Request Report, and Excel ODBC Link. Only
the Appointment Request Report exists in OLD source. There is no
Schedule Report controller, component, route, read-only replica DB, or
merged-doctor query anywhere in `P:\PatientPortalOld`; the single
"Reports" sidebar item (`side-bar.component.html:152`) routes to `report`
which loads only the Appointment Request Report. There is no ODBC view
exposure, connection string, or `vReport*` view wiring in source either.
Because the audit's ground truth is CODE, these two are recorded as
"spec-only, not in OLD code" -- they are NOT behavioral parity gaps
(nothing a user could do in OLD is lost), but they are flagged as Open
Questions because the spec calls for them and the owner may still want
them.

**2. The Appointment Request Report is single-doctor by construction.**
The OLD filter view-model
(`appointment-request-report-view-report.ts:28-29`) declares a
`doctorName` field, but the report's HTML
(`appointment-request-report-search.component.html`) renders NO doctor
input and NO doctor column -- because the OLD app is single-tenant (one
doctor's office per database). The `doctorName` field is dead in OLD.
This matters for NEW parity: a faithful NEW report needs the same six
spec'd filters only if/when it becomes multi-doctor (Phase 2); the
single-office Phase 1 report mirrors OLD's five live filters.

**3. The report binds to a stored proc, not the entity.** The visible
report comes from `POST /api/appointmentrequestreport/search` ->
`EXEC spm.spAppointmentRequestReport @Query,@UserId`, which returns a
single JSON blob (`StoreProcSearchViewModel.Result`) the rx-table
paginates/sorts client-server via `isStoreProc=true`. The
`AppointmentRequestReportController` CRUD endpoints and the
`AppointmentRequestReport : Appointment` entity are unused scaffolding
for the visible report. NEW would replace the proc with an EF query --
an expected view->query translation, NOT a gap.

## Summary counts

| Class | Count |
|---|---|
| Missing behavior | 4 |
| Partial behavior | 0 |
| Intent deviation | 0 |
| Equivalent (different implementation) | 0 |
| OLD-bug (do not port) | 2 |

(Equivalent count is 0 because NEW has built nothing in this area yet --
there is no alternate implementation to compare. The library-swap
"equivalents" the brief anticipates -- ClosedXML->any-xlsx-lib,
iTextSharp->QuestPDF, SQL-view->EF-query -- are listed under "Equivalent"
below as guidance for the eventual build, not as scored rows.)

## Report / export inventory

| OLD capability | OLD source | Visible to | NEW status |
|---|---|---|---|
| Appointment Request Report (HTML table, paged/sorted) | `AppointmentRequestReportSearchController.cs:20-27`; `*-search.component.html` | Clinic Staff, Supervisor, IT Admin (`MODULES.Reports`) | Absent |
| Report filters: date range, location, type, status, patient name | `*-search.component.ts:97-136`; `*-view-report.ts:10-29` | same | Absent |
| Report column set (10 cols incl. SSN, DOB, email, phone) | `*-search.component.html:113-140` | same | Absent |
| Export to Excel (.xlsx) via async key | `CSVExportController.cs:109-115` (POST key) + `:42-67` (GET, downloadType=2, ClosedXML) | report viewers | Absent |
| Export to PDF (HTML render of table) | `CSVExportController.cs:53-61` (downloadType=1) + `GeneratePDF :157-241` | report viewers | Absent |
| Per-appointment Patient-Demographics PDF (print) | `CSVExportController.cs:73-106` + `GetPatientDataHtml :302-681`; `login.service.ts:35` `printPDF`; `*-search.component.ts:161-166` `exportPdf` | report viewers + appointment viewers | Absent |
| Async export handshake (POST data -> key -> GET render) | `CSVExportController.cs:109-115` + `:42-70`; `ExportKeyViewModel`, `ExportViewModel` | report viewers | Absent |
| Schedule Report (merged 3-doctor, read-only replica) | Spec p.14 ONLY -- not in OLD code | (Supervisor/IT Admin per spec) | Absent (also absent in OLD code) |
| Excel ODBC pivot link (3-4 views) | Spec p.14 ONLY -- not in OLD code | (ad-hoc analyst per spec) | Absent (also absent in OLD code) |
| Dashboard counters | `DashboardController.cs:45-54` (`spDashboardCounters`) | internal users | Present (NEW Dashboards AppService) -- NOT reporting; see Audit 09 |

## Behavioral gaps

### G-08-01 -- Appointment Request Report (the whole report) is absent

- **Class:** Missing behavior
- **OLD:** `AppointmentRequestReportSearchController.cs:20-27`
  (`POST /api/appointmentrequestreport/search` ->
  `EXEC spm.spAppointmentRequestReport @Query,@UserId`); Angular
  `appointment-request-report-search.component.{ts,html}`; reached from
  the "Reports" sidebar item (`side-bar.component.html:152`, gated on
  `MODULES.Reports`).
- **NEW:** No report AppService, controller, route, or component. Grep
  across `src\` and `angular\src\app\` finds no report feature. The
  sidebar has no Reports entry.
- **What it is:** A searchable, paged, sortable HTML table of all
  appointment requests with ten columns: Confirmation No (links to the
  appointment), Appointment Type, Location Name, Appointment Date Time,
  Status, Patient Name, Date Of Birth, Email, Phone Number, Social
  Security Number. Default sort: `requestConfirmationNumber desc`
  (`*-search.component.ts:63-64`). Has a free-text quick search plus an
  "Advanced Search" collapsible panel with five filters.
- **Why it existed:** Internal staff need an operational worklist /
  lookup of every request across all patients -- the central "find any
  appointment and its patient details" screen, distinct from the
  dashboard (counts only) and the per-appointment view (one record).
- **What it does + user impact:** Without it, internal staff in NEW have
  no cross-appointment tabular report -- they cannot list/scan/filter all
  requests in one grid, cannot find a patient by name/DOB across
  appointments from a report screen, and have no export source. They are
  limited to whatever per-record navigation the appointments feature
  offers. For a clinic-operations user this is a core daily tool.
- **Plain-English:** OLD had a "Reports" screen that listed every
  appointment in a searchable table with patient name, DOB, contact info
  and status, filterable by date/location/type/status/patient. NEW has no
  such screen at all.
- **Keep in NEW?** Yes -- high priority. This is the primary reporting
  surface in OLD and is used by Clinic Staff, Supervisor, and IT Admin.
  Build as an ABP AppService (EF query replacing the stored proc) +
  Angular grid behind a `CaseEvaluation.Reports` permission.

### G-08-02 -- Export to Excel (.xlsx) is absent

- **Class:** Missing behavior
- **OLD:** `CSVExportController.cs:109-115` (`POST /api/csvexport` stores
  the client-rendered CSV under a GUID key) + `:42-67`
  (`GET /api/csvexport/{key}?fileName=&downloadType=2`, branch
  `downloadType==2` -> `ConvertWithClosedXml :123-155` writes an `.xlsx`
  with ClosedXML and streams it). Triggered from the report toolbar
  "Export to Excel" button
  (`*-search.component.html:101-104`,
  `tableEvent.exportToCsv('Appointment Request Report',2)`).
- **NEW:** No Excel export anywhere. No ClosedXML / EPPlus / any xlsx
  library referenced in `src\` (only QuestPDF, used for packets). No
  export endpoint.
- **What it is:** A one-click download of the current report (header row
  + data rows) as an Excel spreadsheet, file-named after the report.
- **Why it existed:** Staff hand the appointment list to billing /
  management / external parties in a spreadsheet they can sort, filter,
  and pivot offline.
- **What it does + user impact:** NEW users cannot get the appointment
  list into Excel at all -- no spreadsheet hand-off, no offline analysis.
  Any downstream process that expected an .xlsx breaks.
- **Plain-English:** OLD let you click "Export to Excel" on the report
  and download a spreadsheet of the rows. NEW cannot produce a
  spreadsheet.
- **Keep in NEW?** Yes -- ships with G-08-01. The ClosedXML/iTextSharp
  library choice is NOT load-bearing; any .xlsx writer (ClosedXML, EPPlus,
  or even CSV) satisfies parity. Outcome = "user downloads the report as a
  spreadsheet".

### G-08-03 -- Export to PDF (report table) is absent

- **Class:** Missing behavior
- **OLD:** `CSVExportController.cs:53-61` (`downloadType==1` branch) ->
  `GeneratePDF :157-241` builds a styled HTML table (Bootstrap CDN, green
  header, zebra striping, a print button) and returns it as
  `ReportViewModel.HtmlString`; the SPA opens it in a new window for the
  browser's print-to-PDF. Triggered from "Export to PDF"
  (`*-search.component.html:97-100`,
  `tableEvent.exportToCsv('Appointment Request Report',1)`).
- **NEW:** No report PDF export. (QuestPDF exists in the Domain project
  but is wired only into the document/packet pipeline, not reports.)
- **What it is:** A printable PDF rendering of the report grid -- the same
  rows as the Excel export but laid out as a print-friendly HTML table.
- **Why it existed:** Staff print or PDF the appointment list for
  paper-based workflows, filing, or sharing an immutable snapshot.
- **What it does + user impact:** NEW users cannot produce a PDF/printable
  version of the report. Per the root mission (OLD DOCX/print -> NEW PDF),
  the NEW build should emit a real PDF (QuestPDF) rather than OLD's
  open-HTML-and-print trick, but the capability itself is missing today.
- **Plain-English:** OLD let you print/PDF the report list. NEW cannot.
- **Keep in NEW?** Yes -- ships with G-08-01, rendered as a true PDF via
  QuestPDF (immutable, per mission). The iTextSharp/HTML-print mechanism
  is not load-bearing; only the outcome (a PDF of the report) matters.

### G-08-04 -- Per-appointment Patient-Demographics print-to-PDF is absent

- **Class:** Missing behavior
- **OLD:** `CSVExportController.cs:73-106`
  (`GET /api/csvexport/{appointmentId}/{downloadType}`) +
  `GetPatientDataHtml :302-681` assembles a full single-appointment
  demographic sheet: Appointment Details, Patient Details (name, phone,
  address, has-interpreter, email, language, DOB, SSN, referred-by),
  Employer Details, per-injury Injury/Insurance/Claim-Examiner blocks
  (incl. body parts), Applicant Attorney, Defense Attorney, Appointment
  Accessors (with Edit/View rights), and Custom Form fields. Invoked two
  ways: from the report row's PDF icon
  (`*-search.component.ts:161-166` `exportPdf` ->
  `login.service.ts:35` `printPDF(appointmentId)` -> `.../{id}/1`) and
  via `appointment-request-report.service.ts:72-78` `exportPdf`.
- **NEW:** No per-appointment demographic PDF/print. The SPA
  appointment-view shows a "Patient Demographics" heading
  (`appointment-view.component.html:197`) but it is an EDITABLE reactive
  form section, not a read-only printable export -- no PDF/print button
  exists on that view.
- **What it is:** A one-click, read-only, print-ready PDF of one
  appointment's complete intake -- every party and detail on a single
  document.
- **Why it existed:** Staff print a patient's full demographic/intake
  sheet to bring to the exam, attach to a paper file, or hand off -- a
  whole-record snapshot the on-screen view does not provide as a
  document.
- **What it does + user impact:** NEW users cannot generate a printable
  single-appointment intake document. They can view the record on screen
  but cannot produce the consolidated PDF that paper/clinical workflows
  rely on.
- **Plain-English:** OLD had a PDF icon on each appointment that produced
  a complete printable patient sheet (all the intake details on one
  page). NEW has no such printable sheet.
- **Keep in NEW?** Yes -- medium/high priority; arguably more useful than
  the report-grid PDF for clinical use. Render via QuestPDF as an
  immutable PDF (per mission). Note OLD includes SSN and DOB on this
  sheet (`CSVExportController.cs:410, 409`) -- decide whether NEW masks
  PHI on the printed copy (see Open Questions / HIPAA).

## Equivalent -- different implementation

NEW has built nothing in this area, so there are no
already-implemented equivalents to score. The following are translation
notes for the eventual build, so the implementer does NOT misclassify the
expected library/data-access swaps as parity gaps:

1. **ClosedXML -> any .xlsx writer (or CSV).** OLD's Excel export uses
   `ClosedXML.Excel.XLWorkbook` (`CSVExportController.cs:123-155`). NEW
   may use EPPlus, ClosedXML, or emit CSV. Outcome-equivalent = not a
   gap.
2. **iTextSharp HTML-print -> QuestPDF.** OLD returns styled HTML the
   browser prints (`GeneratePDF`, `GetPatientDataHtml`); the dead
   `CreatePDF`/`CreatePDFForDemographics` methods (`:243-300`) used
   iTextSharp `XMLWorkerHelper` but are commented out of the live path.
   NEW should emit a real immutable PDF via QuestPDF (per root mission
   "PDF replaces DOCX/print"). Same outcome (a PDF), better fidelity =
   not a gap.
3. **Stored proc (`spm.spAppointmentRequestReport`) -> EF/LINQ query.**
   OLD binds the report to a JSON-returning stored proc
   (`AppointmentRequestReportSearchController.cs:25`). NEW replaces it
   with an `IRepository`/`IQueryable` paged query. Expected view->query
   translation = not a gap.
4. **rx-table client `exportToCsv` -> server-rendered export.** OLD's
   in-house `@rx` grid serializes the visible rows to CSV on the client,
   POSTs them to get a key, then GETs the rendered file
   (`*-search.component.html:97-104` + the export-key handshake). NEW can
   render the export server-side directly from the query (simpler,
   avoids the GUID-key round-trip). Same outcome = not a gap. (The OLD
   handshake is itself fragile -- see OLD bugs.)
5. **Excel ODBC views -> NEW ad-hoc reporting mechanism (if ever built).**
   The spec'd ODBC pivot link (never built in OLD) would, in NEW's
   per-tenant DB-per-tenant model, more naturally be exposed views or a
   read API; a literal ODBC pivot is not the only valid outcome. Out of
   parity scope until the owner asks (see Open Questions).

## OLD bugs (do not port)

1. **`CsvExportController` is on BOTH auth-bypass lists
   (`AllowedApis.cs:22, 36`).** The export controller is exempt from
   authentication (`AuthenitcationByPass`) AND authorization
   (`AuthorizationByPass`). Combined with the static
   `ConcurrentDictionary<string,string> Data` cache
   (`CSVExportController.cs:31`) keyed by a returned GUID, this means
   anyone who POSTs report data and gets a key can GET it back rendered,
   with no login and no role check. For a report that includes SSN, DOB,
   and full patient contact details, this is a PHI-exposure bug. NEW must
   put the report + export behind authentication and a
   `CaseEvaluation.Reports` permission -- do not port the bypass.
2. **Process-wide static export cache (`CSVExportController.cs:31-32`).**
   `Data` (the GUID->CSV map) and `filePath` are `static`, so they are
   shared across all requests/users for the life of the process, the
   files are written to a shared `reportFilePath` and overwritten by
   filename (`ConvertWithClosedXml :152`, `Get :65` reads
   `filePath + fileName + ".xlsx"`), and entries are never evicted
   (`DeleteExistingFiles :683-687` is never called). Two concurrent
   exports of the same report name race on the same file; the cache
   leaks indefinitely. NEW should render exports per-request in-memory /
   streamed, not via a shared static cache + named temp files -- do not
   port this pattern.

Additional non-blocking OLD oddities (note, do not port):
- **`AppointmentRequestReportController` is generic CRUD scaffolding** for
  an entity (`AppointmentRequestReport : Appointment`, an empty subclass)
  that the visible report never uses -- the report is proc-bound. Dead
  scaffold; build only the search/export the UI actually calls.
- **Dead `doctorName` filter** (`*-view-report.ts:28-29`) with no UI
  control or column -- single-tenant artifact; see structural finding 2.
- **`GeneratePDF`'s `dobCount==4` hack** (`CSVExportController.cs:216-220`)
  positionally replaces `.` with `-` in the 5th CSV column assuming it is
  the DOB. This is brittle column-position coupling to the client CSV
  order; NEW should format dates by field, not by column index.

## Open questions

1. **Schedule Report -- build it, or was it dropped?** The spec (Overview
   p.14) describes a merged read-only report across all three doctors'
   websites in a separate replica DB, with six filters (date range,
   location, type, status, patient name, doctor name). It does not exist
   in OLD code. Does the owner still want it? In NEW's per-tenant
   architecture a cross-doctor merged report is a Phase 2 (multi-tenant)
   concern and conflicts with the Phase 1 single-office, no-cross-tenant
   constraint. Recommend: defer to Phase 2, confirm with owner.
2. **Excel ODBC pivot link -- build it, or replace with a NEW
   mechanism?** The spec calls for "3-4 views exposed via ODBC ... linked
   into MS Excel as an external data source for ad-hoc reporting." Never
   built in OLD. NEW's DB-per-tenant model makes a literal ODBC pivot
   awkward; a read-only reporting API or per-tenant views are more
   natural. Does the owner need Excel-pivot-style ad-hoc access at all?
3. **Does the NEW report mask PHI on export/print?** OLD's report grid
   and the per-appointment PDF show full SSN and DOB
   (`*-search.component.html:127, 130`; `CSVExportController.cs:409-410`).
   The SPA already has an `ssn-mask.pipe.ts`. Confirm whether NEW report
   columns and the exported/printed files should mask SSN (and possibly
   DOB) -- a HIPAA decision the owner must make before build.
4. **Which filters does the Phase 1 report expose?** OLD's live UI has
   five (date range counts as one filter pair: type, location, status,
   patient name, date range) and a dead doctorName field. NEW Phase 1 is
   single-office, so doctorName is moot. Confirm the Phase 1 filter set
   matches OLD's live five (not the spec's six).
5. **Export rendering location.** OLD exports client-rendered rows
   (paginated grid -> only the visible page?) via the CSV handshake.
   Confirm NEW should export the FULL filtered result set server-side
   (correct, and what users expect), not just the on-screen page -- a
   likely improvement over OLD's behavior that should be verified against
   what OLD actually exported.
