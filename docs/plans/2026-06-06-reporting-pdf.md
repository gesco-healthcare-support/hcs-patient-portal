---
feature: reporting-pdf
date: 2026-06-06
status: in-progress
base-branch: feat/replicate-old-app
related-issues: []
parity-records: [G-08-01, G-08-03, G-08-04]
---

## Goal

Replicate the OLD app's Reports area on the new stack: an internal-staff
Appointment Request Report grid plus two immutable PDFs (the report table and a
per-appointment demographics sheet), matching OLD's columns/filters/sort/role
access exactly, with PHI masked at the server boundary.

## Context

- Group M, the first Phase 4 parity slice (`docs/parity-research/SUMMARY.md`).
  Binding records: `G-08-01` (grid), `G-08-03` (report PDF), `G-08-04`
  (demographics PDF). `G-08-02` (xlsx) is excluded.
- The whole reporting area is greenfield in NEW (verified: no report
  AppService/controller/permission/route/feature). The build is cheap because
  the query, the nav-DTO graph, SSN masking, and the per-appointment read guard
  already exist.
- Mission override (root CLAUDE.md): OLD's HTML-print/DOCX output becomes a true
  PDF; report business logic must still match OLD.
- Decisions locked with Adrian (2026-06-06): QuestPDF in-process; PHI = SSN
  last-4 + DOB year-only, Name/Email/Phone shown in full; ship as 3 sequenced
  PRs (grid -> report PDF -> demographics PDF).
- Branch gate: `feat/reporting-pdf` branches off `feat/replicate-old-app` ONLY
  after Adrian merges PR #297 and the local base is fast-forwarded. Build phase
  confirms this before any code.

## Parity contract (verified from OLD source)

- Grid columns (order): Confirmation No (links to appointment) | Appointment
  Type | Location Name | Appointment Date Time | Status | Patient Name (PHI) |
  Date Of Birth (PHI) | Email (PHI) | Phone Number (PHI) | Social Security
  Number (PHI).
- Quick search (free text) + 5 advanced filters: Appointment Type, Location,
  Status, Patient Name, Date range (From/To). `doctorName` is dead -> omitted.
- Default sort: `RequestConfirmationNumber` DESC.
- Validation: at least one filter required; if dates given, From <= To and
  both-or-neither.
- Role access: Clinic Staff, Staff Supervisor, IT Admin only (verified in OLD
  `access-permission.service.ts`); all external roles excluded.
- Demographics sheet sections: Appointment Details | Patient Details (DOB+SSN) |
  Employer | per-injury {Injury / Insurance / Claim Examiner + Body Parts} |
  Applicant Attorney | Defense Attorney | Accessors (Edit/View rights) | Custom
  Form fields.

## Approach

Chosen:

- **Reuse the existing appointment query.** `IAppointmentRepository`
  `GetListWithNavigationPropertiesAsync` / `GetCountAsync` already filter by
  FilterText + type + location + status + date range and page/sort. The report
  AppService composes a masked read-only projection over it; no change to the
  appointments feature.
- **Mask PHI in C# at the Application boundary**, in one pure redactor reused by
  grid and PDF (the Angular `ssn-mask.pipe` does not reach the server-side PDF).
  SSN -> `SsnVisibility.MaskToLast4` (`***-**-1234`); DOB -> new
  `DobVisibility.ToYearOnly` (`1985`); Name/Email/Phone -> verbatim. Full SSN
  only via the existing audited `Patients.RevealSsn` gate, never in the
  report/PDF.
- **QuestPDF renders in `Application`.** QuestPDF is referenced only in
  `Domain.csproj`; add the package reference to `Application.csproj` and render
  from the already-masked Application DTOs. This avoids a Domain->Contracts
  dependency inversion and duplicate render models. The `License = Community`
  setting in `CaseEvaluationDomainModule` is a process-global static, so it
  applies to renders in any layer. (The packet pipeline stays in Domain;
  reporting PDFs are synchronous AppService flows, so they live with the DTOs.)
- **Demographics PDF is internal-staff-only.** Gate it on
  `CaseEvaluationPermissions.Reports.Default` PLUS the per-appointment
  `AppointmentReadAccessGuard`. This overrides research's tentative
  "read-guard-alone" lean: the sheet aggregates cross-party PHI (other
  attorneys, claim examiner), and OLD only ever exposed it to internal staff via
  the report grid. The appointment-view button is shown only to internal roles.
- **Custom Form section deferred in PR3.** The nav DTO carries no custom-field
  values (custom fields are input-only today). Render the other 8 sections; add
  a `PARITY-FLAG` + a `_parity-flags.md` row for the deferred section rather
  than expanding the read DTO/repo in this slice.

Alternatives rejected:

- *Extend the appointments grid with a "report mode"* (G-08-01 Option B):
  muddies the appointments audience/column model and risks PHI-column bleed to
  roles that see appointments but not reports. Parity expects a separate
  permissioned screen.
