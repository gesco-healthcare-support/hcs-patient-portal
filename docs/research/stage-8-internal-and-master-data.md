# Stage 8 Research: Internal-User UIs, Reports, Master Data, IT Admin

Date: 2026-05-04
Branch: feat/replicate-old-app-track-domain
Scope: X1, X2, X3 (internal-user UIs + reports) and M1, M2 (master data + IT Admin).
Status: research-only. No code changes. Future implementation will pick a page from this doc and reuse the citations as a starting point. Cap: ~800 lines.

OLD root: P:\PatientPortalOld
NEW root: W:\patient-portal\replicate-old-app

Convention used below: `<exists>` and `<absent>` are inventory shortcuts; `TODO` means the asset is missing in NEW and is expected to be created. "OLD" = legacy single-tenant Patient Portal. "NEW" = ABP Commercial 10.0.2 / .NET 10 / Angular 20.

---

## 0. Cross-cutting findings (read once, applies to every section)

### 0a. Reports format -- DOCX assertion is incorrect; OLD uses HTML+iTextSharp+ClosedXML

The task statement says "OLD: DOCX export; NEW: PDF". OLD code does NOT generate DOCX for the
two appointment reports; it builds an HTML string server-side and either:
- passes it back to Angular which uses `window.print()` to render PDF in the browser, or
- uses iTextSharp `XMLWorkerHelper.ParseXHtml()` (server-side) to write a `.pdf` file, or
- ClosedXML `XLWorkbook` to write `.xlsx`.

OLD source (full, single file): `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs:1-689`. No DOCX/OpenXml-Word code path exists for these two reports. The Word-OpenXml libraries shipped (DocumentFormat.OpenXml.dll) are unused by the report flows; they are present for other parts of the app.

Implication: NEW PDF requirement is consistent with OLD intent (PDF was the user-visible output). Replication effort is "match data + layout + columns", not "convert DOCX to PDF". Choose a server-side renderer per Section 3 below.

CLAUDE.md still says "OLD generated `.docx` reports; NEW generates PDF" -- that statement is outdated relative to the OLD code. Flag for Adrian; do not re-litigate the NEW PDF decision. The CLAUDE.md note's spirit (immutable output, recipients cannot edit) is preserved by PDF.

### 0b. OLD page guard model

OLD uses a numeric `applicationModuleId` + `rootModuleId` pair on every route to gate access. NEW uses ABP `requiredPolicy` strings (e.g., `CaseEvaluation.Dashboard.Tenant`). Each section below cites the OLD ID for traceability; the NEW permission string belongs in `CaseEvaluationPermissions.cs` per the existing CLAUDE.md ABP convention.

### 0c. OLD list-page pattern (uniform across X2 / users / templates / appointment-types)

Every OLD list page uses the same shape:
- store proc `EXEC spm.sp<Entities> @OrderByColumn, @SortOrder, @PageIndex, @RowCount, @UserId[, optional filters]` (`P:\PatientPortalOld\PatientAppointment.Domain\TemplateManagementModule\TemplateDomain.cs:25-36` is the canonical example),
- response is JSON-as-string from `StoreProcSearchViewModel.Result`,
- `<rx-table>` Angular component with column-level sort, server-side paging, free-text search, and an Advanced Search accordion.

NEW must replicate the columns and filters but should swap the storage layer to ABP `IRepository<T>.GetQueryableAsync()` + LINQ paging. Do NOT port the SPs; recreate the equivalent queries in C#.

### 0d. OLD master-data delete guard -- universal "CandDelete" check

Every master-data domain class (AppointmentType, Location, WcabOffice, Doctor, SystemParameter, Template, CustomField) implements `DeleteValidation(int id)` which calls `ApplicationUtility.CandDelete<T>(id, true)`. This walks every FK reference to the row and returns true (validation FAILED) if any child row exists. Confirmed in:
- `AppointmentTypeDomain.cs:72-86`
- `LocationDomain.cs:80-90`
- `WcabOfficeDomain.cs:75-87`
- `DoctorDomain.cs:60-76`
- `SystemParameterDomain.cs:57-71`
- `TemplateDomain.cs:77-93` (AND soft-delete via `StatusId = Status.Delete`).

So master-data CRUD in NEW must enforce: cannot delete an entity that is referenced by appointments or any other table. Pattern: domain service method `DeleteValidationAsync(id)` returns `IReadOnlyList<string>`; AppService throws `BusinessException` if non-empty.

### 0e. NEW Identity branch carries Phase 4/6/8 AppServices not in working branch

