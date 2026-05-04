---
feature: internal-user-reports
date: 2026-05-04
phase: 2-frontend (NOT YET IMPLEMENTED in NEW; no reports module exists in angular/src/app/)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointment-request-report/search/appointment-request-report-search.component.html (150 lines) + .ts (167 lines)
new-feature-path: n/a (reports module does not yet exist in angular/src/app/)
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: pending
---

# Design: Internal User -- Reports (Appointment Request Report)

## Overview

The Reports page lets Clinic Staff, Staff Supervisor, and IT Admin search and export
appointment data as PDF or Excel, and generate per-appointment Patient Demographics
PDF reports.

In OLD, a single "Appointment Request Report" page at `/appointment-request-report`
provides filtered tabular data with bulk export and a per-row demographics report.

In NEW, **this feature is not yet implemented**. No `reports` Angular module or
AppService exists. This doc captures the OLD behavior as the design contract.

---

## 1. Route

| | OLD | NEW |
|---|---|---|
| Reports page | `/appointment-request-report` (AppointmentRequestReportModule) | `/reports` |

Guard:
- OLD: `canActivate: [PageAccess]` `rootModuleId: 33` `applicationModuleId: 13`
- NEW: `canActivate: [authGuard, permissionGuard]`; `CaseEvaluation.Reports.Default`

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar).
Side-nav item: "Reports" under the main navigation.

---

## 3. OLD Page Layout

```
+----------------------------------------------------------+
| [H2] Appointment Request Report                         |
|                          [Search input] [Sync reset]    |
| [Advanced Search accordion]                             |
|   Appt Type [select]   Location [select]                |
|   Status [select]      Patient Name [text]              |
|   From Date [date]     To Date [date]                   |
|                        [Reset]  [Search]                |
+----------------------------------------------------------+
| [Export to PDF]  [Export to Excel]                      |
| [rx-table]                                              |
| Conf# | Appt Type | Location | Appt Date/Time |         |
| Status | Patient Name | DOB | Email | Phone | SSN |      |
| Action (Patient Demographics PDF)                       |
+----------------------------------------------------------+
```

OLD source: `appointment-request-report/search/appointment-request-report-search.component.html:1-150`

---

## 4. Filters

### 4a. Inline search

| Element | Behavior |
|---|---|
| Text input | Searches across all columns; submits on Enter key or search icon click |
| Reset icon | Clears search text and reloads full list |

### 4b. Advanced Search accordion (collapsed by default)

| Filter | Type | Notes |
|---|---|---|
| Appointment Type | dropdown | Lookup; filters out `AppointmentTypeEnum.ALL` entry |
| Location | dropdown | Lookup from `locationLookUps` |
| Appointment Status | dropdown | Lookup from `appointmentStatusLookUps` (all 13 statuses) |
| Patient Name | text | Free-text partial match |
| From Date | date picker | `MM/DD/YYYY` format; validates against To Date |
| To Date | date picker | `MM/DD/YYYY` format; validates against From Date |

**Filter validation rules (must replicate exactly):**
1. If all filters are empty, show error: "Please enter a search value" (from i18n key `validation.message.custom.enterSearchValue`).
2. If From Date is set but To Date is empty, show error: "Please enter end date".
3. If To Date is set but From Date is empty, show error: "Please enter 'From Date'".
4. If both dates are set but From Date > To Date, show error: "To Date should be greater than From Date".

These validations fire on **Search button click** (`filterReport()`), not on field change.

OLD source: `search/appointment-request-report-search.component.ts:97-136`

---

## 5. Table Columns

| Column | Field | Notes |
|---|---|---|
| Confirmation No | `requestConfirmationNumber` | Clickable link → appointment-view page |
| Appointment Type | `appointmentTypeName` | |
| Location Name | `locationName` | |
| Appointment Date Time | `appointmentDateTime` | |
| Status | `appointmentStatusName` | Plain text (no color badge in this table) |
| Patient Name | `patientName` | `text-uppercase` CSS |
| Date Of Birth | `dateOfBirth` | PHI |
| Email | `email` | PHI |
| Phone Number | `phoneNumber` | PHI |
| Social Security Number | `socialSecurityNumber` | PHI; no masking applied in OLD (raw SSN shown) |
| Action | -- | "Patient Demographics Report" PDF icon (see Section 6) |

- Default sort: `requestConfirmationNumber` descending.
- Pagination: server-side; page size configurable via form.
- Confirmation No click: `router.navigate(['appointments', appointmentDetail.appointmentId])`.