- *Literal OLD port* (stored proc + GUID-keyed client-CSV handshake): proc body
  is not in source; inherits OLD's auth-bypass + static-cache PHI bugs.
- *Gotenberg HTML->PDF*: adds a synchronous sidecar dependency to a user-facing
  export and needs the unwired Chromium route. QuestPDF is mission-aligned and
  self-contained.
- *Render QuestPDF in Domain*: would force Domain to depend on Application.
  Contracts DTOs or duplicate the nav graph as Domain render models.

## Tasks

### PR1 -- Report grid (G-08-01) [effort ~L]

- T1: Add the `Reports` permission and seed it.
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs, src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs, src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json]
  - acceptance: `CaseEvaluation.Reports` appears in the ABP permission tree; after seed it is granted to IT Admin (host), Staff Supervisor + Clinic Staff (tenant), and to no external role.

- T2: PHI redaction for report rows (pure).
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/Patients/DobVisibility.cs, src/HealthcareSupport.CaseEvaluation.Application/Reports/ReportRowRedactor.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Reports/ReportRowRedactorTests.cs]
  - acceptance: redactor maps a nav row to a masked `AppointmentReportRowDto` with SSN as `***-**-NNNN`, DOB as 4-digit year, and Name/Email/Phone verbatim; null/blank SSN and DOB handled; unit tests green.

- T3: Report query rules (pure validator).
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/Reports/ReportFilterValidator.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Reports/ReportFilterValidatorTests.cs]
  - acceptance: rejects an all-empty filter set; rejects From > To; rejects one-of-two dates; accepts any single valid filter; resolves default sort to `RequestConfirmationNumber DESC` when none supplied; unit tests green.

- T4: Reports AppService + contracts + controller.
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application.Contracts/Reports/IReportsAppService.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Reports/GetAppointmentReportInput.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Reports/AppointmentReportRowDto.cs, src/HealthcareSupport.CaseEvaluation.Application/Reports/ReportsAppService.cs, src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs, src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Reports/ReportController.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Reports/ReportsAppServiceTests.cs]
  - acceptance: `GetAppointmentReportAsync` is `[RemoteService(IsEnabled=false)]` + `[Authorize(Reports.Default)]`, returns a paged masked result honoring the 5 filters + quick search + sort + paging, default sort confNum DESC; route `api/app/reports`; Confirmation No maps to `RequestConfirmationNumber` and the row carries `AppointmentId` for linking; a test asserts SSN/DOB are masked in the payload.

