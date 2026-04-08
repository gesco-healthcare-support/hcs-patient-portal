<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Locations/CLAUDE.md on 2026-04-08 -->

# Locations — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| LocationComponent | `angular/src/app/locations/location/components/location.component.ts` | `/doctor-management/locations` | List view with bulk delete |
| AbstractLocationComponent | `angular/src/app/locations/location/components/location.abstract.component.ts` | — | Base directive with CRUD wiring |
| LocationDetailModalComponent | `angular/src/app/locations/location/components/location-detail.component.ts` | — | Modal for create/edit |

## Pattern

ABP Suite abstract/concrete. `AbstractLocationComponent` → `LocationComponent`. Supports bulk selection and delete.

## Routes and Guards

- Route: `/doctor-management/locations`
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.Locations`
- Parent menu: `::Menu:DoctorManagement`

## Forms

**Detail Form:**
- name: text (maxLength: 50, required)
- address: text (maxLength: 100)
- city: text (maxLength: 50)
- zipCode: text (maxLength: 15)
- parkingFee: number (required)
- isActive: checkbox
- stateId: lookup select
- appointmentTypeId: lookup select

**Filter Form:**
- name, city, zipCode: text inputs
- parkingFeeMin, parkingFeeMax: number inputs
- isActive: select
- stateId: lookup select
- appointmentTypeId: lookup select

## Services

| Service | Source | Purpose |
|---|---|---|
| LocationViewService | Custom | List data, filtering, bulk operations |
| LocationDetailViewService | Custom | Form management, lookups |
| LocationService | Proxy | REST API client |
| ListService | @abp/ng.core | Pagination |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |

## Permission Checks

- `*abpPermission="'CaseEvaluation.Locations.Create'"` — create button
- `*abpPermission="'CaseEvaluation.Locations.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.Locations.Delete'"` — delete (single and bulk)

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
