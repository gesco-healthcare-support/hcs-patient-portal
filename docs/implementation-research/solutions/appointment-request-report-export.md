# Appointment request report + CSV/XLSX/(PDF) export

## Source gap IDs

- [03-G07](../../gap-analysis/03-application-services-dtos.md) --
  `AppointmentRequestReport` entity service. Track 03 row 143,
  evidence at OLD `AppointmentRequestReportController.cs:29-52`, effort
  `1-2 days`. NEW absent.
- [03-G11](../../gap-analysis/03-application-services-dtos.md) --
  CSV/Excel/PDF export (generic). Track 03 row 147, evidence at OLD
  `CSVExportController.cs:42-116`, effort `3-5 days`. NEW has per-entity
  Excel on `WcabOffices` (and `UserExtended`) only.
- [5-G08](../../gap-analysis/05-auth-authorization.md) -- `Reports`
  permission group. Track 05 line 196, evidence at OLD
  `access-permission.service.ts:32,53,76`. NEW absent.
- [G-API-12](../../gap-analysis/04-rest-api-endpoints.md) -- Appointment
  request report + search (5 endpoints). Track 04 line 133, effort
  `Small-Medium`.
- [G-API-13](../../gap-analysis/04-rest-api-endpoints.md) -- CSV/PDF/XLSX
  export (3 endpoints, generic). Track 04 line 134, effort `Medium-Large`.
- [A8-13](../../gap-analysis/08-angular-proxy-services-models.md) --
  `AppointmentRequestReport + exportPdf` Angular service. Track 08 row 213,
  evidence at OLD
  `appointment-request\appointment-request-report\appointment-request-report.service.ts:1-80`,
  effort `M`.
- [R-08](../../gap-analysis/07-angular-routes-modules.md) -- `/report`
  search route. Track 07 line 143, evidence at OLD
  `appointment-request-report-search.routing.ts:9-13`, effort `L`.
- [UI-11](../../gap-analysis/09-ui-screens.md) -- `/report` report search
  page. Track 09 line 149. "Large (depends on G2-11 data + G-API-13
  export)".

Track 10 errata that apply:

- [Erratum 1](../../gap-analysis/10-deep-dive-findings.md#erratum-1--old-does-not-generate-pdfs-server-side-track-03-track-06)
  -- OLD `iTextSharp` PDF methods `CreatePDF()` at
  `CSVExportController.cs:243-270` and `CreatePDFForDemographics()` at
  `CSVExportController.cs:273-300` are NEVER CALLED (call sites commented
  on lines 100 and 239). Only XLSX (ClosedXML) and HTML-for-`window.print()`
  are active. Server-side PDF is therefore optional for parity.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Application/WcabOffices/WcabOfficesAppService.cs:94-109`
  -- `GetListAsExcelFileAsync(WcabOfficeExcelDownloadDto input)` is
  `[AllowAnonymous]`, validates the single-use `DownloadToken` against
  `IDistributedCache<WcabOfficeDownloadTokenCacheItem, string>`, rebuilds
  the filtered list via the repo, projects to an anonymous type, and
  streams via `MiniExcelLibs.MiniExcel.SaveAsAsync` into a
  `MemoryStream` wrapped in `RemoteStreamContent` with MIME
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- `src/HealthcareSupport.CaseEvaluation.Application/WcabOffices/WcabOfficesAppService.cs:123-131`
  -- `GetDownloadTokenAsync` mints a `Guid.NewGuid().ToString("N")` token,
  stores it for `TimeSpan.FromSeconds(30)` in the distributed cache,
  returns `DownloadTokenResultDto`. This is the CSRF-proof pattern for
  anonymous binary downloads (anon route + short-lived pre-minted token).
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/WcabOffices/WcabOfficeController.cs:75-87`
  -- Controller plumbing: `[HttpGet]` `as-excel-file` delegates to
  `GetListAsExcelFileAsync`; `[HttpGet]` `download-token` delegates to
  `GetDownloadTokenAsync`. Route prefix `api/app/wcab-offices`. Pattern
  template for the per-entity approach.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:29-464`
  -- `AppointmentsAppService` has NO export method. 14 methods: `GetListAsync`,
  `GetWithNavigationPropertiesAsync`, `GetAsync`, 5 `*LookupAsync`,
  `DeleteAsync`, `CreateAsync`, `UpdateAsync`,
  `GetApplicantAttorneyDetailsForBookingAsync`,
  `GetAppointmentApplicantAttorneyAsync`,
  `UpsertApplicantAttorneyForAppointmentAsync`. No `GetListAsExcelFileAsync`
  or `GetDownloadTokenAsync`.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/GetAppointmentsInput.cs:7-30`
  -- `GetAppointmentsInput` already carries 8 filter fields
  (`FilterText`, `PanelNumber`, `AppointmentDateMin`, `AppointmentDateMax`,
  `IdentityUserId`, `AccessorIdentityUserId`, `AppointmentTypeId`,
  `LocationId`). These match the OLD report-search fields and are the
  same bag an export-of-filtered-view should accept. No AppointmentStatus
  filter yet (OLD `/report` search has that) -- gap surfaced by this work.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:1-133`
  -- No `Reports` nested static class. 15 entity groups plus `Dashboard`;
  `Reports.Default / Export` entirely absent, matching 5-G08.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentTypes/AppointmentTypeExcelDownloadDto.cs`
  + `AppointmentTypeExcelDto.cs` (noted in
  `docs/gap-analysis/03-application-services-dtos.md:86`) -- DTO shapes
  for `AppointmentType` Excel export exist but `IAppointmentTypesAppService`
  has no matching method. Dead scaffolding from the ABP generator that
  signals the team intended to extend the per-entity XLSX pattern.
- `angular/src/app/wcab-offices/wcab-office/services/wcab-office.service.ts`
  (referenced via auto-generated proxy for the as-excel-file endpoint) --
  shape to mirror for a new `appointments/services/appointment-export.service.ts`.
- `angular/src/app/app.routes.ts:99-102` -- only `/appointments`,
  `/appointments/add`, `/appointments/view/:id` registered in the root
  routing; no `/report` or `/reports` feature module. No
  `AppointmentRequestReport` client model.
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentRequestReports/`
  -- directory does NOT exist. Grep confirms no `AppointmentRequestReport`
  entity, manager, or repository anywhere in the NEW codebase.

## Live probes

- **Probe 1** (2026-04-24T13:27 local):
  `curl -sk https://localhost:44327/swagger/v1/swagger.json` returns HTTP
  200 with 2.6 MB body. Filter `/api/app/[a-z-]+/(as-excel-file|download-token)`
  yields exactly four matches:

    ```
    /api/app/user-extended/as-excel-file
    /api/app/user-extended/download-token
    /api/app/wcab-offices/as-excel-file
    /api/app/wcab-offices/download-token
    ```

  Filter `/api/app/[a-z-]+/.*export.*` returns zero matches. Filter
  `/api/app/appointments.*` returns 9 paths, none of which include
  `export`, `excel`, `download-token`, or `report`. Proves NEW has the
  per-entity Excel pattern wired on 2 entities only and Appointments
  have no export surface today. Full log:
  `../probes/appointment-request-report-export-2026-04-24T13-27-00.md`.

## OLD-version reference

- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs:42-70`
  -- `GET /api/csvexport/{key}` with `fileName` + `downloadType` query.
  Client POSTs CSV text, receives a GUID key; then GETs it back as XLSX
  (`downloadType != 1` at line 64) or as HTML (`downloadType == 1` at line
  55) the Angular client then window.prints. The `Data` variable is a
  process-local `ConcurrentDictionary` (line 31) -- no multi-instance
  safety; key is in-memory only and lost on restart.
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs:73-106`
  -- `GET /api/csvexport/{appointmentId}/{downloadType}` returns
  `{ HtmlData: "..." }` populated from 7 `v*` stored-proc views
  (`vPatient`, `vAppointmentDetail`, `vAppointmentEmployerDetail`,
  `vAppointmentPatientAttorney`, `vAppointmentDefenseAttorney`,
  `vInjuryDetail`, `AppointmentInjuryBodyPartDetail`) -- this is the
  "Patient Demographics" per-appointment sheet. Angular client calls
  `window.print()` on the injected HTML; server-side PDF
  (`CreatePDFForDemographics` line 100) is NEVER called.
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentRequestReportController.cs:13-53`
  -- REST shell over an `AppointmentRequestReport` entity. Methods:
  `GET /` (All), `GET /{id}` (find-by-appointmentId),
  `POST /`, `PUT /{id}`, `DELETE /{id}`. Persists rows via generic
  `IUow.Repository<AppointmentRequestReport>().RegisterNew / Dirty /
  Deleted`. Zero auth attributes; OLD's `AllowedApis.cs` leaves it under
  JWT but without per-method permission. Confirms the entity is a
  persisted sibling to `Appointment` (not a materialised view).
- `P:\PatientPortalOld\patientappointment-portal\src\app\database-models\appointment-request-report.ts:1-139`
  -- OLD Angular model: 34 properties mirror `Appointment` plus 13 nested
  collections (`AppointmentDefenseAttorney`, `AppointmentPatientAttorney`,
  `AppointmentChangeRequest`, `Note`, `AppointmentDocument`,
  `CustomFieldsValue`, `AppointmentChangeLog`, `AppointmentJointDeclaration`,
  `AppointmentNewDocument`, `AppointmentAccessor`, `AppointmentInjuryDetail`,
  `AppointmentEmployerDetail`). Evidence the `AppointmentRequestReport`
  table is essentially a denormalised audit snapshot of a booking request
  -- confirms Adrian's open question Q3 phrased as "materialised report
  row or audit trail?". In practice it behaves as a sibling history
  table; in OLD the search page reads it, not `Appointment`.
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-request-report\appointment-request-report.routing.ts:5-13`
  -- Angular route `''` under `/report`, `canActivate: [PageAccess]`,
  `data: { applicationModuleId: 13, accessItem: 'search', keyName:
  'appointmentId', ... }`. Loads the search module.
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-request-report\search\appointment-request-report-search.component.ts:55-121`
  -- search form wires 6 filters (`appointmentTypeId`, `locationId`,
  `appointmentStatusId`, `patientName`, `doctorName`,
  `appointmentStartDate`, `appointmentEndDate`). Validation refuses empty
  searches; start/end date comparison enforces ordering. Bound to the
  `access-permission.service` with `MODULES.Reports` gate.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2 on .NET 10; MiniExcelLibs is already referenced
  by the Application project via `WcabOfficesAppService.cs:16` (`using
  MiniExcelLibs;`) and the `as-excel-file` endpoints are the canonical
  ABP Suite-generated pattern. Do not introduce a second Excel library.
- ADR-001 (Mapperly over AutoMapper) -- new DTOs for the excel row
  projection must either use an anonymous type inline (the WcabOffice
  pattern at line 104) or a Mapperly `[Mapper]` partial in
  `CaseEvaluationApplicationMappers.cs`.
- ADR-002 (manual controllers) -- every new AppService method requires a
  matching controller delegate in
  `src/.../HttpApi/Controllers/Appointments/AppointmentController.cs`,
  and the AppService must keep `[RemoteService(IsEnabled = false)]`.
- ADR-004 (row-level `IMultiTenant`) -- `Appointment` is `IMultiTenant`,
  so the repository's `GetCountAsync` / `GetListWithNavigationPropertiesAsync`
  will auto-filter rows to the current tenant; the export inherits the
  same scope by design. Host admins see all tenants via
  `IDataFilter.Disable<IMultiTenant>()` when the host context drives it.
- HIPAA applicability: an XLSX of appointment rows contains PHI (patient
  names via `WithNavigationProperties`, DOB via joins, injury fields when
  joined). Track 10 recorded that NEW's audit-log coverage on downloads
  is nil; every export execution must be recorded in the ABP audit log
  with the resolved filter payload and row count. Track 10 Part 2
  `NEW-SEC-02` also flags that most NEW AppService mutators are missing
  method-level authorize attributes; adding a method-level
  `[Authorize(CaseEvaluationPermissions.Reports.Export)]` on the new
  export method is mandatory for this capability, independent of the
  NEW-SEC-02 broader sweep.
- The existing `GetAppointmentsInput` lacks `AppointmentStatus` filter
  (`GetAppointmentsInput.cs:7-30`); the repo's `ApplyFilter`
  (`EfCoreAppointmentRepository.cs:92-108`) only matches `FilterText`
  against `PanelNumber`. OLD's report page needs status, patient-name,
  and doctor-name filters. Either extend `GetAppointmentsInput` here or
  defer to `appointment-search-listview` and re-use whatever it adds.
- Distributed cache requirement: `IDistributedCache<..., string>` is
  already wired in NEW. Root CLAUDE.md notes Redis is optional locally
  ("disabled by default locally"), and ABP falls back to in-memory
  distributed cache when Redis is off. The download-token flow works
  without Redis in dev; Redis is required in prod for multi-instance
  stability.
- Plan-mode constraint: ASCII only, cite every claim, no PHI in any
  example row or screenshot.

## Research sources consulted

1. ABP Commercial docs --
   `https://commercial.abp.io/modules/SaaS` (general module listing;
   accessed 2026-04-24). Confirms ABP Commercial 10 offers no bespoke
   "Reports" module; reports are app-level concerns in ABP.
