# 08 -- Angular Proxy, Services, Models: Gap Analysis OLD vs NEW

## Summary

- OLD ships **32 handwritten `*.service.ts` API clients** (31 under `src/app/components/**`, 1 `UserAuthorizationService` under `src/app/domain/authorization/`) that thin-wrap a bespoke `RxHttp` helper plus the in-tree `@rx/*` monorepo, and **157 typed files under `src/app/database-models/`** (56 entity classes + 101 `v*` view/record/lookup projections) decorated with `@rx/annotations` validators.
- NEW ships **15 auto-generated ABP proxy services** across 13 feature folders under `angular/src/app/proxy/` plus **22 handwritten `*.abstract.service.ts` / `*.service.ts` view-service wrappers** (11 pairs, each with an extra `*-detail` pair).
- The proxy/service mechanics themselves are an intentional architectural swap (`RxHttp` + stored-proc JSON vs ABP `RestService` + generated DTOs) -- NOT an MVP gap. The functional surface drift IS: 13 OLD services have no NEW equivalent and 7 `@rx/*` UI primitives have no handwritten NEW analogue (NEW relies on `@abp/ng.theme.shared` + LeptonX).
- **MVP risk rating: High** -- 13 MVP-blocking client-service gaps, the most critical being Notes, Users, CustomFields, AppointmentAccessors, AppointmentDocuments (child-scoped), AppointmentInjuryDetails, AppointmentJointDeclarations, AppointmentChangeRequests, and Dashboard.

## Method

