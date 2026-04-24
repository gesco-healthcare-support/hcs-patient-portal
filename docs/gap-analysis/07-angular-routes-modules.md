# 07 -- Angular Routes + Modules: Gap Analysis OLD vs NEW

## Summary

OLD exposes 32 distinct in-app URL paths defined across 29 `*-routing.module.ts` files using the Angular 7 NgModule + `loadChildren` string-import pattern. NEW exposes 22 application-feature URL paths plus 9 ABP Commercial module routes (identity, saas, audit-logs, etc.) defined via Angular 20 standalone `loadComponent` / `loadChildren` lambdas. After subtracting the tracks of OLD URLs that are intentional architectural differences (string-based lazy loading, `PageAccess`/`CanActivatePage` guards, login/reset/forgot/verify-email now served by `@volo/abp.ng.account/public`, and `users` now served by `@volo/abp.ng.identity`), 14 OLD feature routes are absent in NEW. Of those, 10 are MVP-blocking. MVP risk rating: High.

## Method

- OLD: Read `P:\PatientPortalOld\patientappointment-portal\src\app\app.module.ts` (bootstraps `APP_LAZY_ROUTING`) and `P:\PatientPortalOld\patientappointment-portal\src\app\components\start\app.lazy.routing.ts` (the 27-entry `APP_LAZY_ROUTES` root) plus every per-feature `*-routing.module.ts` referenced by `loadChildren`. Glob: `P:\PatientPortalOld\patientappointment-portal\src\app\components\**\*routing*.ts` (29 files found).
- NEW: Read `W:\patient-portal\development\angular\src\app\app.routes.ts` (the `APP_ROUTES` root) plus every per-feature `*-routes.ts` file it references. Globs: `W:\patient-portal\development\angular\src\app\**\*-routes.ts` (11 files) and `W:\patient-portal\development\angular\src\app\**\*.routes.ts` (14 `*-base.routes.ts` menu metadata files plus `gdpr-cookie-consent.routes.ts`).
- NEW's `providers/*-base.routes.ts` files are menu / navigation metadata (`ABP.Route[]` with `iconClass`, `requiredPolicy`, `breadcrumbText`), not router routes; they are discussed only in the Intentional differences section.
- No Angular source was modified. No file under `angular/src/app/proxy/` was read or touched. Timestamps 2026-04-23 local.

## OLD version state

### Root routing module

`P:\PatientPortalOld\patientappointment-portal\src\app\components\start\app.lazy.routing.ts:19-147` defines `APP_LAZY_ROUTES` (27 top-level entries) and exports it via `RouterModule.forRoot(APP_LAZY_ROUTES, { preloadingStrategy: PreloadAllModules })` at line 150. The root module imports this via `APP_LAZY_ROUTING` from `app.module.ts:20`.

### Guards used

- `CanActivatePage` (`P:\PatientPortalOld\patientappointment-portal\src\app\domain\authorization\can-activate-page.ts:21`) -- the workhorse guard. Fetches permission tree from `POST /api/userauthorization`, caches per `cacheMinutes`, reads `route.data.applicationModuleId` / `accessItem` / `rootModuleId` / `childModuleName`, and indexes into `user.applicationPermission[moduleId][accessItem]`. Handles `route.data.anonymous`: if `anonymous && !auth` -> allow; if `anonymous && auth` -> redirect to `dashboard` (internal) or `home` (external). Special-cases `verify-email`, `applicationModuleId == 1037`, `applicationModuleId == 5104`.
- `PageAccess` (`P:\PatientPortalOld\patientappointment-portal\src\app\domain\authorization\page-access.ts:11`) -- composite guard awaiting `CanActivatePage.canActivate(...)` AND `ApplicationJsonConfiguration.canActivate(...)`; resolves true only when both resolve true.

### Full OLD route inventory