2. MiniExcel GitHub repo --
   `https://github.com/mini-software/MiniExcel` (accessed 2026-04-24).
   Apache-2.0 license, supports .NET 4.5 through .NET 9.0+ (.NET 10 by
   implication), streaming `SaveAsAsync` for low-memory XLSX export.
   Matches the WcabOffice implementation pattern.
3. QuestPDF licensing --
   `https://www.questpdf.com/license/` (accessed 2026-04-24). MIT
   Community license free for companies under 1M USD annual gross
   revenue; Professional 999 USD for >=10 devs; Enterprise 2,999 USD for
   org-wide. Gesco qualifies for Community tier today. Cited also by
   track 10 Part 4.
4. iTextSharp / iText 7 licensing --
   `https://itextpdf.com/products/itext-7-core/license` (accessed
   2026-04-24). AGPL or paid commercial; AGPL infects closed-source
   SaaS. QuestPDF is preferred specifically because its MIT Community
   tier does not infect.
5. ABP distributed cache docs --
   `https://abp.io/docs/latest/framework/fundamentals/distributed-cache`
   (accessed 2026-04-24). Confirms `IDistributedCache<TCacheItem,
   TCacheKey>` falls back to in-memory when Redis is absent; ABP
   prefixes keys with `c:` and the generic type name.