- OLD inventory:
  - `find P:\PatientPortalOld\patientappointment-portal\src\app\components -name "*.service.ts"` -- 31 component services
  - `ls P:\PatientPortalOld\patientappointment-portal\src\app\domain\` + `ls P:\PatientPortalOld\patientappointment-portal\src\app\domain\authorization\` -- 1 client-side gate + 3 auth services
  - `ls P:\PatientPortalOld\patientappointment-portal\src\app\database-models\ | wc -l` -- 157 files
  - `ls P:\PatientPortalOld\patientappointment-portal\src\app\components\shared\` -- 4 shared UI components
  - `ls P:\PatientPortalOld\patientappointment-portal\packages\@rx\` -- 13 in-tree monorepo packages
- NEW inventory:
  - Glob `W:\patient-portal\development\angular\src\app\**\*.service.ts` -- 56 service files
  - Glob `W:\patient-portal\development\angular\src\app\proxy\**\models.ts` -- 16 model files
  - `ls W:\patient-portal\development\angular\src\app\shared\components\` -- 1 shared component
  - Read NEW `CLAUDE.md:33` -- "Never edit files in `angular/src/app/proxy/`"
- Timestamp: 2026-04-23.

## OLD version state

### Handwritten services (absolute paths)

- `P:\PatientPortalOld\patientappointment-portal\src\app\domain\access-permission.service.ts:1-144` -- client-side role gate (NOT API client)
- `P:\PatientPortalOld\patientappointment-portal\src\app\domain\authorization\app.service.ts:1-45` -- static JSON config fetch
- `P:\PatientPortalOld\patientappointment-portal\src\app\domain\authorization\user-authorization.service.ts:1-51` -- `api/userauthorization/authorize`, `/logout`, `api/recordlocks*`
- Feature services (`src\app\components\`):
  - `login\login.service.ts:14-38` -- login + forgot-password + email-verify + CSV export
  - `dashboard\dashboard.service.ts:1-20` -- POST `api/Dashboard/post`
  - `appointment-request\appointments\appointments.service.ts:1-79` -- full CRUD + patch + 7-param get
  - `appointment-request\appointment-accessors\appointment-accessors.service.ts:1-74` -- child-scoped `api/Appointments/{id}/AppointmentAccessors`
  - `appointment-request\appointment-change-requests\appointment-change-requests.service.ts:1-82` -- child-scoped + `downloadDocument`
  - `appointment-request\appointment-documents\appointment-documents.service.ts:1-74` -- child-scoped CRUD
  - `appointment-request\appointment-injury-details\appointment-injury-details.service.ts:1-74` -- child-scoped
  - `appointment-request\appointment-joint-declarations\appointment-joint-declarations.service.ts:1-74` -- child-scoped
  - `appointment-request\appointment-new-documents\appointment-new-documents.service.ts:1-110` -- child-scoped + `getBucketName`, `uploadLocalDocument`, `downloadDocument`
  - `appointment-request\appointment-request-report\appointment-request-report.service.ts:1-80` -- CRUD + `exportPdf`
  - `appointment-request\upload-document-by-pass\upload-document-by-pass.service.ts:1-45` -- anonymous token upload
  - `appointment-change-log\appointment-change-logs\appointment-change-logs.service.ts:1-68` -- audit
  - `custom-field\custom-fields\custom-fields.service.ts:1-68` -- CRUD `api/CustomFields`
  - `doctor-management\appointment-types\appointment-types.service.ts:1-68`
  - `doctor-management\doctor-preferred-locations\doctor-preferred-locations.service.ts:1-74`
  - `doctor-management\doctors\doctors.service.ts:1-68`
  - `doctor-management\doctors-appointment-types\doctors-appointment-types.service.ts:1-74`
  - `doctor-management\doctors-availabilities\doctors-availabilities.service.ts:1-74` -- 4-arg composite delete `id/date/locationId`
  - `doctor-management\locations\locations.service.ts:1-68`
  - `doctor-management\locations\wcab.service.ts:1-68`
  - `document\appointment-document-types\appointment-document-types.service.ts:1-68`
  - `document\appointment-documents\appointment-documents.service.ts:1-73` -- ROOT-scoped variant + patch
  - `document\appointment-joint-declarations\appointment-joint-declarations.service.ts:1-71` -- ROOT-scoped variant + patch
  - `document-management\document-packages\document-packages.service.ts:1-74`
  - `document-management\documents\documents.service.ts:1-68`
  - `document-management\package-details\package-details.service.ts:1-68`
  - `note\notes\notes.service.ts:1-95`
  - `system-parameter\system-parameters\system-parameters.service.ts:1-68`
  - `template-management\templates\templates.service.ts:1-68`
  - `user\users\users.service.ts:1-68`
  - `user-query\user-queries\user-queries.service.ts:1-68`

Every service: constructor-injected `RxHttp`; `api` getter returning `AuthorizeApi { api, applicationModuleId, keyName }`; uniform `lookup / group / search / get / getBy / post / put / delete` surface, sometimes with `patch` / `filterLookup` / ad-hoc methods.

### Typed models

- 157 `.ts` files (56 entities + 101 `v*` projections) under `P:\PatientPortalOld\patientappointment-portal\src\app\database-models\`
- `src\app\view-models\*.ts` -- 15 UI scratch models

### Shared UI (`src\app\components\shared\`)

- `appointment-validation/`, `footer-bar/`, `side-bar/side-bar.component.ts`, `top-bar/top-bar.component.ts`

### In-tree `@rx/*` monorepo (`packages\@rx\`, 13 packages)

`annotations`, `common`, `core`, `forms` (currency, datepicker, decimal, dirty, focus, mask, message, multilingual, placeholder, select, tabindex, tag, time, validator), `http`, `linq`, `security`, `storage`, `table` (rx-table/rx-column/rx-footer/rx-cell-template/rx-selectable/rx-table-detail-template/rx-permission-item-template), `util`, `view` (collapse, dialog, html, label, permission, popover, popup, remove, spinner, tab, template, toast, tooltip).

## NEW version state

### Auto-generated proxy (DO NOT EDIT) -- `W:\patient-portal\development\angular\src\app\proxy\`

Folders: `applicant-attorneys/`, `appointment-languages/`, `appointment-statuses/`, `appointment-types/`, `appointments/`, `books/`, `doctor-availabilities/`, `doctors/`, `enums/`, `locations/`, `patients/`, `shared/`, `states/`, `users/`, `volo/abp/{content,identity}`, `volo/saas/host/dtos/`, `wcab-offices/`, plus `index.ts`, `generate-proxy.json`, `README.md`.

Services (15):

1. `applicant-attorneys/applicant-attorney.service.ts`
2. `appointment-languages/appointment-language.service.ts`
3. `appointment-statuses/appointment-status.service.ts`
4. `appointment-types/appointment-type.service.ts`
5. `appointments/appointment.service.ts:22-204` (CRUD + `getAppointmentTypeLookup` + `getDoctorAvailabilityLookup` + `getDownloadToken` + `getFile` + `getIdentityUserLookup` + `getLocationLookup` + `getPatientLookup` + `getWithNavigationProperties` + `uploadFile`)
6. `books/book.service.ts` (scaffolding sample)
7. `doctor-availabilities/doctor-availability.service.ts:20-157` (CRUD + `deleteByDate` + `deleteBySlot` + `generatePreview`)
8. `doctors/doctor.service.ts:16-143`
9. `doctors/doctor-tenant.service.ts`
10. `locations/location.service.ts:16-150` (CRUD + `deleteAll` + `deleteByIds`)
11. `patients/patient.service.ts`
12. `states/state.service.ts`
13. `users/user-extended.service.ts`
14. `wcab-offices/wcab-office.service.ts`

All follow: `@Injectable({ providedIn: 'root' })`, `private restService = inject(RestService)`, `apiName = 'Default'`, arrow-function properties.

### Handwritten view-services -- `angular\src\app\<feature>\<entity>\services\`

22 files across 11 features (pair: `<entity>.service.ts` extends `Abstract...ViewService` from `<entity>.abstract.service.ts`; plus analogous `<entity>-detail.*.service.ts`). Example evidence: `appointments\appointment\services\appointment.service.ts:1-5` (empty body) + `appointment.abstract.service.ts:11-52` (injects `AppointmentService` proxy + `ConfirmationService` + ABP `ListService`; exposes `delete`, `hookToQuery`, `clearFilters`).

Features: applicant-attorney, appointment-language, appointment-status, appointment-type, appointment, doctor-availability, doctor, location, patient, state, wcab-office.

### Generated models (`proxy/**/models.ts`, 16 files)

Per-feature DTOs: `<Entity>CreateDto`, `<Entity>Dto`, `<Entity>UpdateDto`, `<Entity>WithNavigationPropertiesDto`, `Get<Entity>Input`. Shared: `AppFileDescriptorDto`, `DownloadTokenResultDto`, `GetFileInput`, `LookupDto<TKey>`, `LookupRequestDto`. Enums in `proxy/enums/`: `AppointmentStatusType`, `BookingStatus`, `Gender`, `PhoneNumberType`. `volo/abp/identity/models.ts` = `IdentityUserDto`; `volo/saas/host/dtos/models.ts` = `SaasTenantDto`.

### Shared components (`angular\src\app\shared\components\`)

Exactly ONE: `top-header-navbar\top-header-navbar.component.ts:1-34` (standalone, inputs: tenantName/userName/roleName/showProfile/showHelp/showLogout; outputs: profileClick/helpClick/logoutClick). NEW relies on `@abp/ng.theme.shared` + `@abp/ng.theme.lepton-x` for tables/modals/toasts/buttons.

## Delta

### Service inventory (API-surface name | OLD file:line | NEW proxy file:line or "absent" | drift notes)

| API surface | OLD service file:line | NEW proxy file:line | Drift |
|---|---|---|---|
| Appointments root CRUD | `src\app\components\appointment-request\appointments\appointments.service.ts:1-79` | `angular\src\app\proxy\appointments\appointment.service.ts:22-204` | NEW adds `uploadFile`, `getFile`, `getDownloadToken`, nav-prop getter, 6 lookups; OLD uses loose 7-param get |
| Appointments.patch | `appointments.service.ts:69-71` | absent | PATCH missing on NEW (use PUT) |
| AppointmentAccessors | `appointment-request\appointment-accessors\appointment-accessors.service.ts:1-74` | absent | MVP-blocking (A8-04) |
| AppointmentChangeRequests | `appointment-request\appointment-change-requests\appointment-change-requests.service.ts:1-82` | absent | MVP-blocking (A8-09) |
| AppointmentDocuments (child) | `appointment-request\appointment-documents\appointment-documents.service.ts:1-74` | absent | MVP-blocking (A8-05) |
| AppointmentDocuments (root) | `document\appointment-documents\appointment-documents.service.ts:1-73` | absent | MVP-blocking (A8-05) |
| AppointmentInjuryDetails | `appointment-request\appointment-injury-details\appointment-injury-details.service.ts:1-74` | absent | MVP-blocking (A8-07) |
| AppointmentJointDeclarations (child) | `appointment-request\appointment-joint-declarations\appointment-joint-declarations.service.ts:1-74` | absent | MVP-blocking (A8-08) |
| AppointmentJointDeclarations (root) | `document\appointment-joint-declarations\appointment-joint-declarations.service.ts:1-71` | absent | Duplicate absent |
| AppointmentNewDocuments | `appointment-request\appointment-new-documents\appointment-new-documents.service.ts:1-110` | Partial: `appointments\appointment.service.ts:195-203` (uploadFile) | MVP-blocking; NEW has generic upload but no S3 bucket negotiation (A8-06) |
| AppointmentRequestReport + exportPdf | `appointment-request\appointment-request-report\appointment-request-report.service.ts:1-80` | absent | MVP-blocking (A8-13) |
| AppointmentChangeLogs | `appointment-change-log\appointment-change-logs\appointment-change-logs.service.ts:1-68` | absent | MVP-blocking (A8-10) |
| AppointmentDocumentTypes | `document\appointment-document-types\appointment-document-types.service.ts:1-68` | absent | Non-MVP (A8-18) |
| AppointmentTypes | `doctor-management\appointment-types\appointment-types.service.ts:1-68` | `proxy\appointment-types\appointment-type.service.ts` | Parity |
| AppointmentLanguages | absent in OLD as dedicated service; lookup-only | `proxy\appointment-languages\appointment-language.service.ts` | Extra in NEW |
| AppointmentStatuses | absent in OLD; embedded in Appointment | `proxy\appointment-statuses\appointment-status.service.ts` | Extra in NEW |
| ApplicantAttorneys | OLD uses `appointment-patient-attorney` + `appointment-defense-attorney` embedded in `database-models\`; NO dedicated OLD service | `proxy\applicant-attorneys\applicant-attorney.service.ts` | Extra in NEW (modelled as first-class entity) |
| Books | absent in OLD | `proxy\books\book.service.ts` | ABP scaffolding sample |
| CustomFields | `custom-field\custom-fields\custom-fields.service.ts:1-68` | absent | MVP-blocking (A8-03) |
| Dashboard | `dashboard\dashboard.service.ts:1-20` | absent | MVP-blocking (A8-11) |
| Doctors | `doctor-management\doctors\doctors.service.ts:1-68` | `proxy\doctors\doctor.service.ts:16-143` | Parity; NEW adds nav-prop getter, 5 lookups |
| DoctorTenant (tenant-provisioning) | absent | `proxy\doctors\doctor-tenant.service.ts` | Extra in NEW |
| DoctorsAvailabilities | `doctor-management\doctors-availabilities\doctors-availabilities.service.ts:1-74` | `proxy\doctor-availabilities\doctor-availability.service.ts:20-157` | Parity+; NEW adds deleteByDate/deleteBySlot/generatePreview |
| DoctorsAppointmentTypes (M2M) | `doctor-management\doctors-appointment-types\doctors-appointment-types.service.ts:1-74` | Collapsed into `doctor.service.getAppointmentTypeLookup` | Partial collapse (A8-20) |
| DoctorPreferredLocations (M2M) | `doctor-management\doctor-preferred-locations\doctor-preferred-locations.service.ts:1-74` | Collapsed into `doctor.service.getLocationLookup` | Partial collapse (A8-19) |
| Documents | `document-management\documents\documents.service.ts:1-68` | absent | Non-MVP (A8-17) |
| DocumentPackages | `document-management\document-packages\document-packages.service.ts:1-74` | absent | Non-MVP (A8-17) |
| PackageDetails | `document-management\package-details\package-details.service.ts:1-68` | absent | Non-MVP (A8-17) |
| Locations | `doctor-management\locations\locations.service.ts:1-68` | `proxy\locations\location.service.ts:16-150` | Parity+; NEW adds deleteAll/deleteByIds |
| Login + forgot-password + email-verify | `login\login.service.ts:14-38` | absent (OpenIddict used for login; no Angular client wrapper for account flows) | MVP-blocking (A8-12) |
| Notes | `note\notes\notes.service.ts:1-95` | absent | MVP-blocking (A8-01) |
| States | implicit lookup | `proxy\states\state.service.ts` | Extra in NEW |
| SystemParameters | `system-parameter\system-parameters\system-parameters.service.ts:1-68` | absent | Non-MVP (A8-15); replaced by ABP SettingManagement |
| Templates | `template-management\templates\templates.service.ts:1-68` | absent | Non-MVP (A8-14) |
| UploadDocumentByPass (anon) | `appointment-request\upload-document-by-pass\upload-document-by-pass.service.ts:1-45` | absent | Non-MVP (A8-21) |
| UserAuthorization + record locks | `domain\authorization\user-authorization.service.ts:1-51` | absent (`ConcurrencyStamp` partial) | Intentional (A8-22) |
| Users | `user\users\users.service.ts:1-68` | `proxy\users\user-extended.service.ts` (ABP Identity extension only) | MVP-blocking (A8-02) |
| UserQueries | `user-query\user-queries\user-queries.service.ts:1-68` | absent | Non-MVP (A8-16) |
| WcabOffices | `doctor-management\locations\wcab.service.ts:1-68` | `proxy\wcab-offices\wcab-office.service.ts` | Parity |

### Model/DTO inventory (abridged; see full table in subagent's raw output)

Key drift patterns:
- Primary key int -> GUID across all entities
- `@rx/annotations` decorators removed; validation moved server-side
- 101 `v*` projection TS files eliminated in favor of `<Entity>WithNavigationPropertiesDto` + Mapperly
- Enum-like `Status` / `Role` constants in OLD `src/app/const/` replaced by generated enums in `proxy/enums/`
- `Patient.isInterpreter`/`isOther` flags in OLD made implicit (derived) in NEW + tenant scope added
- `AppointmentPatientAttorney` + `AppointmentDefenseAttorney` consolidated into `ApplicantAttorney`
- OLD `vUser`/`vPatient`/etc projections map to NEW `<Entity>WithNavigationPropertiesDto` envelope

Missing NEW models: AppointmentAccessor, AppointmentChangeLog, AppointmentChangeRequest, AppointmentDocument, AppointmentDocumentType, AppointmentInjuryDetail, AppointmentJointDeclaration, AppointmentNewDocument, AppointmentRequestReport, CustomField/CustomFieldsValue, Note, SystemParameter, Template, UserQuery, Document/DocumentPackage/PackageDetail.

### Shared-component inventory (component | OLD source | NEW source or "absent" | notes)

| Component | OLD source | NEW source | Notes |
|---|---|---|---|
| Sidebar (role-aware nav) | `src\app\components\shared\side-bar\side-bar.component.ts:1-30` | absent (LeptonX shell) | LeptonX provides full layout sidebar; OLD's role-based `MODULES` array gating replaced by `*abpPermission` directive |
| Top bar / header navbar | `src\app\components\shared\top-bar\top-bar.component.ts:1-30` | `angular\src\app\shared\components\top-header-navbar\top-header-navbar.component.ts:1-34` | Partial; NEW is simpler, no user-query popup trigger |
| Footer bar | `src\app\components\shared\footer-bar\` | absent | Minor; LeptonX footer |
| Appointment validation panel | `src\app\components\shared\appointment-validation\appointment-validation.component.ts` | absent | Domain-specific; NEW uses Angular reactive-forms errors inline |
| Date picker (day / month / year / multi) | `packages\@rx\forms\datepicker\rx_{date,month,year,picker}_control_component.ts` | absent (ABP/LeptonX uses NgBootstrap datepicker) | Verify multi-year picker parity |
| Grid / table | `packages\@rx\table\rx-table.component.ts`, `rx-column.component.ts`, `rx-footer.component.ts`, `rx-cell-template.directive.ts`, `rx-selectable.directive.ts` | absent (ABP `AbpTable` + NgBootstrap/LeptonX theme) | Verify inline-edit, row-detail, selectable rows |
| Dialog | `packages\@rx\view\dialog\dialog.service.ts`, `rx_dialog_control_component.ts` | absent (ABP `ConfirmationService` in `@abp/ng.theme.shared`) | Intentional replacement |
| Popup | `packages\@rx\view\popup\popup.service.ts`, `rx_popup_control_component.ts`, `unauthorized-access.component.ts`, `validation-failed.component.ts` | absent | LeptonX modals cover most; unauthorized page is in NEW app routes |
| Toast | `packages\@rx\view\toast\toast_service.ts`, `rx_toast_control_component.ts`, `toasts.ts` | absent (ABP `ToasterService`) | Intentional |
| Select / combobox | `packages\@rx\forms\select\rx_select_control_component.ts` | absent (NgBootstrap + ABP `AbpSelect`) | Intentional |
| Validator | `packages\@rx\forms\validator\validators\*`, `rxvalidation.service.ts` | absent (Angular `Validators`) | Intentional |
| Tag input | `packages\@rx\forms\tag\rx_tag_control_component.ts` | absent | Verify if used in OLD-only admin screens |
| Spinner / tooltip / popover / tab / collapse | `packages\@rx\view\{spinner,tooltip,popover,tab,collapse}\` | absent (NgBootstrap) | Intentional |
| Permission directive | `packages\@rx\security\rx_authorization_control_directive.ts` | `*abpPermission` structural directive from `@abp/ng.core` | Intentional replacement |

### MVP-blocking gaps (13)

| gap-id | capability | evidence-old | evidence-new-absent | effort |
|---|---|---|---|---|
| A8-01 | Notes CRUD | `note\notes\notes.service.ts:1-95` | no `proxy/notes/`; `proxy\index.ts:1-34` omits | M |
| A8-02 | Users admin CRUD | `user\users\users.service.ts:1-68` | only `user-extended.service.ts` wrapper | S |
| A8-03 | CustomFields CRUD | `custom-field\custom-fields\custom-fields.service.ts:1-68` | no proxy | L |
| A8-04 | AppointmentAccessors | `appointment-request\appointment-accessors\appointment-accessors.service.ts:1-74` | no proxy (entity exists backend) | S |
| A8-05 | AppointmentDocuments (both variants) | `appointment-request\appointment-documents\...:1-74` + `document\appointment-documents\...:1-73` | no proxy | M |
| A8-06 | AppointmentNewDocuments (S3/local upload) | `appointment-request\appointment-new-documents\appointment-new-documents.service.ts:44-109` | `proxy\appointments\appointment.service.ts:195-203` (generic upload only) | M-L |
| A8-07 | AppointmentInjuryDetails | `...appointment-injury-details...:1-74` | no proxy | S |
| A8-08 | AppointmentJointDeclarations (both variants) | `...appointment-joint-declarations...:1-74` + `document\...:1-71` | no proxy | S |
| A8-09 | AppointmentChangeRequests | `...appointment-change-requests...:1-82` | no proxy | M |
| A8-10 | AppointmentChangeLogs (audit) | `...appointment-change-logs...:1-68` | no proxy | S |
| A8-11 | Dashboard | `dashboard\dashboard.service.ts:1-20` | no AppService, no proxy | S |
| A8-12 | Login flows (forgot-pw, email-verify) | `login\login.service.ts:14-38` | OIDC login covered; no client wrapper for account flows | S |
| A8-13 | AppointmentRequestReport + exportPdf | `...appointment-request-report...:1-80` | no proxy | M |

### Non-MVP gaps (10)

- A8-14 Templates CRUD (M)
- A8-15 SystemParameters CRUD (S; mostly replaced by ABP SettingManagement)
- A8-16 UserQueries (M; `top-header-navbar.helpClick` has no backing service)
- A8-17 DocumentPackages + PackageDetails + Documents (M-L)
- A8-18 AppointmentDocumentTypes lookup (S)
- A8-19 DoctorPreferredLocations M2M (S; may be collapsed into `doctor.getLocationLookup`)
- A8-20 DoctorsAppointmentTypes M2M (S; may be collapsed into `doctor.getAppointmentTypeLookup`)
- A8-21 UploadDocumentByPass (anon uploads) (L)
- A8-22 Record-locking (N/A; ABP uses `ConcurrencyStamp`)
- A8-23 UI view-models scratch folder (N/A; style)

### Intentional architectural differences (8)

| topic | OLD | NEW | why |
|---|---|---|---|
| HTTP client | Handwritten `RxHttp` services, `AuthorizeApi` | `abp generate-proxy` + `RestService` | CLAUDE.md:33; generator reads Swagger |
| UI primitives | In-tree `@rx/*` monorepo (13 packages) | `@abp/ng.theme.shared` + `@abp/ng.theme.lepton-x` | Sanctioned ABP Commercial theme |
| Typed models | `@rx/annotations` decorators on classes | Plain TS interfaces; validation on backend | 2018 pattern superseded |
| View projections | 101 `v*` TS classes mirroring SQL views / stored-procs | `<Entity>WithNavigationPropertiesDto` + Mapperly | Stored-procs gone |
| Authorization scope | `AuthorizeApi { applicationModuleId }` numeric IDs + `dbo.spPermissions` JSON | `[Authorize(CaseEvaluationPermissions.X.Y)]` + `*abpPermission` directive | ADR; OIDC |
| RxJS | `rxjs/Rx` v5 | `rxjs` v7+ with `ListService.hookToQuery` | v5 deprecated |
| Shared components | Hand-rolled `side-bar`/`top-bar`/`footer-bar` | LeptonX layout shell | Theme provides everything |
| Model imports | Barrel `from 'src/app/database-models'` | Feature-scoped `from '../../proxy/<feature>/models'` | Generator convention |

### Extras in NEW

- `proxy\doctors\doctor-tenant.service.ts` -- tenant-provisioning API
- `proxy\users\user-extended.service.ts` -- ABP Identity extension
- `proxy\shared\models.ts` -- `AppFileDescriptorDto`, `DownloadTokenResultDto`, `GetFileInput`, `LookupDto<TKey>`, `LookupRequestDto` (typed lookup contracts)
- `proxy\enums\*.enum.ts` -- proper TS enums
- `proxy\volo\abp\identity\models.ts` -- `IdentityUserDto` first-class
- `proxy\volo\saas\host\dtos\models.ts` -- `SaasTenantDto` for tenant nav-props
- ABP `ConfirmationService` wired into every handwritten abstract view-service for uniform delete-confirmation UX

## Open questions

1. Adrian, please clarify: are the document-packaging features (Documents, DocumentPackages, PackageDetails, AppointmentDocumentTypes, AppointmentJointDeclarations, AppointmentNewDocuments) MVP-required, or did the rewrite intentionally drop the template-packaging workflow?
2. Adrian, please clarify: does NEW's MVP include CustomFields (dynamic per-tenant form fields)?
3. Adrian, please clarify: are forgot-password / email-verification on the MVP critical path? ABP Identity provides endpoints but no Angular client wrapper exists yet in NEW.
4. Adrian, please clarify: are the TWO variants of `AppointmentDocumentsService` (root-scoped vs child-scoped) both in use in OLD?
5. Adrian, please clarify: does NEW need record-locking (`api/recordlocks`) or is `ConcurrencyStamp` sufficient?
6. Adrian, please clarify: is `AppointmentRequestReport.exportPdf` MVP? Which format (CSV/PDF/XLSX)?
7. Adrian, please clarify: is the anonymous UploadDocumentByPass flow part of MVP?
8. Adrian, please clarify: are in-app UserQueries still wanted? `top-header-navbar.helpClick` has no backing service.
9. Adrian, please clarify: should Templates + SystemParameters remain UI-administered or be replaced by ABP `SettingManagement`?
10. Adrian, please clarify: confirm the `<entity>-detail.service.ts` / `-detail.abstract.service.ts` pairs are the scaffolded "drawer / detail view" wrappers per ABP's pattern, not unfinished scaffolding.
