<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/CLAUDE.md on 2026-04-08 -->

# AppointmentTypes — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| AppointmentTypeComponent | `angular/src/app/appointment-types/appointment-type/components/appointment-type.component.ts` | `/appointment-management/appointment-types` | List view with bulk delete |
| AbstractAppointmentTypeComponent | `angular/src/app/appointment-types/appointment-type/components/appointment-type.abstract.component.ts` | — | Base directive with bulk selection |
| AppointmentTypeDetailModalComponent | `angular/src/app/appointment-types/appointment-type/components/appointment-type-detail.component.ts` | — | Modal for create/edit |

## Pattern

ABP Suite abstract/concrete with bulk operations. `AbstractAppointmentTypeComponent` includes `bulkDelete()`, `selectAll()`, and `selectedCount()` computed signal.

## Routes and Guards

- Route: `/appointment-management/appointment-types`
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.AppointmentTypes`
- Parent menu: `::Menu:AppointmentManagement`

## Forms

**Detail Form:**
- name: text (maxLength: 100, required)
- description: text (maxLength: 200, optional)

**Filter Form:**
- nameFilter: text input

## Services

| Service | Source | Purpose |
|---|---|---|
| AppointmentTypeViewService | Custom | List data, bulk operations |
| AppointmentTypeDetailViewService | Custom | Form management |
| AppointmentTypeService | Proxy | REST API client |
| ListService | @abp/ng.core | Pagination |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |
| AbpWindowService | @abp/ng.core | Window utilities |

## Permission Checks

- `*abpPermission="'CaseEvaluation.AppointmentTypes.Create'"` — create button
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.AppointmentTypes.Delete'"` — delete (single and bulk)

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