6. ABP CRUD "Excel export" Suite generator --
   `https://docs.abp.io/en/commercial/latest/abp-suite/creating-a-new-solution`
   (accessed 2026-04-24). Confirms ABP Suite scaffolds exactly the
   `GetDownloadTokenAsync` + `GetListAsExcelFileAsync` two-step CSRF
   pattern when "enable excel export" is ticked. This is what NEW has
   for WcabOffices, so building the same on Appointments is idiomatic.
7. OLD code reference --
   `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs`
   (accessed 2026-04-24). File-local evidence for erratum 1.

## Alternatives considered

- **A. Per-entity XLSX export on Appointments via the
  `GetListAsExcelFileAsync` + `GetDownloadTokenAsync` pair (the
  WcabOffice pattern).** Idiomatic ABP Suite scaffold, consistent with
  the two existing NEW export surfaces (WcabOffices, UserExtended), uses
  `MiniExcelLibs` already referenced. Filter bag re-uses existing
  `GetAppointmentsInput` (after minor extension). No new NuGet. Row-level
  tenancy automatic. Effort ~2-3 days. **Chosen.**
- **B. Server-side PDF export via QuestPDF.** Adds NuGet
  `QuestPDF` (MIT Community, Gesco-eligible). `Community.QuestPDF.License`
  attribute set at module startup to acknowledge the tier. Document a
  renderer in `src/.../Application/Appointments/Export/AppointmentPdfRenderer.cs`.
  Effort ~3-5 days incremental over alt A. **Conditional.** Only if
  Adrian's answer to open question Q12 says "PDF required for MVP"; the
  track-10 erratum showed OLD never ships a real server-side PDF, so
  Gesco's clinic workflow may not actually depend on it.
- **C. Generic cross-entity `/api/app/reports/csv-export` endpoint
  mirroring OLD's `CSVExportController`.** Would centralise CSV/PDF/HTML
  in one `IReportsAppService`. **Rejected.** Track 04 line 162 flags
  this as an intentional architectural difference: per-entity targeted
  export is the ABP pattern; fragmenting data across a generic endpoint
  loses the row-level tenant filter, the per-entity DTO shape, and the
  permission-group hierarchy. Multiplies test surface and bypasses
  Mapperly codegen.
