<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/States/CLAUDE.md on 2026-04-07 -->

# States — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| StateComponent | `angular/src/app/states/state/components/state.component.ts` | `/configurations/states` | List view |
| AbstractStateComponent | `angular/src/app/states/state/components/state.abstract.component.ts` | — | Base directive with CRUD wiring |
| StateDetailModalComponent | `angular/src/app/states/state/components/state-detail.component.ts` | — | Modal for create/edit |

## Pattern

ABP Suite abstract/concrete (`AbstractStateComponent` -> `StateComponent`). Simplest UI in the application — single Name field, no lookups, no bulk operations.

## Routes and Guards

- Route: `/configurations/states` (under Configurations parent menu)
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.States`
- Menu config: `state-base.routes.ts` — icon `fas fa-flag`, parent `::Menu:Configurations`

## Forms

**Detail Form:**
- name: text input (required, no max length constraint in UI or backend consts)

**Filter Form:**
- name: text input

## Services

| Service | Source | Purpose |
|---|---|---|
| StateViewService | Custom | List data, filtering |
| StateDetailViewService | Custom | Form management |
| StateService | Proxy | REST API client |
| ListService | @abp/ng.core | Pagination |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |

## Permission Checks

- `CaseEvaluation.States.Create` — checked in abstract component for "New" button visibility
- `CaseEvaluation.States.Edit` — checked for edit action visibility
- `CaseEvaluation.States.Delete` — checked for delete action visibility

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
