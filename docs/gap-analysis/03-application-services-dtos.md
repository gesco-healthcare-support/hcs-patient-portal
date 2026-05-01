# 03 -- Application Services + DTOs: Gap Analysis OLD vs NEW

## Summary

OLD exposes ~50 ASP.NET Core MVC controllers (52 files under `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\`) driving ~100 distinct operations across 27 domain entities, with heavy reliance on stored-procedure-backed list/search endpoints (9 `*SearchController.cs` wrappers over `spm.*` SPs) and `spm` view-tables for read models. NEW exposes 17 `IAppService` interfaces (101 `.cs` files in `Application.Contracts/`) covering 15 entities + 2 meta-services (`IExternalSignupAppService`, `IDoctorTenantAppService` via ABP SaaS). NEW ships full CRUD + WithNavigationProperties + Lookup methods for the 10 entities it covers, plus one bespoke flow (attorney upsert on appointment). OLD carries ~27 gap-worthy operations that NEW does not implement. **MVP risk rating: High** -- the document-management, injury-detail, change-request, and notification-scheduler families are all absent from NEW, and the Angular booking flow in OLD calls every one of them.

## Method

1. Enumerated OLD controllers via `Glob P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\**\*.cs` (50 files).
2. Read each controller for `[HttpGet/Post/Put/Patch/Delete]` actions.
3. Read `PatientAppointment.Models\ViewModels\**\*.cs` (21 view models).
4. Enumerated NEW via `Glob W:\patient-portal\development\src\HealthcareSupport.CaseEvaluation.Application\**\*AppService.cs` (17 services) + `Application.Contracts\**\*.cs` (101 files).
5. Read all 11 NEW `I*AppService.cs` interfaces.
6. Cross-checked `W:\patient-portal\development\docs\backend\APPLICATION-SERVICES.md` against actual `*AppService.cs` implementations.

Timestamp: 2026-04-23.

## OLD version state

### Controllers by functional area

| Folder | Controller count | Representative routes |
|---|---|---|
| `AppointmentChangeLog/` | 2 | `api/appointmentchangelogs`, `api/appointmentchangelogs/search` |
| `AppointmentRequest/` | 9 | `api/appointments`, child-scoped documents, accessors, change-requests, injury-details, joint-declarations, new-documents, request-reports |
| `Core/` | 7 | `api/userauthentication/login`, `api/userauthorization`, `api/dashboard`, `api/scheduler`, `api/applicationconfigurations`, `api/documentupload`, `api/documentdownload` |
| `CustomField/` | 1 | `api/customfields` |
| `DoctorManagement/` | 7 | `api/doctors`, `api/locations`, `api/wcaboffices`, `api/appointmenttypes`, etc. |
| `Document/` | 6 | `api/appointmentdocuments`, `api/appointmentdocumenttypes`, search variants |
| `DocumentManagement/` | 3 | `api/documents`, `api/packagedetails`, `api/packagedetails/{id}/documentpackages` |
| `Export/` | 1 | `api/csvexport` |
| `Lookups/` | 9 | 27 lookup endpoints |
| `Note/` | 1 | `api/notes` |
| `SystemParameter/` | 1 | `api/systemparameters` |
| `TemplateManagement/` | 1 | `api/templates` |
| `User/` | 1 | `api/users` |
| `UserQuery/` | 1 | `api/userqueries` |

### OLD operation shape -- canonical CRUD template

Most entity controllers follow a rigid template (example: `DoctorsController.cs:26-72`):
- `GET api/doctors` -> `Domain.Doctor.Get()` (all rows, no paging)
- `GET api/doctors/{id}`
- `POST api/doctors` (validate + Add)
- `PUT api/doctors/{id}` (validate + Update)
- `PATCH api/doctors/{id}` (JsonPatchDocument<Doctor> -> Put)
- `DELETE api/doctors/{id}`

### OLD DTO/ViewModel inventory

21 view models in `PatientAppointment.Models\ViewModels\`:
- `AppointmentDetailViewModel`, `AppointmentStackHoldersEmailPhone`, `ChangeCredentialViewModel`, `ConfigurationContentViewModel`, `DoctorsAvailabilityViewModel`, `DoctorsAvailabilitySlotsViewModel`, `ExportKeyViewModel`, `ExportViewModel`, `InCompleteDocumentNotificationViewModel`, `ModuleMasterViewModel`, `NotificaionTemplateViewModel`, `ReportViewModel`, `StoreProcSearchViewModel`, `UserAuthenticationViewModel`, `UserCredentialViewModel`, `UserQueryAppointmentDetailViewModel`, `vEmailSenderViewModel`, `VerifyUser`, scheduler payload shapes.

**Key observation:** OLD has no generic `ListResultDto`/`PagedResultDto` -- every SP-backed list endpoint returns a raw JSON string in `StoreProcSearchViewModel.Result`. Paging via `pageIndex` + `rowCount` query params; sorting via `orderByColumn`+`sortOrder`. No `*CreateDto`/`*UpdateDto`; controllers bind directly to EF entity types.

## NEW version state

### Application services (17)

| Service | Interface | Methods |
|---|---|---|
| AppointmentsAppService | `IAppointmentsAppService.cs:10-39` | 14 methods |
| PatientsAppService | `IPatientsAppService.cs:10-28` | 17 methods |
| DoctorAvailabilitiesAppService | `IDoctorAvailabilitiesAppService.cs:10-23` | 12 methods |
| ExternalSignupAppService | `IExternalSignupAppService.cs:9-16` | 4 methods |
| DoctorTenantAppService | extends `TenantAppService` | Overrides `CreateAsync(SaasTenantCreateDto)` |
| DoctorsAppService | `IDoctorsAppService.cs:10-22` | 10 methods |
| UserExtendedAppService | extends `IdentityUserAppService` | Overrides `UpdateAsync` |
| ApplicantAttorneysAppService | `IApplicantAttorneysAppService.cs:10-20` | 9 methods |
| AppointmentAccessorsAppService | `IAppointmentAccessorsAppService.cs:10-20` | 9 methods |
| AppointmentApplicantAttorneysAppService | `IAppointmentApplicantAttorneysAppService.cs:10-21` | 10 methods |
| AppointmentEmployerDetailsAppService | `IAppointmentEmployerDetailsAppService.cs:10-20` | 9 methods |
| AppointmentLanguagesAppService | `IAppointmentLanguagesAppService.cs:9-16` | 5 methods |
| AppointmentStatusesAppService | `IAppointmentStatusesAppService.cs:9-18` | 7 methods (bulk delete) |
| AppointmentTypesAppService | `IAppointmentTypesAppService.cs:9-16` | 5 methods |
| LocationsAppService | `ILocationsAppService.cs:10-22` | 10 methods (bulk delete) |
| StatesAppService | `IStatesAppService.cs:9-16` | 5 methods |
| WcabOfficesAppService | `IWcabOfficesAppService.cs:11-24` | 10 methods (Excel export, bulk delete, download token) |
| BookAppService | `IBookAppService.cs` | ABP scaffolding demo |

### NEW DTO inventory (101 files)

Per-feature (typical set): `<Entity>Dto`, `<Entity>CreateDto`, `<Entity>UpdateDto`, `<Entity>WithNavigationPropertiesDto`, `Get<Entity>Input`, `I<Entity>AppService`.

Special shapes:
- `AppointmentTypeExcelDto`, `AppointmentTypeExcelDownloadDto`
- `ApplicantAttorneyDetailsDto` (attorney upsert on appointment)
- `CreatePatientForAppointmentBookingInput`
- `DoctorAvailabilityGenerateInputDto`, `DoctorAvailabilitySlotsPreviewDto`, `DoctorAvailabilityDeleteBySlotInputDto`, `DoctorAvailabilityDeleteByDateInputDto`
- `ExternalUserLookupDto`, `ExternalUserProfileDto`, `ExternalUserSignUpDto`, `ExternalUserType` enum
- `LookupDto<Guid>` (`Id`, `DisplayName` uniform across all lookups)
- `LookupRequestDto`, `DownloadTokenResultDto`

### NEW DTO shape example (`AppointmentDto.cs:9-37`)

`FullAuditedEntityDto<Guid>` + `IHasConcurrencyStamp` with: `PanelNumber?`, `AppointmentDate`, `IsPatientAlreadyExist`, `RequestConfirmationNumber` (required), `DueDate?`, `InternalUserComments?`, `AppointmentApproveDate?`, `AppointmentStatus` (enum), `PatientId`, `IdentityUserId`, `AppointmentTypeId`, `LocationId`, `DoctorAvailabilityId`, `ConcurrencyStamp`.

`GetAppointmentsInput.cs:7-31` extends `PagedAndSortedResultRequestDto` with `FilterText`, `PanelNumber`, `AppointmentDateMin/Max`, `IdentityUserId`, `AccessorIdentityUserId`, `AppointmentTypeId`, `LocationId`.

### ABP conventions already in place

- Class-level `[Authorize]` + per-method permissions
- `PagedResultDto<T>` / `ListResultDto<T>` for all lists
- Uniform `LookupDto<Guid>` for dropdowns
- Concurrency via `IHasConcurrencyStamp`
- Bulk delete on AppointmentStatuses, Locations, WcabOffices
- Excel export on WcabOffices with download-token CSRF pattern

## Delta

### Per-entity operations matrix (summary)

Complete CRUD + WithNav + Lookups in NEW: Appointment, AppointmentAccessor, AppointmentEmployerDetail, ApplicantAttorney, AppointmentApplicantAttorney, AppointmentType, AppointmentStatus, AppointmentLanguage, State, Doctor, DoctorAvailability, Location, WcabOffice, Patient (17 methods).

**Entities entirely absent in NEW (MVP-blocking):**
- AppointmentChangeRequest (reschedule/cancel workflow)
- AppointmentChangeLog (audit log)
- AppointmentDocument (nested + flat variants)
- AppointmentDocumentType
- AppointmentNewDocument (S3 upload)
- AppointmentInjuryDetail + sub-collections
- AppointmentJointDeclaration
- AppointmentRequestReport
- AppointmentDefenseAttorney
- Note
- CustomField
- Template
- Document, DocumentPackage, PackageDetail
- UserQuery
- DoctorsAppointmentType, DoctorPreferredLocation (standalone M2M)
- SystemParameter

### MVP-blocking gaps (capability present in OLD, absent in NEW)

| gap-id | capability | evidence-old | evidence-new-absent | effort |
|---|---|---|---|---|
| 03-G01 | AppointmentDocument CRUD + SendDocumentEmail on update | `Document\AppointmentDocumentsController.cs:34-69` + search | no IAppointmentDocumentsAppService | 2-3 days |
| 03-G02 | AppointmentDocumentType lookup + CRUD | `AppointmentDocumentTypesController.cs:31-72` + search | absent | 1 day |
| 03-G03 | AppointmentNewDocument -- file upload (S3/local) | `AppointmentNewDocumentsController.cs:40-132` | absent | 3-5 days |
| 03-G04 | AppointmentInjuryDetail CRUD + 3 sub-collections | `AppointmentInjuryDetailsController.cs:31-71` + sub-expansion | absent | 4-6 days |
| 03-G05 | AppointmentChangeRequest (reschedule/cancel workflow) | `AppointmentChangeRequestsController.cs:25-82` with UpdateDocument branch | absent | 3-4 days |
| 03-G06 | AppointmentJointDeclaration + approve email | 2 OLD controllers + search | absent | 2-3 days |
| 03-G07 | AppointmentRequestReport entity | `AppointmentRequestReportController.cs:29-52` | absent | 1-2 days |
| 03-G08 | Dashboard counters | `DashboardController.cs:45-54` | absent | 1-2 days |
| 03-G09 | Scheduler trigger + 9 notification types | `SchedulerController.cs:41-48`, `SchedulerDomain.cs` | absent | 5-7 days |
| 03-G10 | Note CRUD | `NotesController.cs:37-77` | absent | 1 day |
| 03-G11 | CSV/Excel/PDF export (generic) | `CSVExportController.cs:42-116` | NEW has only per-entity Excel (WcabOffices); AppointmentTypes DTOs present but no interface method | 3-5 days |
| 03-G12 | CustomField CRUD | `CustomFieldsController.cs:32-72` | absent | 2 days |
| 03-G13 | Template CRUD | `TemplatesController.cs:37-77` | absent | 2 days |
| 03-G14 | UserQuery | `UserQueriesController.cs:36-77` | absent | 1-2 days |

### Non-MVP gaps

| gap-id | capability | effort |
|---|---|---|
| 03-N01 | AppointmentChangeLog CRUD + search | 1 day (surface ABP audit logs) |
| 03-N02 | DocumentPackage / PackageDetail CRUD | 3 days |
| 03-N03 | Document (library template) CRUD | 2 days |
| 03-N04 | SystemParameter CRUD | 1 day (use ABP SettingManagement) |
| 03-N05 | DoctorsAppointmentType + DoctorPreferredLocation standalone M2M | N/A (folded into Doctor in NEW) |
| 03-N06 | ApplicationConfigurations i18n | N/A (ABP localization replaces) |
| 03-N07 | DocumentDownload generic file download | 1-2 days (security review needed) |
| 03-N08 | Lookups: MonthTypeLookups, CityLookups, PhoneNumberTypeLookups | 1 day |
| 03-N09 | Lookups: InternalUserName / ExternalUserRole filters | 1 day |
| 03-N10 | PATCH verb (JsonPatchDocument) | N/A (ABP convention is full PUT) |
| 03-N11 | Appointment PATCH expansion of sub-collections | Tied to 03-G04 |

### Intentional architectural differences (NOT gaps)

| topic | OLD | NEW | Why |
|---|---|---|---|
| Controllers vs AppService layering | Controllers embed Uow + Domain directly; DTOs = EF entities | AppService is the business seam; controllers are thin wrappers | Per ADR 002 |
| List endpoint shape | SP returns JSON string -> `StoreProcSearchViewModel.Result` | `PagedResultDto<T>` + `PagedAndSortedResultRequestDto` input + LINQ | Typed contract |
| Mapping strategy | Manual property copies in Domain services | Riok.Mapperly source-gen | Per ADR 001 |
| Lookup shape | `IQueryable<v*LookUp>` view rows (entity-specific) | Uniform `LookupDto<Guid> { Id, DisplayName }` | Consistent dropdown pattern |
| Search endpoints | Dedicated `*SearchController.cs` over SPs (6) | Merged into `GetListAsync(Get*Input)` with FilterText | Single entry point |
| Authentication API | `UserAuthenticationController` + `UserAuthorizationController` + custom JWT | OpenIddict on port 44368 + ABP Account module | OAuth 2.0/OIDC standard |
| Permission model | Role-keyed tree from `spPermissions` | Policy-based `[Authorize(...)]` | Declarative |

### Extras in NEW (not in OLD)

| Capability | file:line |
|---|---|
| Patient as first-class entity (17 methods: booking creation, self-profile, attorney-books-on-behalf) | `IPatientsAppService.cs:10-28` |
| ApplicantAttorney as first-class entity | `IApplicantAttorneysAppService.cs:10-20` |
| AppointmentApplicantAttorney M2M as standalone service | `IAppointmentApplicantAttorneysAppService.cs:10-21` |
| AppointmentEmployerDetail as standalone service | `IAppointmentEmployerDetailsAppService.cs:10-20` |
| `UpsertApplicantAttorneyForAppointmentAsync` -- atomic create-or-update-and-link | `IAppointmentsAppService.cs:38` |
| `AppointmentAccessor.AccessorIdentityUserId` filter on appointment list | `GetAppointmentsInput.cs:22` |
| `DoctorTenantAppService.CreateAsync` -- provisions tenant + admin + Doctor in one call | `APPLICATION-SERVICES.md:293-304` |
| `UserExtendedAppService.UpdateAsync` -- keeps Doctor in sync when admin edits IdentityUser | `APPLICATION-SERVICES.md:316-333` |
| ExternalSignup flow for patients/attorneys with tenant selection | `IExternalSignupAppService.cs:9-16` |
| `GetListAsExcelFileAsync` + `GetDownloadTokenAsync` CSRF pattern | `IWcabOfficesAppService.cs:20, 23` |
| Bulk delete ops (`DeleteByIdsAsync`, `DeleteAllAsync`) on AppointmentStatuses/Locations/WcabOffices | Per interface |
| Slot generation preview (validates + reports conflicts without saving) | `IDoctorAvailabilitiesAppService.cs:22` |
| DTO validation via DataAnnotations + Concurrency via IHasConcurrencyStamp | Standard on all DTOs |
| AppointmentStatus/AppointmentLanguage promoted from enum to DB row entity (host-scoped) | |

## Open questions

1. **Defense Attorney parity** -- OLD has `AppointmentDefenseAttorneys` as first-class. NEW appears to have only ApplicantAttorney (plaintiff side). If MVP, add gap 03-G15.
2. **Patient Attorney semantics** -- Are OLD's "Patient Attorney" and NEW's "Applicant Attorney" the same concept with a rename?
3. **AppointmentRequestReport** -- Is this a materialized report row or audit trail?
4. **Scheduler ownership** -- ABP BackgroundJobs or manual-trigger API for testing?
5. **CustomField** -- Product MVP feature or cruft?
6. **Template entity** -- Use ABP's email template store or carry over OLD `Template`?
7. **SystemParameter** -- Adopt ABP's `SettingManagement` or keep as separate entity?
8. **Dashboard schema** -- What counters does NEW MVP need?
9. **CSV export scope** -- Generic or per-entity?
10. **File storage** -- ABP blob store provider choice?
11. **PATCH verb parity** -- Is partial update in MVP?
12. **Dual AppointmentDocument controllers** -- Both in use in OLD?
13. **LOG/audit granularity** -- Custom `AppointmentChangeLog` or ABP `AbpAuditLogs`?
14. **Localization source of truth** -- Losing DB-editable strings acceptable?