- **D. HTML-for-`window.print()` endpoint (OLD's `downloadType == 1`
  path) to let Angular render PDF client-side.** Cheap and matches OLD's
  actual production behaviour. **Conditional.** Recommend deferring.
  Browser print output is inconsistent across Chromium versions, and if
  Adrian accepts alternative A for XLSX plus alternative B (optional)
  for PDF, window-print HTML is redundant and a code-quality liability.
  Re-visit only if per-appointment "Demographics" printout is demanded
  and neither XLSX nor PDF suffice.
- **E. Materialise an `AppointmentRequestReport` sibling table (OLD
  parity for 03-G07).** OLD persists a row per-request as a denormalised
  audit. **Rejected.** Evidence: `AppointmentRequestReportController.cs`
  is a raw REST shell over a generic repo with zero business logic; the
  OLD model duplicates 34 `Appointment` columns plus nested collections.
  In ABP, the canonical pattern is ABP `AbpAuditLogs` plus
  `AppointmentChangeLog` (tracked separately under
  `appointment-change-log-audit` capability). Replicating OLD's
  redundant snapshot would fight both ADR-002 (manual controllers) and
  the audit-log capability's chosen shape.

## Recommended solution for this MVP

Ship per-entity XLSX export on `Appointments` using the WcabOffice
two-endpoint pattern plus a new `Reports` permission group.

WHAT, WHERE, WHICH ABP primitive:

1. **Permission group** --
   `src/.../Application.Contracts/Permissions/CaseEvaluationPermissions.cs`:
   add nested static `Reports { Default, Export }` and register under
   `CaseEvaluationPermissionDefinitionProvider.cs` so it appears in the
   admin permission tree. Per CLAUDE.md permission convention. Closes
   5-G08.
2. **DTOs** --
   `src/.../Application.Contracts/Appointments/AppointmentExcelDto.cs`
   (sheet-row projection; keep to flat columns: ConfirmationNumber,
   PanelNumber, AppointmentDate, Status, Patient FullName, Doctor
   FullName, Location Name, AppointmentType Name, CreationTime), and
   `AppointmentExcelDownloadDto.cs` (mirrors the 8-field
   `GetAppointmentsInput` plus `string DownloadToken`). No new filter
   shape; reuse existing. Extend `GetAppointmentsInput` to add
   `AppointmentStatus?` if `appointment-search-listview` has not already
   done so (cross-check at build time).
3. **AppService methods on `AppointmentsAppService`** --
   `GetListAsExcelFileAsync(AppointmentExcelDownloadDto input)` marked
   `[AllowAnonymous]` (anon required so `<a href>` download works -- the
   token is the CSRF guard), plus `GetDownloadTokenAsync()` marked
   `[Authorize(CaseEvaluationPermissions.Reports.Export)]`. Inject
   `IDistributedCache<AppointmentDownloadTokenCacheItem, string>` (new
   cache-item type in `Appointments/AppointmentDownloadTokenCacheItem.cs`).
   Inner body mirrors `WcabOfficesAppService.cs:94-131` one-for-one:
   validate token from cache, call
   `_appointmentRepository.GetListWithNavigationPropertiesAsync(...)`,
   project to anonymous rows or through a Mapperly
   `MapperBase<AppointmentWithNavigationProperties, AppointmentExcelDto>`
   partial, call `await memoryStream.SaveAsAsync(items)`, return
   `RemoteStreamContent`. Write a corresponding Mapperly partial in
   `CaseEvaluationApplicationMappers.cs` per ADR-001.
4. **Interface** -- add method pair to
   `IAppointmentsAppService.cs`.
5. **Controller** --
   `src/.../HttpApi/Controllers/Appointments/AppointmentController.cs`:
   add `[HttpGet("as-excel-file")]` and `[HttpGet("download-token")]`
   one-line delegates (per ADR-002). Routes become
   `GET /api/app/appointments/as-excel-file?FilterText=...&DownloadToken=...`
   and `GET /api/app/appointments/download-token`.