OLD source: `search/appointment-request-report-search.component.html:110-141`

---

## 6. Export Buttons

### 6a. Bulk table export

| Button | OLD behavior | NEW behavior |
|---|---|---|
| Export to PDF | `tableEvent.exportToCsv('Appointment Request Report', 1)` -- rx-table client-side export | Server-side: `GET /api/app/reports/export?format=pdf` with current filter state |
| Export to Excel | `tableEvent.exportToCsv('Appointment Request Report', 2)` -- rx-table client-side export | Server-side: `GET /api/app/reports/export?format=excel` with current filter state |

OLD exports current filtered/sorted data from the `rx-table` in-memory state (client-side).
NEW must generate server-side because the Angular Material table is not `rx-table` and has no
built-in export capability. See Exception 1.

### 6b. Per-row Patient Demographics Report

| Element | OLD behavior | NEW behavior |
|---|---|---|
| Trigger | Click PDF icon in Action column | Click PDF icon in Action column |
| OLD endpoint | `GET api/CsvExport/{appointmentId}/1` -- returns `{ htmlData: string }` | NEW endpoint: `GET /api/app/reports/{appointmentId}/demographics` -- returns PDF stream |
| OLD display | Opens `htmlData` in a new browser window | Browser downloads PDF file |
| Content | Patient demographics: name, DOB, SSN, claim info, contact details | Same content, PDF format |

See Exception 2.

OLD source: `search/appointment-request-report-search.component.ts:161-166`,
`login.service.ts:35-37` (`api/CsvExport/{appointmentId}/1`)

---

## 7. API

| Operation | OLD | NEW |
|---|---|---|
| Search / filter | `POST api/AppointmentRequestReport` (stored proc: `[isStoreProc]="true"`) | `GET /api/app/reports` with query params |
| Bulk PDF export | `rx-table` built-in export (client-side) | `GET /api/app/reports/export?format=pdf` |
| Bulk Excel export | `rx-table` built-in export (client-side) | `GET /api/app/reports/export?format=excel` |
| Per-row demographics | `GET api/CsvExport/{appointmentId}/1` | `GET /api/app/reports/{appointmentId}/demographics` |

**Search params (OLD stored proc input):**
- `appointmentTypeId` (int?)
- `locationId` (int?)
- `appointmentStatusId` (int?)
- `patientName` (string?)
- `appointmentStartDate` (date?, `yyyy-MM-dd` format)
- `appointmentEndDate` (date?, `yyyy-MM-dd` format)
- `search` (string? -- global text search)
- `orderByColumn` (string, default: `requestConfirmationNumber`)
- `sortOrder` (string, default: `desc`)
- `pageIndex` (int)
- `rowCount` (int)

**Search response fields:** `requestConfirmationNumber`, `appointmentTypeName`, `locationName`,
`appointmentDateTime`, `appointmentStatusName`, `patientName`, `dateOfBirth`, `email`,
`phoneNumber`, `socialSecurityNumber`, `appointmentId`, `totalCount`

---

## 8. PHI Handling

This page displays PHI directly in the table (DOB, Email, Phone, SSN). Access must be
restricted to internal roles. OLD applied `MODULES.Reports` access check on component init.

NEW must gate this route behind `permissionGuard` with `CaseEvaluation.Reports.Default`.

**SSN masking: note that the OLD table shows raw SSN (no masking).** This is a parity flag.
All other appointment-list surfaces mask SSN. The report page does not. Replicate OLD
behavior (show raw SSN) but flag for future PHI policy review. See Exception 3.

---

## 9. Role Visibility Matrix

| Role | Access | Notes |
|---|---|---|
| Patient / Adjuster / Attorney / ClaimExaminer | No | External users never access reports |
| Clinic Staff | Yes (own appointments only, scoped) | Matches appointment list role-scoping |
| Staff Supervisor | Yes (all appointments) | |
| IT Admin | Yes (all appointments) | |

Role-scoped filtering (Clinic Staff vs Supervisor/Admin) applies the same
`IAppointmentAccessPolicy` used by the main appointments list.

---

## 10. Branding Tokens

| Element | Token |
|---|---|
| Page heading | `--text-primary` |
| Export to PDF button | `btn-primary` via `--brand-primary` |
| Export to Excel button | `btn-primary` via `--brand-primary` |
| Search button | `btn-primary` via `--brand-primary` |
| Reset button | `btn-secondary` |
| Demographics PDF icon | `--brand-primary` (icon color) |
| Error toast | `--status-rejected` (red) |

---