| # | URL path | Defined in (file:line) | Sub-module file | Guard | Route data (key props) |
|---|---|---|---|---|---|
| 1 | empty -> redirect `/login` | `components/start/app.lazy.routing.ts:20-22` | n/a | `PageAccess` | `anonymous: true` |
| 2 | `/login` | `app.lazy.routing.ts:24` + `login/login.routing.ts:7` | `login/login.routing.ts` | `PageAccess` | `anonymous: true` |
| 3 | `/unauthorized` | `app.lazy.routing.ts:28` | inline | none | -- |
| 4 | `/dashboard` | `app.lazy.routing.ts:31-34` + `dashboard/dashboard.routing.ts:7-11` | `dashboard.routing.ts` | `CanActivatePage` | `rolePermission: false` |
| 5 | `/home` | `app.lazy.routing.ts:36-38` + `my-appointments/my-appointment-list.routing.ts:6` | my-appointment-list | `CanActivatePage` | `rolePermission: false` |
| 6 | `/forgot-password` | `app.lazy.routing.ts:40-42` | inline | `PageAccess` | `anonymous: true, rolePermission: false` |
| 7 | `/reset-password` | `app.lazy.routing.ts:44-46` | inline | `PageAccess` | `anonymous: true, rolePermission: false` |
| 8 | `/verify-email/:userId` | `app.lazy.routing.ts:48-50` | inline | `PageAccess` | `anonymous: true, rolePermission: false` |
| 9 | `/users` | `app.lazy.routing.ts:52-54` + `user/users/users.routing.ts:6-11` | users.routing | `PageAccess` | `applicationModuleId: 8, accessItem: 'list', rootModuleId: 33` |
| 10 | `/users/add` | `users.routing.ts:13-17` + `add/user-add.routing.ts:7` | user-add | `PageAccess` | `anonymous: true` |
| 11 | `/users/:userId` | `users.routing.ts:19-23` + `edit/user-edit.routing.ts:6` | user-edit | `PageAccess` | `applicationModuleId: 8, accessItem: 'edit'` |
| 12 | `/appointments/add` | `appointments/appointments.routing.ts:6-11` + `add/appointment-add.routing.ts:6` | appointment-add | `PageAccess` | `applicationModuleId: 6, accessItem: 'add'` |
| 13 | `/appointments/:appointmentId` | `appointments.routing.ts:13-17` + `edit/appointment-edit.routing.ts:6` | appointment-edit | `PageAccess` | `applicationModuleId: 6, accessItem: 'edit'` |
| 14 | `/doctors/:doctorId` | `doctor-management/doctors/doctors.routing.ts:6-11` + `edit/doctor-edit.routing.ts:6` | doctor-edit | `PageAccess` | `applicationModuleId: 9, accessItem: 'edit'` |
| 15 | `/system-parameters/:systemParameterId` | `system-parameters/system-parameters.routing.ts:6-11` + `edit/system-parameter-edit.routing.ts:6` | sys-param-edit | `PageAccess` | `applicationModuleId: 11, accessItem: 'edit'` |
| 16 | `/templates` | `templates/templates.routing.ts:6-11` + `list/template-list.routing.ts:6` | template-list | `PageAccess` | `applicationModuleId: 19, accessItem: 'list'` |
| 17 | `/appointment-rescheduled-requests` | `appointment-change-requests/appointment-change-requests.routing.ts:6-11` + `list/appointment-change-request-list.routing.ts:6` | change-req-list | `PageAccess` | `applicationModuleId: 6, accessItem: 'list'` |
| 18 | `/appointment-cancel-requests` | `app.lazy.routing.ts:76-78` + `detail/appointment-change-request-detail.routing.ts:7-10` | change-req-detail | `PageAccess` | `applicationModuleId: 6, accessItem: 'list'` |
| 19 | `/appointment-change-logs` | `appointment-change-logs/appointment-change-logs.routing.ts:6-11` + `list/appointment-change-log-list.routing.ts:7-10` | change-log-list | `PageAccess` | `applicationModuleId: 14, accessItem: 'list'` |
| 20 | `/documents` | `documents/documents.routing.ts:6-11` + `list/document-list.routing.ts:6` | document-list | `PageAccess` | `applicationModuleId: 27, accessItem: 'list'` |
| 21 | `/package-details` | `package-details/package-details.routing.ts:6-11` + `list/package-detail-list.routing.ts:6` | package-list | `PageAccess` | `applicationModuleId: 27, accessItem: 'list'` |
| 22 | `/custom-fields` | `custom-fields/custom-fields.routing.ts:6-11` + `list/custom-field-list.routing.ts:6` | cf-list | `PageAccess` | `applicationModuleId: 10, accessItem: 'list'` |
| 23 | `/doctors-availabilities` | `doctors-availabilities.routing.ts:6-11` + `list/doctors-availability-list.routing.ts:6` | avail-list | `PageAccess` | `applicationModuleId: 9, accessItem: 'list'` |
| 24 | `/doctors-availabilities/add` | `doctors-availabilities.routing.ts:13-17` + `add/doctors-availability-add.routing.ts:7-9` | avail-add | `PageAccess` | `applicationModuleId: 9, accessItem: 'add'` |
| 25 | `/doctors-availabilities/:doctorsAvailabilityId` | `doctors-availabilities.routing.ts:19-23` + `edit/doctors-availability-edit.routing.ts:6` | avail-edit | `PageAccess` | `applicationModuleId: 9, accessItem: 'edit'` |
| 26 | `/locations/:type` | `locations/locations.routing.ts:6-11` + `list/location-list.routing.ts:7` | location-list | `PageAccess` | `applicationModuleId: 9, accessItem: 'list'` |
| 27 | `/locations/add/:type` | `locations.routing.ts:12-17` + `add/location-add.routing.ts:6` | location-add | `PageAccess` | `applicationModuleId: 9, accessItem: 'add'` |
| 28 | `/locations/:locationId/:type` | `locations.routing.ts:18-23` + `edit/location-edit.routing.ts:6` | location-edit | `PageAccess` | `applicationModuleId: 9, accessItem: 'edit'` |
| 29 | `/appointment-search` | `app.lazy.routing.ts:104-106` + `search/appointment-search.routing.ts:7-10` | appt-search | `PageAccess` | -- |
| 30 | `/appointment-pending-request` | `app.lazy.routing.ts:112-114` + `detail/appointment-detail.routing.ts:7-11` | appt-detail | `PageAccess` | `applicationModuleId: 6, accessItem: 'add'` |
| 31 | `/appointment-approve-request` | `app.lazy.routing.ts:116-118` + `list/appointment-list.routing.ts:7-12` | appt-list | `PageAccess` | `applicationModuleId: 6, accessItem: 'add'` |
| 32 | `/appointment-documents` (pass-through) | `app.lazy.routing.ts:120-122` + `appointment-documents/appointment-documents.routing.ts:6-11` | appt-docs (pass) | `PageAccess` | `applicationModuleId: 6, accessItem: 'list'` |
| 33 | `/appointment-documents/:appointmentId` | `appointment-documents/list/appointment-document-list.routing.ts:7` | doc-list | `PageAccess` | -- |
| 34 | `/appointment-new-documents/:appointmentId` | `app.lazy.routing.ts:124-126` -> `appointment-new-documents.routing.ts:6-11` -> `list/appointment-new-document-list.routing.ts:7` | new-doc-list | `PageAccess` | `applicationModuleId: 6, accessItem: 'list'` |
| 35 | `/appointment-documents-search` | `app.lazy.routing.ts:128-130` + `appointment-documents/search/appointment-document-search.routing.ts:7-11` | doc-search | `PageAccess` | `applicationModuleId: 7, accessItem: 'list'` |
| 36 | `/appointment-joint-declarations-search` | `app.lazy.routing.ts:132-134` + `search/appointment-joint-declaration-search.routing.ts:7-10` | jd-search | `PageAccess` | -- |
| 37 | `/report` | `app.lazy.routing.ts:136-138` + `search/appointment-request-report-search.routing.ts:9-13` | report-search | `PageAccess` | -- |
| 38 | `/upload-documents/:appointmentId/:type` | `app.lazy.routing.ts:140-142` + `upload-document-by-pass.routing.ts:6-9` + `list/upload-document-by-pass-list.routing.ts:8-11` | upload-list | outer `anonymous: true`, inner `PageAccess` | public magic-link |
| 39 | `/appointment-document-types` | `app.lazy.routing.ts:144-146` + `appointment-document-types.routing.ts:6-11` + `list/appointment-document-type-list.routing.ts:6` | doc-type-list | `PageAccess` | `applicationModuleId: 7, accessItem: 'list'` |
| 40 | `/appointment-document-types/add` | `appointment-document-types.routing.ts:13-17` + `add/appointment-document-type-add.routing.ts:6` | doc-type-add | `PageAccess` | `applicationModuleId: 7, accessItem: 'add'` |
| 41 | `/appointment-document-types/:appointmentDocumentTypeId` | `appointment-document-types.routing.ts:19-23` + `edit/appointment-document-type-edit.routing.ts:6` | doc-type-edit | `PageAccess` | `applicationModuleId: 7, accessItem: 'edit'` |
| 42 | `/appointment-types/add` (possibly unreachable) | `doctor-management/appointment-types/appointment-types.routing.ts:6-11` + `add/appointment-type-add.routing.ts:6` | apt-type-add | `PageAccess` | `applicationModuleId: 9, accessItem: 'add'` |
| 43 | `/appointment-types/:appointmentTypeId` (possibly unreachable) | `appointment-types.routing.ts:13-17` + `edit/appointment-type-edit.routing.ts:6` | apt-type-edit | `PageAccess` | `applicationModuleId: 9, accessItem: 'edit'` |