6. **Audit** -- rely on ABP audit log's automatic capture of the AppService
   call (request + user). Add an explicit
   `Logger.LogInformation("Export executed by {User}, filter={@Input}, rowCount={Count}")`
   at the end of `GetListAsExcelFileAsync` so a Serilog sink has the
   payload without needing to replay audit-log UI. Gate log body to exclude
   raw PHI (`patient lookups return names, not DOB/SSN`; sheet still
   contains names -- HIPAA acceptable given the export permission).
7. **Angular** -- new feature under
   `angular/src/app/appointments/appointment-export/` (do NOT touch
   `proxy/`, which is regenerated by `abp generate-proxy`). Service
   calls auto-generated `appointmentService.getDownloadToken()`, then
   builds `<a>` href with the received token and filter params and
   clicks it. Button placed on the existing `/appointments` list view,
   `*abpPermission="'CaseEvaluation.Reports.Export'"`. Pattern mirrors
   `angular/src/app/wcab-offices/wcab-office/components/wcab-office.component.ts`
   export button.
8. **Migration** -- none. No new schema. Caches are distributed
   (in-memory fallback locally, Redis in prod).
9. **Report search page (UI-11, R-08)** -- deferred. The existing
   `/appointments` list with its filter bar plus an "Export XLSX" button
   IS the NEW-idiom Reports screen in MVP. A second `/report` page is
   redundant given `GetAppointmentsInput` already hosts the filter set
   that OLD's search page exposed. If Q12's MVP answer demands a
   distinct sidebar entry named "Reports", add a sidebar item that
   deep-links to `/appointments` pre-filtered to no rows (a thin
   wrapper), not a second route module.

Do NOT ship:

- Server-side PDF. Defer QuestPDF until Q12 confirms PDF is MVP. OLD
  never actually shipped one (track 10 erratum 1).
- `AppointmentRequestReport` sibling entity. Rejected per alt E above;
  tell Adrian it maps to the `appointment-change-log-audit` capability
  which owns the audit trail concern.
- Generic `/api/app/reports/csv-export`. Rejected per alt C.

## Why this solution beats the alternatives

- Matches ABP Suite's generated pattern exactly (two endpoints, anon
  stream + authed token mint), so future entity exports share the shape
  and future ABP upgrades apply uniformly.
- Zero new NuGet (MiniExcel already in Application). Zero new ADR.
  Zero new migration. Minimal blast radius.
- Row-level `IMultiTenant` on Appointment (ADR-004) auto-scopes the
  export to the caller's tenant; no bespoke WHERE clause, no leak risk.
- Permission group closes 5-G08 cleanly; the same `Reports.Export`
  policy will gate future per-entity exports (Doctors, Patients) once
  business asks for them.
- Deferring server-side PDF drops 2-3 days of QuestPDF plumbing for a
  feature OLD never actually shipped. Re-addable later without schema
  change.

## Effort (sanity-check vs inventory estimate)

Inventory rolls up as `03-G07` 1-2 days + `03-G11` 3-5 days +
`G-API-12` Small-Medium + `G-API-13` Medium-Large + `A8-13` M + `R-08` L
+ `UI-11` Large ~= **Large (7-10 days)** if naively summed. Erratum 1
drops server-side PDF to optional, and the decision to fold UI-11/R-08
into the existing `/appointments` list with a filter-aware export button
(rather than a new `/report` module + AppointmentRequestReport table)
further compresses the work.

Realistic sizing:

- Permission group + DTOs + cache-item type: 0.25 day.
- AppService methods + Mapperly partial + interface + controller: 1 day.
- Angular button + service wire-up (using existing list page filter
  form): 0.75 day.
- Extend `GetAppointmentsInput` + `ApplyFilter` with
  `AppointmentStatus` if not already done under
  `appointment-search-listview`: 0.5 day (or zero if that capability
  lands first).
- Test (xUnit for the AppService, Angular spec for the button gating):
  0.5 day.
- Audit-log verification + Serilog line + permission-tree smoke test:
  0.25 day.

