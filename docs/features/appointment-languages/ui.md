<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/CLAUDE.md on 2026-04-08 -->

# AppointmentLanguages — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| AppointmentLanguageComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.component.ts` | `/appointment-management/appointment-languages` | List view |
| AbstractAppointmentLanguageComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.abstract.component.ts` | — | Base directive |
| AppointmentLanguageDetailModalComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language-detail.component.ts` | — | Modal for create/edit |

## Pattern

ABP Suite abstract/concrete. `AbstractAppointmentLanguageComponent` → `AppointmentLanguageComponent`.

## Routes and Guards

- Route: `/appointment-management/appointment-languages`
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.AppointmentLanguages`
- Parent menu: `::Menu:AppointmentManagement`

## Forms

**Detail Form:**
- name: text (maxLength: 50, required, default: "English")

## Services

| Service | Source | Purpose |
|---|---|---|
| AppointmentLanguageViewService | Custom | List data, filtering |
| AppointmentLanguageDetailViewService | Custom | Form management |
| AppointmentLanguageService | Proxy | REST API client |
| ListService | @abp/ng.core | Pagination |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |

## Permission Checks

- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Create'"` — create button
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Delete'"` — delete action

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
