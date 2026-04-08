<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/CLAUDE.md on 2026-04-08 -->

# AppointmentStatuses — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| AppointmentStatusComponent | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status.component.ts` | `/appointment-management/appointment-statuses` | List view |
| AbstractAppointmentStatusComponent | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status.abstract.component.ts` | — | Base directive |
| AppointmentStatusDetailModalComponent | `angular/src/app/appointment-statuses/appointment-status/components/appointment-status-detail.component.ts` | — | Modal for create/edit |

## Pattern

ABP Suite abstract/concrete. `AbstractAppointmentStatusComponent` → `AppointmentStatusComponent`.

## Routes and Guards

- Route: `/appointment-management/appointment-statuses`
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.AppointmentStatuses`
- Parent menu: `::Menu:AppointmentManagement`

## Forms

**Detail Form:**
- name: text (maxLength: 100, required)

## Services

| Service | Source | Purpose |
|---|---|---|
| AppointmentStatusViewService | Custom | List data, filtering |
| AppointmentStatusDetailViewService | Custom | Form management |
| AppointmentStatusService | Proxy | REST API client |
| ListService | @abp/ng.core | Pagination |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |

## Permission Checks

- `*abpPermission="'CaseEvaluation.AppointmentStatuses.Create'"` — create button
- `*abpPermission="'CaseEvaluation.AppointmentStatuses.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.AppointmentStatuses.Delete'"` — delete action

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
