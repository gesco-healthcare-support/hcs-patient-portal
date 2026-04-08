<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/ApplicantAttorneys/CLAUDE.md on 2026-04-08 -->

# ApplicantAttorneys — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| ApplicantAttorneyComponent | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney.component.ts` | `/applicant-attorneys` | List view with filtering and table |
| AbstractApplicantAttorneyComponent | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney.abstract.component.ts` | — | Base directive with CRUD wiring |
| ApplicantAttorneyDetailModalComponent | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney-detail.component.ts` | — | Modal form for create/edit |

## Pattern

ABP Suite abstract/concrete pattern. `AbstractApplicantAttorneyComponent` is a `@Directive()` base class; `ApplicantAttorneyComponent` extends it as a standalone component.

## Routes and Guards

- Route: `/applicant-attorneys`
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.ApplicantAttorneys`

## Forms

**List Filter Form:**
- firmName: text input
- phoneNumber: text input
- city: text input
- stateId: lookup select (`getStateLookup()`)
- identityUserId: lookup select (`getIdentityUserLookup()`)

**Detail Form:**
- firmName: text (maxLength: 50, required)
- firmAddress: text (maxLength: 100)
- phoneNumber: text (maxLength: 20)
- webAddress: text (maxLength: 100)
- faxNumber: text (maxLength: 19)
- street: text (maxLength: 255)
- city: text (maxLength: 50)
- zipCode: text (maxLength: 10)
- stateId: lookup select
- identityUserId: lookup select (required)

## Services

| Service | Source | Purpose |
|---|---|---|
| ApplicantAttorneyViewService | Custom | List data, delete, filtering |
| ApplicantAttorneyDetailViewService | Custom | Form management, create/edit, lookups |
| ApplicantAttorneyService | Proxy (auto-generated) | REST API client |
| ListService | @abp/ng.core | Pagination and query |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |

## Permission Checks

- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Create'"` — create button
- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Delete'"` — delete action
- Service-level: `permissionService.getGrantedPolicy('CaseEvaluation.ApplicantAttorneys.Edit')`

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