`feat/replicate-old-app-track-identity` already has scaffolds for: `NotificationTemplatesAppService`, `Patients`, `ExternalSignups`, `AppointmentBodyParts`, `AppointmentChangeRequests`, etc. Working branch (`feat/replicate-old-app-track-domain`) does NOT. The plan should assume merge-from-identity (G2 in the open task list) before any of X/M work begins. The audit below treats those as "exists on identity branch" rather than "absent".

---

## 1. X1: Internal-user dashboard

### OLD source (paths)

- Controller: `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\` (DashboardController -- single POST endpoint `api/Dashboard/post`).
- Frontend: `P:\PatientPortalOld\patientappointment-portal\src\app\components\dashboard\` (5 files: component .html / .ts, module, routing, service).
- Layout: `dashboard\dashboard.component.html:1-172` (~172 visible lines; rest is commented-out scaffolding).
- Logic: `dashboard\dashboard.component.ts:1-80+` (counter binding + `counterClicked()` router navigation).

### OLD UX

12 counter cards in a 4-column responsive Bootstrap grid (`col-sm-6 col-xl-3`):

Row 1 (appointment-status counters):
1. Pending Appointment (lnr-cart, success) -> `routerLink="/appointment-pending-request"`
2. Approved Appointment (lnr-earth, info) -> `counterClicked('ApprovedAppointment')` -> `/appointment-search?appointmentStatusId=Approved`
3. Rejected Appointment (lnr-gift, danger) -> `Rejected`
4. Cancelled Appointment (lnr-users, warning) -> `Cancelled`

Row 2 (more status counters):
5. Rescheduled Appointment (success)
6. Checked-In Appointment (info)
7. Checked-Out Appointment (danger)
8. Billed Appointment (warning)

Row 3 (user-type counters; staff supervisor + IT admin only):
9. Patient
10. Claim Examiner (note: stored as `Adjuster` field in dashboard payload)
11. Applicant Attorney
12. Defense Attorney

Each card shows: icon (`lnr-*` Linearicons), small muted label, large counter number. Click -> filtered list.

### OLD permissions

| Card group | Clinic Staff | Staff Supervisor | IT Admin | Doctor | External roles |
|---|---|---|---|---|---|
| Status counters (1-8) | yes | yes | yes | yes (own appts) | no |
| User-count counters (9-12) | no | yes | yes | no | no |

OLD source: role filtering happens server-side in the `POST /api/Dashboard/post` body using `UserClaim.UserId` + role; client trusts the response shape.

### NEW current backend state

- `<exists>` `src/HealthcareSupport.CaseEvaluation.Application/Dashboards/DashboardAppService.cs` (already implemented, 5 live counters per design doc + 8 placeholders).
- `<exists>` `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Dashboards/` (`DashboardCountersDto`, `IDashboardAppService`).
- `<exists>` Permissions: `CaseEvaluation.Dashboard.Host` (ABP analytics) and `CaseEvaluation.Dashboard.Tenant` (counters) -- already in `route.provider.ts:21-26`.

### NEW frontend state

- `<exists>` `angular/src/app/dashboard/` (per design doc: tenant-dashboard component implemented with 5 live + 8 placeholder cards).
- Route registered at `/dashboard` with `requiredPolicy: 'CaseEvaluation.Dashboard.Host || CaseEvaluation.Dashboard.Tenant'` (`angular/src/app/route.provider.ts:21-26`).

### Implementation plan (high-level)

1. Backend: extend `DashboardAppService.GetCountersAsync()` to return all 12 counters (8 status + 4 user-type), gated on caller's role via `ICurrentUser`. Add `[Authorize(CaseEvaluation.Dashboard.Tenant)]`.
2. Frontend: replace placeholder cards with real values from updated DTO. Wire `(click)` handlers to navigate to `/appointments?status=<Status>` (Stage X2 feature).
3. Apply OLD design tokens (see `docs/design/_design-tokens.md`) -- LeptonX card component already styled close enough; just colour the icon.

### Acceptance criteria

- All 12 OLD counters reproduce their OLD click destinations (filtered list pages).
- Clinic Staff sees rows 1-2 only; Staff Supervisor + IT Admin see all three rows.
- Counter values match a known seed dataset (write a single integration test).

### Gotchas

- "Adjuster" in OLD payload = "Claim Examiner" everywhere user-visible. Honor the user-visible label per project memory (`role-model` note).
- Dashboard route requires policy OR -- ensure both Host and Tenant Dashboard permissions seeded.
- Doctor dashboard variant: in OLD, doctors landed on the same dashboard but counters were filtered to their own appointments. NEW design doc renames Doctor's view to Tenant Dashboard with same scoping. Verify scoping query.

---

## 2. X2: Internal-user view-all-appointments list

### OLD source

- Frontend folder: `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\` (10 sub-folders: add, detail, domain, edit, info, list, my-appointments, search, view + 5 module/routing/service files).
- The "view-all" list is `appointments\search\` (advanced filter panel + table) and `appointments\list\` (simple list).
- Backend search controller: `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsSearchController.cs`.
- Routes: `/appointment-search`, `/appointment-pending-request`, `/my-appointments`.

### OLD UX

Three views of the same appointments dataset, gated by role:
- `/appointment-pending-request` -- Clinic Staff inbox: filter pinned to "Pending Approval".
- `/appointment-search` -- general search with filter accordion (Appt Type, Location, Status, Patient Name, From/To Date).
- `/my-appointments` -- Doctor view: own appointments only.

Table columns (server-side paged via `<rx-table>`):
- Confirmation# | Appt Type | Location | Appt Date/Time | Status | Patient Name | DOB | Email | Phone | SSN | Action.

### OLD permissions

| Route | Clinic Staff | Staff Supervisor | IT Admin | Doctor | External |
|---|---|---|---|---|---|
| `/appointment-search` | yes | yes | yes | no | no |
| `/appointment-pending-request` | yes | yes | yes | no | no |
| `/my-appointments` | no | no | no | yes | no (own scoped to PatientAttorney via separate view -- not this page) |

OLD applicationModuleId for appointments: 13.

### NEW current backend state

- `<exists>` `Application/Appointments/` -- `AppointmentsAppService` on identity branch (and probably referenced from working branch via DI).
- The AppService should already return `PagedResultDto<AppointmentDto>`; just confirm the `Get<Entities>Input` includes filters: `AppointmentTypeId`, `LocationId`, `AppointmentStatusId`, `PatientName`, `FromDate`, `ToDate`, `DoctorId` (Doctor scope).

### NEW frontend state

- `<exists>` `angular/src/app/appointments/` -- folder exists on working branch.
- TODO: confirm an internal-user-facing list with the 6 filters above. If the existing component is the external-user "my appointments" view, a new internal-user list is needed.

### Implementation plan

1. Backend: extend `GetAppointmentsInput` to take `Status`, `FromDate`, `ToDate`, `DoctorId`, `LocationId`, `AppointmentTypeId`, `PatientName`. Filter via LINQ in `AppointmentsAppService`. All filters optional.
2. Frontend: build a single `appointments-list-internal.component` with the OLD 11 columns (mask SSN as `***-**-NNNN` per design doc decision -- OLD shows raw SSN; NEW masks). Use an LeptonX `abp-table` or `mat-table` + `mat-paginator`.
3. Apply role-based scope:
   - Doctor -> server filter `AppointmentDoctorId = currentDoctor.Id`
   - Clinic Staff / Supervisor / IT Admin -> no implicit scope (see all)
4. Hook query params: `?status=Approved&fromDate=...` from dashboard counter clicks.

### Acceptance criteria

- All 6 filters work and combine (AND).
- SSN masking on by default; "reveal" toggle requires a user action.
- Default sort: confirmation# DESC.
- Pending-request view = pre-filter `Status = PendingApproval`.

### Gotchas

- 13 appointment statuses (per `AppointmentStatusTypeEnums`). Status filter must enumerate all.
- Doctor scope must use the doctor entity, not the user entity (Doctor is a non-user entity per project memory).
- "Patient Name" is a free-text partial match (case-insensitive); use `EF.Functions.Like(...)`.

---

## 3. X3: Internal-user reports (PDF + Excel)

OLD ships TWO reports:

### Report A: Appointment list export (filtered)

Aliased "Appointment Request Report" in OLD.

- OLD page: `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-request-report\search\appointment-request-report-search.component.html` (150 lines) + `.ts` (167 lines).
- OLD route: `/appointment-request-report`. applicationModuleId 13, rootModuleId 33.
- OLD backend controller: `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentRequestReportController.cs:1-53` (CRUD over `AppointmentRequestReport` view) + `AppointmentRequestReportSearchController.cs` for paged search.
- Export endpoint: `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs:42-70` (`GET api/CsvExport/{key}?fileName=&downloadType=`). `downloadType=1` returns HTML for PDF (browser-print); any other value writes `.xlsx` to disk and streams.
- Filters (validated together; see design doc Section 4): Appointment Type, Location, Status, Patient Name, From Date, To Date.

### Report B: Patient Demographics PDF (per appointment)

- OLD endpoint: `CSVExportController.Get(int appointmentId, int downloadType):73-106` -- builds patient demographics HTML and returns to client.
- OLD HTML composition: `GetPatientDataHtml(Appointment):302-681` -- includes:
  - Appointment Details (Type, Date, Status, Panel #, Time, Request Date, Location, Confirmation#, Responsible Person)
  - Patient Details (Name, DOB, SSN, Address, Email, Phone, Cell, Language, Interpreter)
  - Employer Details
  - For each Injury Detail: Injury Details, Insurance Details, Claim Examiner block
  - Applicant Attorney Details
  - Defense Attorney Details
  - Appointment Accessors (if any)
  - Custom Form Details (looped over appointment-level CustomFieldsValues)
- The HTML uses inline `<style>` with print media queries; the client invokes `window.print()`.

### OLD permissions

| Report | Clinic Staff | Staff Supervisor | IT Admin | Doctor | External |
|---|---|---|---|---|---|
| Appointment list export | yes | yes | yes | no | no |
| Patient Demographics (per appt) | yes | yes | yes | yes (own appts) | no |

### NEW current backend state

- `<absent>` No `Reports/` AppService folder. Search confirms zero `*Report*.cs` under `src/`.
- `<absent>` No PDF rendering library yet referenced.

### NEW frontend state

- `<absent>` No `angular/src/app/reports/` folder.

### PDF rendering -- candidates and recommendation

| Lib | License | Strengths | Weaknesses |
|---|---|---|---|
| QuestPDF | Community MIT for sub-$1M revenue; commercial otherwise | Fluent C# API, fast, no native deps, ABP-friendly | Re-license cost above $1M revenue threshold |
| PdfSharpCore / PdfSharp | MIT | Pure C#, lightweight | HTML-to-PDF requires extra layer (e.g., HtmlAgilityPack); manual layout |
| iText 7 .NET | AGPL or commercial | Closest match to OLD's iTextSharp output | AGPL viral; commercial license expensive |
| IronPDF | Commercial | HTML-to-PDF works out of the box | Per-deployment fee; native deps |
| Playwright + headless Chromium | Apache 2.0 | Pixel-perfect HTML-to-PDF | Heavy runtime; container size |

Recommendation (HIGH confidence based on Adrian's prior tooling preferences and Gesco non-public revenue): **QuestPDF** for the appointment-list export and a minimalist QuestPDF document for Patient Demographics. Reason: avoids HTML rendering entirely; Gesco is well below the $1M revenue threshold; ABP integrates cleanly via DI. If a HTML-to-PDF round-trip is preferred (faster to port the OLD layout), Playwright + headless Chromium is a fallback. Surface this to Adrian before implementation.

For Excel: **ClosedXML** (matches OLD's library; MIT) or **EPPlus** (LGPL). Recommend ClosedXML.

### Implementation plan

1. Add NuGet refs: `QuestPDF` (Application project) and `ClosedXML` (Application project). License config in module init.
2. Create `Application/Reports/AppointmentReportAppService.cs` with three endpoints:
   - `GetAppointmentListAsync(GetAppointmentListReportInput input)` -> `PagedResultDto<AppointmentReportRowDto>` (table data, pre-export).
   - `ExportAppointmentListPdfAsync(GetAppointmentListReportInput input)` -> `IRemoteStreamContent` (PDF).
   - `ExportAppointmentListExcelAsync(...)` -> `IRemoteStreamContent` (XLSX).
   - `GetPatientDemographicsPdfAsync(Guid appointmentId)` -> `IRemoteStreamContent`.
3. DTOs in `Application.Contracts/Reports/`. New permissions: `CaseEvaluation.Reports.AppointmentList`, `CaseEvaluation.Reports.PatientDemographics`.
4. Frontend `angular/src/app/reports/` with one component per report. Filter validation per design doc Section 4 (4 rules).

### Acceptance criteria

- Filter validation: empty -> "Please enter a search value"; missing FromDate / ToDate -> appropriate error; FromDate > ToDate -> "To Date should be greater than From Date".
- Excel export columns identical to UI table columns.
- PDF export contains the same rows + columns; landscape A4.
- Patient Demographics PDF includes all 8 sections (Appointment, Patient, Employer, Injury x N, Insurance x N, Claim Examiner x N, Applicant Attorney, Defense Attorney, Accessors if present, Custom Fields if present).
- Doctor accessing demographics PDF can only access own appointments.

### Gotchas

- Custom Fields values must be looked up by `customField.CustomFieldId` -> `CustomField.FieldLabel` (OLD does this in `GetPatientDataHtml`). NEW must join via `IRepository<CustomField>` not store the label inline.
- Multiple injury details -> repeat the Injury / Insurance / Claim Examiner blocks once per injury.
- OLD shows raw SSN; NEW must mask per design doc decision (`***-**-NNNN`); reveal toggle is UI-only and does not appear in the exported PDF (PDF always masked).
- PHI: every report writes patient PII; ensure logger redaction (`hipaa-data` rule) covers report-export logs.

---

## 4. M1: Master-data CRUD

OLD master-data CRUD touches 7 entities. Doctor is treated as master data (NOT a user role) per project memory.

| Entity | OLD UI folder | OLD controller | OLD domain | NEW backend AppService | NEW frontend folder |
|---|---|---|---|---|---|
| Doctor | `doctor-management\doctors\` | `DoctorsController.cs` | `DoctorDomain.cs` | `<exists>` `Application/Doctors/DoctorsAppService.cs` + `DoctorTenantAppService.cs` | `<exists>` `angular/src/app/doctors/` |
| Location | `doctor-management\locations\` | `LocationsController.cs` | `LocationDomain.cs` | `<exists>` `Application/Locations/LocationsAppService.cs` | `<exists>` `angular/src/app/locations/` |
| Appointment Type | `doctor-management\appointment-types\` | `AppointmentTypesController.cs` | `AppointmentTypeDomain.cs` | `<exists>` `Application/AppointmentTypes/AppointmentTypesAppService.cs` | `<exists>` `angular/src/app/appointment-types/` |
| WCAB Office | `doctor-management\` (no dedicated subfolder; managed via lookups) | `WcabOfficesController.cs` | `WcabOfficeDomain.cs` | `<exists>` `Application/WcabOffices/WcabOfficesAppService.cs` | `<exists>` `angular/src/app/wcab-offices/` |
| State | not separately CRUDable in OLD UI -- seeded data | -- | -- | `<exists>` `Application/States/StatesAppService.cs` | `<exists>` `angular/src/app/states/` |
| Doctor Preferred Location | `doctor-management\doctor-preferred-locations\` | `DoctorPreferredLocationsController.cs` | `DoctorPreferredLocationDomain.cs` | `<absent>` AppService | `<absent>` |
| Doctor Availability | `doctor-management\doctors-availabilities\` | `DoctorsAvailabilitiesController.cs` | `DoctorsAvailabilityDomain.cs` | `<exists>` `Application/DoctorAvailabilities/` (identity branch) | `<exists>` `angular/src/app/doctor-availabilities/` |
| Doctor Appointment Type mapping | `doctor-management\doctors-appointment-types\` | `DoctorsAppointmentTypesController.cs` | `DoctorsAppointmentTypeDomain.cs` | `<absent>` AppService | `<absent>` |

### OLD UX (uniform)

Each entity has: list (search + paged) + add (modal or page) + edit (page or modal) + delete (with `CandDelete` guard). Locations specifically: delete action commented out in OLD list (per master-data-crud-design doc Section: line 125).

### OLD permissions

All master-data CRUD: Staff Supervisor + IT Admin. Clinic Staff: read-only. External users: no access.

### NEW current backend state

Most AppServices exist. Verify: `IDoctorPreferredLocationAppService` and `IDoctorsAppointmentTypeAppService` are missing on working branch (no folder). Both exist on identity branch under `Application/DoctorPreferredLocations/`. Confirm before scaffolding.

### NEW frontend state

Most folders exist. `staff-supervisor-doctor-management-design.md` describes the "Doctor Management" composite page that nests the 4 doctor-related entities into a single tabbed view. Plan that as a single Angular standalone component routing into 4 child tabs.

### Implementation plan

1. Backend: for each of the 7 entities, ensure `*AppService` has `[RemoteService(IsEnabled = false)]`, paired manual controller, and `DeleteValidationAsync` that walks FK references (mirrors OLD's `CandDelete`).
2. Frontend: 6 list pages with filter + add/edit modal. Use ABP CRUD page generator (`abp generate-proxy` then customize) where possible; escape hatch is a hand-written component for tabbed Doctor Management.
3. Locations: per OLD parity, hide Delete action OR enforce "no delete if appointments reference this location" (recommended -- OLD's commented-out behavior was self-protective; NEW should enforce same intent via guard not by hiding).
4. States: read-only or seeded list; no UI add/edit unless an explicit need arises.

### Acceptance criteria

- Cannot delete an AppointmentType / Location / WcabOffice / Doctor that is referenced by at least one Appointment row -> server returns 422 with localized message "Cannot be deleted because it is in use".
- Locations list does not expose Delete (or exposes it but the action is enforced server-side and returns the guard error).
- Master-data list pages support: free-text search across primary columns, pagination, server-side sort.

### Gotchas

- "Doctor is non-user entity" -- when porting `DoctorsAppService`, do NOT create a Volo `IdentityUser` for doctors; treat as ABP `IEntity`.
- WCAB office -> appointment via `AppointmentInjuryDetail.WcabOfficeId`. Delete guard must check that table.
- AppointmentType has a 1:N relationship to AppointmentTypeFieldConfigs (custom-field config). Deleting AppointmentType should also block-or-cascade those configs (OLD soft-delete cascade not yet verified -- mark as PARITY-FLAG when implementing).

---

## 5. M2: IT Admin pages

5 distinct admin pages. All gated to IT Admin (and Staff Supervisor for some) per OLD applicationModuleId.

### 5a. System Parameters

- **OLD source** -- list/edit page: `P:\PatientPortalOld\patientappointment-portal\src\app\components\system-parameter\system-parameters\` (domain, edit, module, routing, service). No add/delete -- system parameters are seeded; only edit is allowed.
- **OLD controller**: `Api\SystemParameter\SystemParametersController.cs`. Domain: `SystemParameterDomain.cs:1-80+`.
- **OLD UX**: simple list -> click row -> edit modal updating `vSystemParameter.ParameterValue`. Examples: appointment slot duration, max booking lead-time, etc.
- **OLD permissions**: IT Admin only. applicationModuleId 27.
- **NEW backend**: `<exists>` `Application/SystemParameters/SystemParametersAppService.cs` (working branch) + identity branch confirms.
- **NEW frontend**: `<absent>`. No `angular/src/app/system-parameters/` folder on working branch.
- **Plan**: single list page with inline-edit (no add/delete UI). Reuse design doc `it-admin-system-parameters-design.md`. Enforce update-only contract on AppService (`Add` and `Delete` should not exist or be `[RemoteService(IsEnabled = false)]`).
- **Acceptance**: edit a parameter -> page reloads with new value; old value preserved via change-log.
- **Gotchas**: changing slot-duration must not break in-flight booking calculations; emit a domain event so the slot-generator can rehydrate cache.

### 5b. Notification Templates

- **OLD source** -- `P:\PatientPortalOld\patientappointment-portal\src\app\components\template-management\templates\` (full add + delete + edit + list + domain + service).
- **OLD controller**: `Api\TemplateManagement\TemplatesController.cs`. Domain: `TemplateDomain.cs:1-100+`.
- **OLD UX**: list (server-side paged via SP) -> add new template OR edit existing OR delete (soft-delete via `StatusId = Status.Delete`). Each template has: TemplateCode, TemplateTypeId (Email/SMS), Subject, Body, Status. Validation: `(TemplateCode, TemplateTypeId)` must be unique among Active rows.
- **OLD permissions**: IT Admin only. applicationModuleId 26.
- **Can users create new templates?** YES. OLD `TemplateDomain.Add()` accepts arbitrary new template + `AddValidation` only checks duplicate code. So OLD allows free creation, not just edit-existing. NEW design doc (`it-admin-notification-templates-design.md`) caps to edit-existing-only -- this is a deliberate NEW change to prevent template proliferation. Confirm with Adrian before locking the policy.
- **NEW backend**: `<exists>` on identity branch: `Application/NotificationTemplates/NotificationTemplatesAppService.cs`. `<absent>` on working branch.
- **NEW domain**: `<exists>` `Domain/NotificationTemplates/NotificationTemplate.cs` + `NotificationTemplateDataSeedContributor.cs`. Codes already seeded.
- **NEW frontend**: `<absent>`.
- **Plan**: edit-only UI (per NEW design choice) -> list 12-ish seeded templates -> click row -> edit modal with subject/body fields + variable picker (token list). Create not exposed in UI; add programmatically only via seed data.
- **Acceptance**: edit template -> next email send uses new copy; variable substitution still works; delete/create disabled in UI.
- **Gotchas**: template code typos already locked per `docs/parity/_parity-flags.md` and CLAUDE.md "Notification template codes" reference. Do not re-litigate.

### 5c. Package Details (Document Packages -- pre-built upload bundles)

- **OLD source** -- `P:\PatientPortalOld\patientappointment-portal\src\app\components\document-management\package-details\` (add/edit/list/domain) + sibling `documents\` and `document-packages\` folders.
- **OLD controllers**: `Api\DocumentManagement\PackageDetailsController.cs`, `DocumentsController.cs`, `DocumentPackagesController.cs`.
- **OLD UX**: a Document Package is a named bundle of required documents per AppointmentType (e.g., AME PreEval Package = WC1 + WC2 + DWC-AD-10133.32). Page: list packages -> add/edit/delete -> for each, manage child documents.
- **OLD permissions**: IT Admin. applicationModuleId 25.
- **NEW backend**: `<exists>` `Application/PackageDetails/PackageDetailsAppService.cs`. Mapping in `CaseEvaluationApplicationMappers.PackageDetails.cs`.
- **NEW frontend**: `<absent>`. No `angular/src/app/package-details/` folder.
- **Plan**: single list + add/edit modal. Modal contains a child grid of document slots (each slot = required doc type). Reuse design doc `it-admin-package-details-design.md`.
- **Acceptance**: create a new package -> assign 3 document types -> assign package to AppointmentType -> next external-user booking sees those 3 doc slots.
- **Gotchas**: package edits should not affect already-issued packets to in-flight appointments (snapshot at appointment-create time); confirm against OLD behavior before implementation.

### 5d. Custom Fields

- **OLD source** -- `P:\PatientPortalOld\patientappointment-portal\src\app\components\custom-field\custom-fields\` (add/edit/list/domain).
- **OLD controller**: `Api\CustomField\CustomFieldsController.cs`. Domain: `CustomFieldDomain.cs:1-80+`.
- **OLD UX**: list active+inactive custom fields -> add new -> edit -> soft-delete (`StatusId`).
- **OLD permissions**: IT Admin. applicationModuleId 24.
- **Max 10 enforcement**: `CustomFieldDomain.AddValidation:38-43` -- counts `StatusId == Active` rows; throws `ValidationFailedCode.Max10CustomFields` at exactly 10. **CONFIRMED** -- OLD enforces a hard cap of 10 globally (across all AppointmentTypes). The NEW design doc says "max 10 per AppointmentType" -- this is a deliberate scope refinement (NEW per-type config; OLD global). Surface to Adrian: keep NEW per-type, or revert to global cap to match OLD.
- **NEW backend**: `<exists>` `Application/CustomFields/CustomFieldsAppService.cs`. Per-AppointmentType linkage via `Domain/AppointmentTypeFieldConfigs/`.
- **NEW frontend**: `<absent>`. (`angular/src/app/custom-field/` does not exist.)
- **Plan**: list page + add/edit modal. Field types: NEW must support the OLD 7 types (Text / Numeric / Email / Phone / Date / Dropdown / Checkbox -- per task G3). Confirm against `Domain.Shared/CustomFields/CustomFieldTypeConsts.cs`.
- **Acceptance**: create custom field -> attach to AppointmentType -> next booking renders the field; max-10 cap surfaces as validation error with localized message.
- **Gotchas**: per project memory, NEW currently supports only a subset of types (G3 task). Block this UI work behind G3.

### 5e. User Management (internal + external)

- **OLD source** -- `P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\` (add/edit/delete/list/view + domain).
- **OLD controller**: `Api\User\UsersController.cs`. Domain: `UserDomain.cs:1-120+`.
- **OLD UX**: list users -> add (single popup handles internal + external) -> edit -> soft-delete.
- **OLD password flow**: `UserDomain.Add():95-` branches on `UserType`:
  - Internal user (`UserType.InternalUser`): calls `AddInternalUser()` -- generates a password (suspect; confirm in full file), sends welcome email via `SendMail`, sets `IsVerified = false` and `VerificationCode = Guid.NewGuid()`.
  - External user: requires `UserPassword` + `ConfirmPassword` from form; emails verification link.
- **OLD permissions**: IT Admin only for the `/users` page. Internal user creation: assigns one of {Clinic Staff, Staff Supervisor, IT Admin}. External user creation (via form, also via `/users` add): assigns one of {Patient Attorney, Defense Attorney, ...}.
- **NEW model**: per design doc `it-admin-user-management-design.md`, NEW delegates user management to ABP LeptonX Identity at `/identity/users`. ABP provides list/add/edit/lockout/soft-delete out of the box. The ONE custom behavior to add: internal-user creation with auto-generated password + welcome email -- ABP does not do this natively.
- **NEW backend**: `<exists>` `Application/Users/UserExtendedAppService.cs`. Identity branch may have `InternalUsersAppService`.
- **NEW frontend**: ABP Identity module is auto-routed at `/identity/users`. Custom AppService called from a custom modal action ("Create Internal User") layered on top.
- **Plan**:
  1. Add a side-nav "Users" item routing to `/identity/users` (ABP renders).
  2. Layer a custom "Create Internal User" button -> calls custom AppService -> generates random password (Identity policy compliant) -> creates IdentityUser -> assigns role -> emails welcome with reset link.
  3. External user creation already covered by the existing self-registration flow.
- **Acceptance**: IT Admin clicks "Create Internal User" -> selects role -> enters name + email -> user receives email with one-time login link.
- **Gotchas**:
  - "Claim Examiner" is metadata not a role per project memory -- never offer it as a role option in the IT Admin user-creation form.
  - 4 external + 3 internal roles only; do not invent additional roles.
  - Welcome email needs a NotificationTemplate with code `WelcomeInternalUser` (verify seeded list).

---

## 6. Other OLD UI areas not in scope but flagged for completeness

| OLD folder | One-line description | Disposition |
|---|---|---|
| `appointment-change-log` | Read-only audit log per appointment | Covered by Stage V1 (frontend) + existing audit doc |
| `note` | Internal-staff notes attached to appointments | Covered by Stage A1 / appointment-notes design |
| `appointment-request` | The big booking flow (10 sub-folders) | Covered by Stages B/A/C/D |
| `document` | Per-appointment uploaded docs | Covered by Stage D |
| `document-management` | IT Admin document library + packages (overlaps M2 5c) | Covered by M2 |
| `home` | Public landing page | Covered by external-user UI stages |
| `login` / `unauthorized` / `not-found` | Auth shell | Replaced by ABP AuthServer Razor pages |
| `term-and-condition` | Static page | Covered by terms-and-conditions design |
| `start` | Empty bootstrap shell | No port needed |
| `user-query` | Submit-query feature for external users | Covered by external-user-submit-query design |
| `shared` | Shared Angular components (rx-table, etc.) | Replace with LeptonX equivalents per Adrian |

---

## 7. Files cited (full path index, for fast re-traversal)

OLD:
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs:1-689` (reports, master file)
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentRequestReportController.cs:1-53`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentRequestReportSearchController.cs`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsSearchController.cs`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\` (DashboardController -- file not enumerated above, exists)
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\SystemParameter\SystemParametersController.cs`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\TemplateManagement\TemplatesController.cs`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\CustomField\CustomFieldsController.cs`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\User\UsersController.cs`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\DoctorManagement\` (7 files)
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\DocumentManagement\PackageDetailsController.cs`
- `P:\PatientPortalOld\PatientAppointment.Domain\SystemParameterModule\SystemParameterDomain.cs:1-80`
- `P:\PatientPortalOld\PatientAppointment.Domain\TemplateManagementModule\TemplateDomain.cs:1-100`
- `P:\PatientPortalOld\PatientAppointment.Domain\CustomFieldModule\CustomFieldDomain.cs:1-80`
- `P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs:1-120`
- `P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\` (7 domain files)
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\dashboard\dashboard.component.html:1-172`
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\dashboard\dashboard.component.ts:1-80+`
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-request-report\` (7 files)

NEW (working branch, unless noted):
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Dashboards\DashboardAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\SystemParameters\SystemParametersAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\CustomFields\CustomFieldsAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\PackageDetails\PackageDetailsAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Doctors\DoctorsAppService.cs` + `DoctorTenantAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Locations\LocationsAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\AppointmentTypes\AppointmentTypesAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\WcabOffices\WcabOfficesAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\States\StatesAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Users\UserExtendedAppService.cs`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Notifications\` (renderer + dispatcher; no AppService on working branch)
- (identity branch) `src\HealthcareSupport.CaseEvaluation.Application\NotificationTemplates\NotificationTemplatesAppService.cs`
- `W:\patient-portal\replicate-old-app\angular\src\app\dashboard\` (exists)
- `W:\patient-portal\replicate-old-app\angular\src\app\appointments\` (exists; verify internal-user variant)
- `W:\patient-portal\replicate-old-app\angular\src\app\route.provider.ts:1-30`

NEW absent (TODO):
- `src\HealthcareSupport.CaseEvaluation.Application\Reports\` (no folder)
- `angular\src\app\reports\` (no folder)
- `angular\src\app\system-parameters\` (no folder)
- `angular\src\app\notification-templates\` (no folder)
- `angular\src\app\package-details\` (no folder)
- `angular\src\app\custom-fields\` (no folder; design name; a `custom-field/` may or may not exist on identity branch -- check before creating)
- `angular\src\app\user-management\` (delegated to `/identity/users` -- no custom folder needed)

---

## 8. Open questions for Adrian (block implementation until answered)

1. PDF library choice: QuestPDF (recommended) vs Playwright headless Chromium vs other? Need a pick before X3 starts.
2. Notification Templates: keep edit-existing-only (per design doc) or allow create (per OLD)?
3. Custom Fields cap: keep "max 10 per AppointmentType" (NEW design) or revert to OLD's "max 10 global"?
4. Locations delete: enforce server-side guard (recommended) or hide button (OLD parity)?
5. SSN masking: confirm masked-by-default + reveal-toggle (per design doc); OLD shows raw.

---

End of stage-8 research.
