<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/CLAUDE.md on 2026-04-08 -->

# WcabOffices — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| WcabOfficeComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.component.ts` | `/doctor-management/wcab-offices` | List view with bulk delete and Excel export |
| AbstractWcabOfficeComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.abstract.component.ts` | — | Base directive with export logic |
| WcabOfficeDetailModalComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office-detail.component.ts` | — | Modal for create/edit |

## Pattern

ABP Suite abstract/concrete. `AbstractWcabOfficeComponent` includes `exportToExcel()` method. Supports bulk selection and delete.

## Routes and Guards

- Route: `/doctor-management/wcab-offices`
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.WcabOffices`
- Parent menu: `::Menu:DoctorManagement`

## Forms

**Detail Form:**
- name: text (maxLength: 50, required)
- abbreviation: text (maxLength: 50, required)
- address: text (maxLength: 100)
- city: text (maxLength: 50)
- zipCode: text (maxLength: 15)
- isActive: checkbox
- stateId: lookup select

**Filter Form:**
- name, abbreviation, address, city, zipCode: text inputs
- isActive: select
- stateId: lookup select

## Services

| Service | Source | Purpose |
|---|---|---|
| WcabOfficeViewService | Custom | List data, filtering, bulk operations, Excel export |
| WcabOfficeDetailViewService | Custom | Form management, lookups |
| WcabOfficeService | Proxy | REST API client |
| ListService | @abp/ng.core | Pagination |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |

## Permission Checks

- `*abpPermission="'CaseEvaluation.WcabOffices.Create'"` — create button
- `*abpPermission="'CaseEvaluation.WcabOffices.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.WcabOffices.Delete'"` — delete (single and bulk)

## Special Features

- **Export to Excel**: "Export to Excel" button with busy state tracking (`isExportToExcelBusy`)

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
