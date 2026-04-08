<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md on 2026-04-08 -->

# Patients — UI

> Angular component documentation. Update code-derived content in the feature CLAUDE.md.

## Components

| Component | File | Route | Purpose |
|---|---|---|---|
| PatientComponent | `angular/src/app/patients/patient/components/patient.component.ts` | `/patients` | List view with extensive filtering |
| AbstractPatientComponent | `angular/src/app/patients/patient/components/patient.abstract.component.ts` | — | Base directive with CRUD wiring |
| PatientDetailModalComponent | `angular/src/app/patients/patient/components/patient-detail.component.ts` | — | Modal for create/edit (20+ fields) |
| PatientProfileComponent | `angular/src/app/patients/patient/components/patient-profile.component.ts` | `/doctor-management/patients/my-profile` | Self-service profile view |

## Pattern

ABP Suite abstract/concrete for admin CRUD. Plus a standalone `PatientProfileComponent` for self-service access by external users (patients, attorneys).

## Routes and Guards

- List route: `/patients`
- Guards: `authGuard`, `permissionGuard`
- Required policy: `CaseEvaluation.Patients`
- Profile route: `/doctor-management/patients/my-profile` (separate, self-service)

## Forms

**Detail Form:**
- firstName: text (maxLength: 50, required)
- lastName: text (maxLength: 50, required)
- middleName: text (maxLength: 50)
- email: text (maxLength: 50, required)
- genderId: select (Male=1, Female=2, Other=3; required)
- dateOfBirth: datepicker (required)
- phoneNumber: text (maxLength: 20)
- socialSecurityNumber: text (maxLength: 20)
- address: text (maxLength: 100)
- city: text (maxLength: 50)
- zipCode: text (maxLength: 15)
- refferedBy: text (maxLength: 50)
- cellPhoneNumber: text (maxLength: 12)
- phoneNumberTypeId: select (Work=28, Home=29; required)
- street: text (maxLength: 255)
- interpreterVendorName: text (maxLength: 255)
- apptNumber: text (maxLength: 100)
- othersLanguageName: text (maxLength: 100)
- stateId: lookup select
- appointmentLanguageId: lookup select
- identityUserId: lookup select (required)
- tenantId: lookup select

**Filter Form:**
- firstName, lastName, middleName, email, phoneNumber, socialSecurityNumber, address, city, zipCode, refferedBy, cellPhoneNumber, street, interpreterVendorName, apptNumber: text inputs
- genderId: select
- dateOfBirthMin, dateOfBirthMax: date inputs
- stateId, appointmentLanguageId, identityUserId: lookup selects

## Services

| Service | Source | Purpose |
|---|---|---|
| PatientViewService | Custom | List data, filtering |
| PatientDetailViewService | Custom | Form management, lookups |
| PatientService | Proxy | REST API client |
| ListService | @abp/ng.core | Pagination |
| PermissionService | @abp/ng.core | Permission checks |
| ConfirmationService | @abp/ng.theme.shared | Delete confirmation |
| AuthService | @abp/ng.core | Profile component auth |
| ConfigStateService | @abp/ng.core | Profile component config |
| RestService | @abp/ng.core | Profile component direct API calls |

## Permission Checks

- `*abpPermission="'CaseEvaluation.Patients.Create'"` — create button
- `*abpPermission="'CaseEvaluation.Patients.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.Patients.Delete'"` — delete action

## Profile Component

The `PatientProfileComponent` is a self-service view:
- Loads `/api/app/patients/me` for the authenticated user's patient record
- Falls back to `/api/app/external-users/me` for non-patient external roles (Applicant Attorney, Defense Attorney)
- External users see read-only display; patients can edit their own profile

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