## 11. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Table component | `rx-table` (in-house, stored proc pagination, built-in export) | Angular Material `mat-table` with server-side pagination |
| PDF export | `rx-table.exportToCsv(..., 1)` (client-side HTML/CSS to PDF) | Server-side `QuestPDF` / iTextSharp PDF generation |
| Excel export | `rx-table.exportToCsv(..., 2)` (client-side CSV/xls) | Server-side `ClosedXML` Excel generation |
| Patient Demographics | `api/CsvExport/{id}/1` returns HTML string opened in new window | Server-side PDF endpoint → browser download |
| Filter validation | Custom `filterReport()` with manual if/else checks | Angular reactive form validators + `MatSnackBar` / `MatDialog` errors |
| Search subscription | RxJS `Subscription` with unsubscribe on destroy | Angular `takeUntilDestroyed()` or signal-based |
| Stored proc | Yes (`[isStoreProc]="true"`) | Standard LINQ query in AppService |

---

## 12. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Bulk PDF/Excel export | Client-side via `rx-table.exportToCsv()` -- exports current in-memory page | Server-side export endpoint receives current filter params and generates file | `rx-table` not available in NEW; server-side export is more reliable for large datasets |
| 2 | Patient Demographics Report | `api/CsvExport/{id}/1` returns HTML; opened in new browser window | Dedicated PDF endpoint returns file stream; downloaded to disk | Framework change; DOCX/HTML reports replaced with PDF per CLAUDE.md primary mission |
| 3 | Raw SSN in table | SSN column shows unmasked value (no `***-**-NNNN` mask) | Replicate OLD verbatim but flag for PHI review | Parity: reports page historically showed full SSN to authorized internal users; masking policy to be confirmed with Adrian before Phase 19b sign-off |
| 4 | Global search on keystroke | Enter key triggers `bindReport()`; no debounce | Add 300ms debounce on Enter to prevent excessive API calls | UX improvement; functionally equivalent |
| 5 | Filter validation requires at least one filter | `filterReport()` blocks if all 6 filters empty | Replicate: show error toast if Search clicked with all filters empty | Same validation gate; ensures non-empty queries |

---

## 13. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `appointment-request-report/search/appointment-request-report-search.component.html` | 1-150 | Full report page: search input, advanced filters, export buttons, rx-table |
| `appointment-request-report/search/appointment-request-report-search.component.ts` | 1-167 | `filterReport()`, `bindReport()`, `exportPdf()`, `viewAppointment()`, validation logic |
| `appointment-request-report/appointment-request-report.routing.ts` | 1-14 | Route guard `PageAccess`, `applicationModuleId: 13`, `rootModuleId: 33` |
| `appointment-request-report/appointment-request-report.service.ts` | 1-80 | `search()`, `exportPdf()` -- `api/CsvExport/{id}/1` |
| `login/login.service.ts` | 35-37 | `printPDF()` -- `api/CsvExport/{appointmentId}/1` |

---

## 14. Verification Checklist

*(Pending implementation)*

- [ ] Reports page accessible to Clinic Staff, Staff Supervisor, IT Admin; blocked for external users
- [ ] Page title "Appointment Request Report" displayed
- [ ] Global text search triggers on Enter key and search icon click; reset icon clears and reloads
- [ ] Advanced Search accordion collapsed by default; expands on click
- [ ] Appointment Type, Location, Status dropdowns populated from lookup endpoints
- [ ] Patient Name free-text filter works (partial match)
- [ ] From Date and To Date date pickers work with MM/DD/YYYY format
- [ ] Clicking Search with all filters empty shows error toast
- [ ] From Date > To Date shows "To Date should be greater than From Date" error
- [ ] From Date only (no To Date) shows "Please enter end date" error
- [ ] To Date only (no From Date) shows "Please enter 'From Date'" error
- [ ] Table shows 11 columns in correct order; Patient Name is uppercase
- [ ] Confirmation No is a clickable link navigating to the appointment view page
- [ ] Default sort: Confirmation No descending
- [ ] Server-side pagination works; page size respects form settings
- [ ] "Export to PDF" button generates server-side PDF of current filtered data
- [ ] "Export to Excel" button generates server-side Excel of current filtered data
- [ ] Action column PDF icon generates Patient Demographics Report PDF download for that row
- [ ] Clinic Staff sees only appointments where they are the Responsible User
- [ ] Staff Supervisor / IT Admin see all appointments (no role filter)
- [ ] SSN column shows unmasked value (parity) -- Exception 3 confirmed with Adrian
