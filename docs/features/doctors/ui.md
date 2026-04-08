<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md on 2026-04-08 -->

# Doctors — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| DoctorComponent | `angular/src/app/doctors/doctor/components/doctor.component.ts` | `/doctor-management/doctors` | List view with filtering |
| AbstractDoctorComponent | `angular/src/app/doctors/doctor/components/doctor.abstract.component.ts` | — | Base directive with CRUD wiring |
| DoctorDetailModalComponent | `angular/src/app/doctors/doctor/components/doctor-detail.component.ts` | — | Multi-tab modal (Doctor, AppointmentTypes, Locations) |

## Pattern

ABP Suite abstract/concrete. `AbstractDoctorComponent` → `DoctorComponent`. Detail modal uses 3 tabs for the entity fields and its M2M relationships.

## Routes and Guards

- Route: `/doctor-management/doctors`
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.Doctors`
- Parent menu: `::Menu:DoctorManagement`

## Forms

**Detail Form (Tab 1 — Doctor):**
- firstName: text (maxLength: 50, required)
- lastName: text (maxLength: 50, required)
- email: text (maxLength: 49, required, email validation)
- gender: select dropdown (Male, Female, Other; required, defaults to null)
- identityUserId: lookup select (optional)

**Tab 2 — AppointmentTypes (M2M):**
- appointmentTypeIds: lookup typeahead many-to-many component

**Tab 3 — Locations (M2M):**
- locationIds: lookup typeahead many-to-many component

**Filter Form:**
- firstName, lastName, email: text inputs
- identityUserId: lookup select
- appointmentTypeId: lookup typeahead
- locationId: lookup typeahead

## Services

| Service | Source | Purpose |
|---|---|---|
| DoctorViewService | Custom | List data, filtering |
| DoctorDetailViewService | Custom | Form management, lookups |
| DoctorService | Proxy | REST API + lookup methods (getAppointmentTypeLookup, getLocationLookup, getIdentityUserLookup) |
| ListService | @abp/ng.core | Pagination |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |

## Permission Checks

- `*abpPermission="'CaseEvaluation.Doctors.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.Doctors.Delete'"` — delete action
- Note: No explicit Create permission check in template (create button always visible to authorized users)

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