Note: `appointment-types` has no top-level entry in `APP_LAZY_ROUTES`; entries 42-43 are reachable only if another route injects them. Flagged in Open Q1.

Distinct user-reachable feature URLs (excl. redirect #1, unauthorized #3, and unreachable #42-43): **38**.

## NEW version state

### Root route file

`W:\patient-portal\development\angular\src\app\app.routes.ts:17-114` defines `APP_ROUTES` via Angular 20 `Routes[]`, consumed by `provideRouter(APP_ROUTES)`. Uses standalone `loadComponent` for leaf routes and `children: FOO_ROUTES` to embed per-entity route collections.

### Guards used

- `authGuard` (from `@abp/ng.core`) -- ABP session/token check. Redirects to `/account/login` if no ABP session.
- `permissionGuard` (from `@abp/ng.core`) -- reads `data.requiredPolicy` and checks ABP `PermissionService`.
- No `CanActivatePage` equivalent. No `ChangeDetectionGuard` equivalent.

### Full NEW route inventory

| # | URL path | Defined in (file:line) | Guard | requiredPolicy |
|---|---|---|---|---|
| 1 | empty -> redirect to HomeComponent | `app.routes.ts:17-22` | none | -- |
| 2 | `/dashboard` | `app.routes.ts:23-28` | `authGuard`, `permissionGuard` | (inferred) |
| 3 | `/account/**` | `app.routes.ts:29-32` | ABP built-in | -- |
| 4 | `/gdpr/**` | `app.routes.ts:33-36` | ABP built-in | -- |
| 5 | `/identity/**` | `app.routes.ts:37-40` | ABP built-in | `AbpIdentity.*` |
| 6 | `/language-management/**` | `app.routes.ts:41-44` | ABP built-in | `LanguageManagement.*` |
| 7 | `/saas/**` | `app.routes.ts:45-48` | ABP built-in | `Saas.*` |
| 8 | `/audit-logs/**` | `app.routes.ts:49-52` | ABP built-in | `AbpAuditLogging.*` |
| 9 | `/openiddict/**` | `app.routes.ts:53-56` | ABP built-in | `OpenIddictPro.*` |
| 10 | `/text-template-management/**` | `app.routes.ts:57-61` | ABP built-in | `TextTemplateManagement.*` |
| 11 | `/file-management/**` | `app.routes.ts:62-65` | ABP built-in | `FileManagement.*` |
| 12 | `/gdpr-cookie-consent/privacy` | `gdpr-cookie-consent/gdpr-cookie-consent.routes.ts:4-8` | none (public) | -- |
| 13 | `/gdpr-cookie-consent/cookie` | `gdpr-cookie-consent/gdpr-cookie-consent.routes.ts:9-13` | none (public) | -- |
| 14 | `/setting-management/**` | `app.routes.ts:70-73` | ABP built-in | `SettingManagement.*` |
| 15 | `/configurations/states` | `app.routes.ts:74` -> `states/state/state-routes.ts:4-12` | `authGuard`, `permissionGuard` | `CaseEvaluation.States` |
| 16 | `/appointment-management/appointment-types` | `app.routes.ts:75` -> `appointment-types/appointment-type/appointment-type-routes.ts:4-14` | `authGuard`, `permissionGuard` | `CaseEvaluation.AppointmentTypes` |
| 17 | `/appointment-management/appointment-statuses` | `app.routes.ts:76` -> `appointment-statuses/appointment-status/appointment-status-routes.ts:4-14` | `authGuard`, `permissionGuard` | `CaseEvaluation.AppointmentStatuses` |
| 18 | `/appointment-management/appointment-languages` | `app.routes.ts:77` -> `appointment-languages/appointment-language/appointment-language-routes.ts:4-14` | `authGuard`, `permissionGuard` | `CaseEvaluation.AppointmentLanguages` |
| 19 | `/appointments` | `app.routes.ts:78` -> `appointments/appointment/appointment-routes.ts:5-11` | `authGuard`, `permissionGuard` | `CaseEvaluation.Appointments` |
| 20 | `/appointments/view/:id` | `appointment-routes.ts:12-20` | `authGuard` only | -- |
| 21 | `/appointments/add` | `app.routes.ts:99-102` | `authGuard` only | -- |
| 22 | `/doctor-management/locations` | `app.routes.ts:79` -> `locations/location/location-routes.ts:4-12` | `authGuard`, `permissionGuard` | `CaseEvaluation.Locations` |
| 23 | `/doctor-management/wcab-offices` | `app.routes.ts:80` -> `wcab-offices/wcab-office/wcab-office-routes.ts:4-12` | `authGuard`, `permissionGuard` | `CaseEvaluation.WcabOffices` |
| 24 | `/doctor-management/doctors` | `app.routes.ts:81` -> `doctors/doctor/doctor-routes.ts:4-12` | `authGuard`, `permissionGuard` | `CaseEvaluation.Doctors` |
| 25 | `/doctor-management/doctor-availabilities/generate` | `app.routes.ts:82-89` | `authGuard`, `permissionGuard` | (inferred) |
| 26 | `/doctor-management/doctor-availabilities/add` | `app.routes.ts:90-97` | `authGuard`, `permissionGuard` | (inferred) |
| 27 | `/doctor-management/doctor-availabilities` (base) | `app.routes.ts:111` -> `doctor-availabilities/doctor-availability/doctor-availability-routes.ts:4-13` | `authGuard`, `permissionGuard` | `CaseEvaluation.DoctorAvailabilities` |
| 28 | `/doctor-management/doctor-availabilities/generate` (DUP child) | `doctor-availability-routes.ts:14-21` | `authGuard`, `permissionGuard` | unreachable |
| 29 | `/doctor-management/doctor-availabilities/add` (DUP child) | `doctor-availability-routes.ts:23-30` | `authGuard`, `permissionGuard` | unreachable |
| 30 | `/doctor-management/patients/my-profile` | `app.routes.ts:104-109` | `authGuard` only | -- |
| 31 | `/doctor-management/patients` | `app.routes.ts:112` -> `patients/patient/patient-routes.ts:4-12` | `authGuard`, `permissionGuard` | `CaseEvaluation.Patients` |
| 32 | `/applicant-attorneys` | `app.routes.ts:113` -> `applicant-attorneys/applicant-attorney/applicant-attorney-routes.ts:4-13` | `authGuard`, `permissionGuard` | `CaseEvaluation.ApplicantAttorneys` |

Distinct NEW feature paths: **22** (excluding ABP module mounts in 3-11, 14, which expand via each module's `createRoutes()`).

Duplication note: entries 25-26 duplicate 28-29 -- first match in `app.routes.ts` wins; inner child duplicates unreachable. Flagged Q3.

## Delta

### MVP-blocking gaps (capability present in OLD, absent in NEW)

| gap-id | Capability (OLD route) | Evidence OLD | Evidence NEW absent | Rough effort |
|---|---|---|---|---|
| R-01 | Appointment rescheduled request queue (`/appointment-rescheduled-requests`) | `start/app.lazy.routing.ts:73` + `appointment-change-requests.routing.ts:6-11` | No `change-request` or `rescheduled` in `app.routes.ts` | M |
| R-02 | Appointment cancel request detail (`/appointment-cancel-requests`) | `app.lazy.routing.ts:76-78` | absent | S |
| R-03 | Appointment change log view (`/appointment-change-logs`) | `appointment-change-logs.routing.ts:6-11` (`applicationModuleId: 14`) | absent | M |
| R-04 | Appointment documents list (`/appointment-documents/:appointmentId`) | `appointment-document-list.routing.ts:7` | absent | M |
| R-05 | Appointment new documents list (`/appointment-new-documents/:appointmentId`) | `appointment-new-document-list.routing.ts:7-11` | absent | M |
| R-06 | Appointment document search (`/appointment-documents-search`) | `appointment-document-search.routing.ts:7-11` | absent | S |
| R-07 | Joint declarations search (`/appointment-joint-declarations-search`) | `appointment-joint-declaration-search.routing.ts:7-10` | absent | M |
| R-08 | Report search page (`/report`) | `appointment-request-report-search.routing.ts:9-13` | absent | L |
| R-09 | Upload documents by magic link (`/upload-documents/:appointmentId/:type`) with `anonymous: true` | `app.lazy.routing.ts:140-142` + `upload-document-by-pass-list.routing.ts:8-11` | absent | L |
| R-10 | Appointment pending request detail (`/appointment-pending-request`) | `app.lazy.routing.ts:112-114` + `appointment-detail.routing.ts:7-11` | absent (NEW has only `/appointments/view/:id`) | S |

### Non-MVP gaps (nice-to-have)

| gap-id | Capability (OLD route) | Evidence OLD | Evidence NEW absent | Rough effort |
|---|---|---|---|---|
| R-11 | Appointment approve request list (`/appointment-approve-request`) | `app.lazy.routing.ts:116-118` + `appointment-list.routing.ts:7-12` | absent | S |
| R-12 | Appointment search standalone page (`/appointment-search`) | `app.lazy.routing.ts:104-106` + `appointment-search.routing.ts:7-10` | absent | S |
| R-13 | Doctor edit (`/doctors/:doctorId`) | `doctors.routing.ts:6-11` | NEW `doctor-routes.ts` has only list (`''`); no `:id` edit | S |
| R-14 | Templates list (`/templates`) | `templates.routing.ts:6-11` | absent; NEW has `/text-template-management/**` via Volo module -- verify coverage | S |
| R-15 | System parameters edit (`/system-parameters/:systemParameterId`) | `system-parameters.routing.ts:6-11` | absent; NEW has `/setting-management/**` -- likely equivalent | XS |
| R-16 | Custom fields list (`/custom-fields`) | `custom-fields.routing.ts:6-11` | absent | M |
| R-17 | Documents list (`/documents`) | `documents.routing.ts:6-11` | absent; NEW has `/file-management/**` -- verify | S |
| R-18 | Package details list (`/package-details`) | `package-details.routing.ts:6-11` | absent | M |
| R-19 | Appointment document types CRUD (`/appointment-document-types/**`) | `appointment-document-types.routing.ts:6-23` | absent | M |
| R-20 | AppointmentType add/edit routes | `appointment-types.routing.ts:6-17` | NEW `appointment-type-routes.ts` has only list; no add/edit route -- inline modal likely | S |

### Intentional architectural differences (NOT gaps)

| Topic | OLD approach | NEW approach | Why NEW is different |
|---|---|---|---|
| Lazy-load syntax | Angular 7 `loadChildren: './components/.../foo.module#FooModule'` string-based | Angular 20 `loadComponent: () => import('./foo.component').then(c => c.FooComponent)` + `loadChildren: () => import('@volo/abp.ng.identity').then(c => c.createRoutes())` lambda | Angular 8+ deprecated string lazy-load; standalone APIs canonical in Angular 17+. Seeded diff (frontend build). |
| NgModule vs standalone | Every feature is `declarations: [FooComponent]` in `FooModule` | Components use `standalone: true`; no feature `*.module.ts` files | Angular 20 removes NgModule requirement for routable components. Seeded diff. |
| Auth guards | Custom `CanActivatePage` fetches permission tree from `POST /api/userauthorization`, caches `user.applicationPermission[applicationModuleId][accessItem]` via `route.data.applicationModuleId` / `accessItem` / `rootModuleId` / `childModuleName`. `PageAccess` composes with `ApplicationJsonConfiguration` guard. | `authGuard` + `permissionGuard` from `@abp/ng.core`. Permission resolution via declarative `data.requiredPolicy` string (e.g., `CaseEvaluation.Appointments`) via ABP `PermissionService` | ABP Commercial standard. Seeded diff (Permissions). |
| Permission metadata | `route.data: { rootModuleId: 33, applicationModuleId: 9, accessItem: 'edit', keyName: 'locationId', compositeKeys: [] }` | `route.data: { requiredPolicy: 'CaseEvaluation.Locations' }` or on `ABP.Route[]` menu metadata | Declarative, code-first. See `CaseEvaluationPermissions.cs`. |
| Public/anonymous flag | `route.data: { anonymous: true }` consumed by `CanActivatePage:47-64` (redirects authenticated users away) | NEW has NO explicit `anonymous` flag. Public routes (`/gdpr-cookie-consent/privacy`, `/gdpr-cookie-consent/cookie`) omit `canActivate` entirely. ABP account flows handled by `@volo/abp.ng.account/public` | Seeded diff said "exists in both" -- verification shows it does NOT. Callout in Open Q2. |
| Login/auth UI | `/login`, `/forgot-password`, `/reset-password`, `/verify-email/:userId` inline at `app.lazy.routing.ts:24-50` | Delegated to `@volo/abp.ng.account/public` via `/account/**` | OIDC-standardized. Seeded diff (Auth server). |
| User management | `/users`, `/users/add`, `/users/:userId` custom | Delegated to `@volo/abp.ng.identity` via `/identity/**` | ABP Identity module provides users, roles, claim types, org units, security logs. Seeded diff. |
| Settings | `/system-parameters/:id` custom | Delegated to `@abp/ng.setting-management` via `/setting-management/**` | ABP module covers tenant + host settings. |
| Templates (text) | `/templates` custom | Delegated to `@volo/abp.ng.text-template-management` via `/text-template-management/**` | ABP module covers text templates. |
| File management | `/documents`, `/package-details` custom | Delegated to `@volo/abp.ng.file-management` via `/file-management/**` | ABP module covers file storage + blob providers. |
| Audit logs | `/appointment-change-logs` (per-entity custom log) | Generic `/audit-logs/**` via `@volo/abp.ng.audit-logging` -- all entity changes. Appointment-specific view still a gap (R-03) | ABP audit log is domain-agnostic. |
| Menu/breadcrumb metadata | Inline in `route.data` (`pageName`, no `breadcrumbText`) -- side-bar reads from `user.permissions` | Separate `providers/*-base.routes.ts` export `ABP.Route[]` with `iconClass`, `name` (i18n key), `layout`, `requiredPolicy`, `breadcrumbText`, registered via `RoutesService.add(...)` at bootstrap | ABP menu metadata registered separately from router routes. |

### Extras in NEW (not in OLD)

- `/doctor-management/wcab-offices` -- WCAB office CRUD. No OLD equivalent.
- `/doctor-management/doctor-availabilities/generate` and `/add` -- bulk slot generation. OLD has only one-at-a-time add.
- `/doctor-management/patients/my-profile` -- patient self-service profile page.
- `/appointments/view/:id` -- dedicated read-only view (OLD conflates view/edit).
- `/gdpr`, `/gdpr-cookie-consent/privacy`, `/gdpr-cookie-consent/cookie` -- GDPR compliance pages.
- `/saas/**`, `/openiddict/**`, `/language-management/**`, `/file-management/**`, `/audit-logs/**`, `/text-template-management/**`, `/identity/**`, `/setting-management/**` -- ABP Commercial platform modules. OLD had none.

### Per-feature summary

| Feature family | OLD routes | NEW routes | Missing in NEW |
|---|---|---|---|
| Auth (login/forgot/reset/verify) | 4 | 1 catch-all `/account/**` | 0 (delegated) |
| Dashboard / Home | 2 | 1 (`/dashboard` only; no `/home`) | 1 (home) |
| Users / Identity | 3 | 1 catch-all `/identity/**` | 0 (delegated) |
| Appointments | 6 (add, edit, search, pending, approve, change-log) | 3 (list, view, add) | 3 (search, pending, approve) |
| Appointment change/cancel requests | 2 | 0 | 2 |
| Appointment documents | 5 (list, new-docs, search, joint-decl search, doc-types CRUD) | 0 | 5 |
| Appointment types | 2 (add, edit) | 1 (list only) | 2 |
| Doctors | 1 (edit) | 1 (list) | 1 (edit) |
| Doctor availabilities | 3 (list, add, edit) | 3 (list, generate, add) | 0 |
| Locations | 3 | 1 (list only) | 2 |
| WCAB offices | 0 | 1 | -- extra |
| Patients | 0 | 2 (list, my-profile) | -- extra |
| Applicant attorneys | 0 | 1 | -- extra |
| States / Languages / Statuses | 0 | 3 | -- extras |
| Templates | 1 | 1 (delegated) | 0 |
| Documents / Packages | 2 | 1 (delegated) | verify |
| System parameters / settings | 1 | 1 (delegated) | 0 |
| Custom fields | 1 | 0 | 1 |
| Reports | 1 | 0 | 1 |
| Upload documents (anonymous link) | 1 | 0 | 1 |
| ABP platform modules | 0 | 8 | -- extras |

Summary totals: OLD **32** user-reachable feature URLs; NEW **22** application-feature URLs + 9 ABP module mounts. MVP-blocking missing routes: **10** (R-01 through R-10). Non-MVP missing routes: **10** (R-11 through R-20).

## Open questions

- **Q1:** Are the OLD `/appointment-types/add` and `/appointment-types/:id` routes actually reachable? No `APP_LAZY_ROUTES` entry mounts `appointment-types.module.ts`. No `loadChildren` references it elsewhere. Adrian, please confirm whether these are dead routes in OLD.
- **Q2:** The seeded intentional-differences table claims `route.data.anonymous: true` "exists in both" but NEW has zero occurrences of `anonymous:` as route data -- NEW uses absence of `canActivate` for public routes. Adrian, please confirm whether this pattern difference should be documented as a change rather than "exists in both", or if there is a plan to add an `anonymous` flag to NEW routes.
- **Q3:** NEW's `app.routes.ts:82-97` declares `/doctor-management/doctor-availabilities/generate` and `/add` at root level, but `doctor-availability-routes.ts:14-30` re-declares them as children of `/doctor-management/doctor-availabilities/**`. First-match wins, so child-level duplicates are unreachable. Intentional defensive fallback or leftover scaffolding?
- **Q4:** OLD has `/unauthorized` (static component) but NEW has none. Does NEW rely on ABP's standard 403 redirect, or should a dedicated `/unauthorized` route be added for parity?
- **Q5:** OLD's `/upload-documents/:appointmentId/:type` is a magic-link flow (outer `anonymous: true`). Does NEW MVP include magic-link file upload? High-effort (anonymous + token validation + file upload).
- **Q6:** NEW has `/appointments/view/:id` but no `/appointments/edit/:id`. OLD's `/appointments/:appointmentId` resolves to `AppointmentEditComponent`. Is NEW edit handled by a list-view modal pattern, or is edit a missing route?
- **Q7:** OLD `/home` (lines 36-38) lazy-loads `my-appointment-list.module`; inside, `my-appointment-list.routing.ts:10-13` has a nested `appointments` route -- this is the external-user home. Is this "external user dashboard" structure replicated in NEW, or does NEW flatten external-user navigation to just `/dashboard`?
- **Q8:** NEW declares `/applicant-attorneys` (line 113) top-level, but OLD has no equivalent -- OLD references applicant attorneys only via inline grids on appointment detail pages. Intentional new top-level MVP route, or is applicant attorney meant to remain appointment-sub-data?