- T5: Regenerate the Angular proxy.
  - approach: code
  - files-touched: [angular/src/app/proxy/reports/**]
  - acceptance: `abp generate-proxy` produces `ReportsService` with the report method + DTOs; build green.

- T6: Angular reports feature.
  - approach: test-after
  - files-touched: [angular/src/app/reports/reports.component.ts, angular/src/app/reports/reports.component.html, angular/src/app/reports/reports.component.scss, angular/src/app/app.routes.ts, angular/src/app/route.provider.ts, src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json]
  - acceptance: a "Reports" nav entry (gated on `CaseEvaluation.Reports`) opens a quick-search + advanced-search (5 filters) + Material paged/sortable grid with the 10 columns in OLD order; client validation mirrors OLD (>=1 filter, From<=To); Confirmation No routes to `/appointments/{id}`; SSN/DOB render masked (from server); built via `npx ng build` (no `ng serve`) and manually verified.

- T7: Docs.
  - approach: code
  - files-touched: [docs/decisions/015-reporting-grid-and-pdf.md, src/HealthcareSupport.CaseEvaluation.Application/Reports/CLAUDE.md, docs/parity-research/G-08-01.md]
  - acceptance: ADR records the reuse-the-query + mask-at-boundary + QuestPDF-in-Application decisions; G-08-01 record annotated as implemented.

### PR2 -- Report-table PDF (G-08-03) [effort ~M]

- T1: Add the `Reports.Export` child permission + seed.
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs, src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs, src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json]
  - acceptance: `CaseEvaluation.Reports.Export` registered under `Reports` and seeded to the 3 internal roles.

- T2: QuestPDF report-table document (first QuestPDF render in the codebase).
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/HealthcareSupport.CaseEvaluation.Application.csproj, src/HealthcareSupport.CaseEvaluation.Application/Reports/Pdf/AppointmentReportPdfDocument.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Reports/AppointmentReportPdfDocumentTests.cs]
  - acceptance: A4 landscape, 10 columns in OLD order, repeating header on every page, title "Appointment Request Report", green (`#4CAF50`) header; given N masked rows it renders non-empty bytes beginning with `%PDF`; a smoke test asserts valid PDF bytes + non-zero pages.

- T3: Export endpoint over the full filtered set.
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application.Contracts/Reports/IReportsAppService.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Reports/ReportFileDto.cs, src/HealthcareSupport.CaseEvaluation.Application/Reports/ReportsAppService.cs, src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Reports/ReportController.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Reports/ReportsAppServiceTests.cs]
  - acceptance: `ExportAppointmentReportPdfAsync(input)` runs the SAME filters/validation as the grid but UNPAGED, reuses T2(PR1) masking, returns `{Content, ContentType="application/pdf", FileName}`; controller streams `File(...)`; gated `[Authorize(Reports.Export)]`; a test asserts the exported rows are masked.

- T4: Regenerate the Angular proxy.
  - approach: code
  - files-touched: [angular/src/app/proxy/reports/**]
  - acceptance: proxy exposes the export method; build green.

- T5: "Export to PDF" button on the grid.
  - approach: test-after
  - files-touched: [angular/src/app/reports/reports.component.ts, angular/src/app/reports/reports.component.html]
  - acceptance: button (shown with `Reports.Export`) downloads a PDF of the current filtered set; manually verified via build + serve.

- T6: Docs.
  - approach: code
  - files-touched: [docs/decisions/015-reporting-grid-and-pdf.md, docs/parity-research/G-08-03.md]
  - acceptance: ADR extended with the QuestPDF layout approach; G-08-03 annotated implemented.

### PR3 -- Demographics PDF (G-08-04) [effort ~M]

- T1: QuestPDF demographics document (8 sections).
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/Reports/Pdf/AppointmentDemographicsPdfDocument.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Reports/AppointmentDemographicsPdfDocumentTests.cs, docs/parity/_parity-flags.md]
  - acceptance: renders the 8 OLD sections (Appointment, Patient incl. masked SSN + year-only DOB, Employer, per-injury Injury/Insurance/ClaimExaminer+BodyParts, Applicant Attorney, Defense Attorney, Accessors with Edit/View); conditional bits (interpreter, language-or-other, cumulative-trauma, per-accessor rights) handled; the Custom Form section is omitted with a `PARITY-FLAG` and a `_parity-flags.md` row (status needs-test); smoke test asserts valid `%PDF` bytes.

- T2: Demographics export endpoint (internal-only + read guard).
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application.Contracts/Reports/IReportsAppService.cs, src/HealthcareSupport.CaseEvaluation.Application/Reports/ReportsAppService.cs, src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Reports/ReportController.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Reports/ReportsAppServiceTests.cs]
  - acceptance: `GetAppointmentDemographicsPdfAsync(Guid)` loads via `GetWithNavigationPropertiesAsync` (SSN already masked), calls `AppointmentReadAccessGuard.EnsureCanReadAsync`, is gated `[Authorize(Reports.Default)]`, applies year-only DOB, returns the file DTO; controller streams `File(...)`.

- T3: Regenerate the Angular proxy.
  - approach: code
  - files-touched: [angular/src/app/proxy/reports/**]
  - acceptance: proxy exposes the demographics method; build green.

- T4: Print/Download button on appointment-view.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/appointment/components/appointment-view.component.ts, angular/src/app/appointments/appointment/components/appointment-view.component.html]
  - acceptance: an internal-only Print/Download Demographics button downloads the appointment's intake PDF; not shown to external parties; manually verified.

- T5: Docs.
  - approach: code
  - files-touched: [docs/decisions/015-reporting-grid-and-pdf.md, docs/parity-research/G-08-04.md]
  - acceptance: ADR closes with the internal-only gate + deferred-custom-fields note; G-08-04 annotated implemented.

## Risk / Rollback

- Blast radius: additive across all 3 PRs. New files plus append-only edits to
  the permission provider, role seeder, en.json, `app.routes.ts`,
  `route.provider.ts`, the Mapperly aggregate, and one button on
  `appointment-view`. No existing endpoint/route is modified. QuestPDF in
  `Application` is a new package reference; the static license is already set.
- Load-bearing risk: PHI exposure. Mitigated by masking in one pure redactor
  (PR1 T2) reused by every surface, permission gating from day one, and the
  internal-only demographics gate. Do NOT port OLD's unauthenticated export.
- Rollback: revert the PR; nothing else depends on the Reports surface. Removing
  the seeded `Reports` permission grants is idempotent on re-seed.

## Verification

After each PR (build via `npx ng build --configuration development` then
`npx serve`; service order SQL -> AuthServer -> HttpApi -> Angular):

- PR1: as Clinic Staff, open Reports; confirm the 10 columns, default sort
  confNum DESC, each of the 5 filters + quick search narrows results, >=1-filter
  + From<=To validation fire; confirm SSN shows `***-**-NNNN` and DOB shows only
  the year; confirm an external user has no Reports menu entry and a 403 on the
  endpoint.
- PR2: apply filters, Export to PDF; confirm the PDF contains the full filtered
  set (not just the page), columns match the grid, SSN/DOB masked; confirm the
  button/endpoint are denied without `Reports.Export`.
- PR3: open an appointment as internal staff, Download Demographics; confirm all
  8 sections render with masked SSN + year-only DOB and the Custom Form section
  is absent (flagged); confirm an external party (patient/attorney) sees no
  button and is denied at the endpoint.
- Full suite green (Domain / Application / EFCore) before each PR opens.