Total: **M, ~3 days.** Matches the brief's inventory-estimate hint
("M-L (3-5 days). With per-entity ABP pattern + erratum-enabled
no-PDF, M (~3 days)"). If Q12 later adds PDF, add +2-3 days for
QuestPDF and an `AppointmentPdfRenderer`.

## Dependencies

- **Blocks**: `dashboard-counters` -- the dashboard card "Pending
  appointments count" and kin benefit from the same filter shape and
  permission group; building Reports first means the dashboard can
  link into a filtered export.
- **Blocked by**: `lookup-data-seeds` -- without seeded States,
  AppointmentTypes, Locations, AppointmentStatuses the filter dropdowns
  in the UI cannot be populated for a demo. Same dropdowns the export
  filter bar will re-use.
- **Blocked by**: `appointment-full-field-snapshot` -- OLD's report
  columns include `CancelledById`, `RejectedById`,
  `CancellationReason`, `RejectionNotes` (see
  `appointment-request-report.ts:17-67`). NEW's `Appointment` entity
  does not yet carry those snapshot fields. If they are missing at
  export time, the XLSX shows blanks where OLD had values. Ideally
  `appointment-full-field-snapshot` lands first; otherwise export
  initially ships with fewer columns.
- **Blocked by**: `appointment-search-listview` -- the single point of
  truth for the filter bag lives there. Co-ordinate on any extension to
  `GetAppointmentsInput` (e.g. add `AppointmentStatus?`) to avoid two
  PRs editing the same DTO.
- **Blocked by open question**:
  `Report search page + PDF/Excel export: required? Which formats? Per-entity (ABP pattern) or generic (OLD pattern)?`
  (verbatim, gap-analysis README line 242). Answer drives whether PDF
  is in MVP (alt B conditional vs omitted) and whether a distinct
  `/report` route is needed (deferred in the recommended shape).

## Risk and rollback

- **Blast radius**: Medium. A bug leaking tenant rows into another
  tenant's XLSX would be a HIPAA incident. Mitigated by
  `IMultiTenant` auto-filter on `Appointment` plus the permission gate
  at the download-token mint step. An XLSX-generation bug (corrupt
  file, OOM on large row counts) affects only the Reports feature;
  other CRUD flows untouched.
- **Rollback**: Revert the commit. No migration to roll back. Clear
  the `IDistributedCache` (if Redis, `FLUSHDB` the ABP prefix; if
  in-memory, restart API Host). Remove the new Permission rows the
  seeder added by running the permission-removal flow in the admin UI,
  or by leaving them orphan (ABP tolerates unreferenced permission
  strings).
- Feature-flag option: wrap `GetListAsExcelFileAsync` body with
  `if (!await _featureChecker.IsEnabledAsync("CaseEvaluation.Reports.Export"))
  throw new BusinessException(...)` so the endpoint can be disabled at
  runtime per tenant. Worth considering if PHI export is rolled out
  gradually.

## Open sub-questions surfaced by research

- **Max row count ceiling.** OLD's `CSVExportController` uses a
  process-local `ConcurrentDictionary` with no row cap. For NEW,
  decide: do we cap the XLSX at 10,000 rows to avoid OOM on large
  tenants, or stream row-by-row via MiniExcel's `SaveAsAsync` (which it
  does). Recommendation: stream; add a warning log if row count >
  50,000.
- **Column selection.** Should the admin be able to pick which columns
  appear (like OLD's CSV client-side column chooser)? Default: fixed
  column set per alt A item 3. Re-evaluate after the clinic reviews
  the first export.
- **Per-appointment "Demographics" printout** (OLD's
  `CSVExportController.Get(appointmentId, downloadType)` at line 73).
  Belongs to a different screen entirely -- the appointment detail
  view with a "Print" button. Currently out of scope for this
  capability; if Q12 brings it in, evaluate under
  `appointment-full-field-snapshot` or a new `appointment-printout`
  capability rather than overloading the reports surface.
- **`AppointmentStatus` filter absence.** Confirm via cross-checking
  `appointment-search-listview` brief: does that capability already
  add `AppointmentStatus?` to `GetAppointmentsInput`? If yes, zero
  extra work here; if no, cost 0.5 day here.
- **`Reports` permission inheritance.** Should `Reports.Default`
  gate only the export action, or also a future standalone `/reports`
  dashboard tile? For MVP, keep `Default` = view-reports-tile,
  `Export` = download. Aligns with existing entity groups.
