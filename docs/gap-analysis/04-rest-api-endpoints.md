# 04 -- REST API Endpoints: Gap Analysis OLD vs NEW

## Summary

OLD exposes roughly 132 HTTP routes across 49 active controllers; NEW exposes roughly 153 ABP-style routes across 20 controllers. Raw endpoint count is near parity, but the distribution is very different: NEW over-covers CRUD + lookups for a small set of core entities with deep navigation-property and bulk-delete variants; OLD over-covers document-heavy workflows (packages, templates, new-documents, document uploads, CSV/PDF export, scheduler jobs) that have not been ported. About 54 OLD endpoints have no NEW counterpart and 72 NEW endpoints have no OLD counterpart; only about 18 endpoint-groups are strongly analogous on both sides. MVP risk rating: HIGH -- the document-management subsystem, reporting/export, and change-log/notes/templates infrastructure are all absent from NEW. Secondary risk: OLD's auth model is wide-open by design (JWT bypass by controller-name list, 0 `[Authorize]` attributes), while NEW enforces Bearer globally; any OLD behavior that depended on unauthenticated POST (DocumentUpload, Dashboard, Scheduler, CsvExport) must be re-scoped before port.

## Method

- **NEW source:** `W:\patient-portal\development\docs\api\ENDPOINTS-REFERENCE.md` + controllers under `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/`. Direct Swagger fetch blocked by HTTPS-only cert issue.
- **OLD source:** Glob + Grep over `P:\PatientPortalOld\PatientAppointment.Api\Controllers\` (54 `.cs` files).
- **OLD auth:** `ApplicationConfiguration.cs:53-61` calls `UseJwtAuthentication(authBypass, authzBypass)`. No `[Authorize]` attributes. Auth is global, bypassed by controller-name list in `AllowedApis.cs:14-45`.
- **NEW auth:** Global Bearer + per-method `[Authorize(CaseEvaluationPermissions.*)]` per `docs/decisions/002-manual-controllers-not-auto.md`.

Timestamp: 2026-04-23.

## OLD version state

### Auth pipeline
- `AllowedApis.cs:14-27` -- `AuthenticationByPass()` returns 9 controllers that skip JWT entirely: `UserAuthenticationController`, `ApplicationConfigurationsController`, `UserLookupsController`, `UsersController`, `SchedulerController`, `CsvExportController`, `DocumentUploadController`, `DocumentDownloadController`, `DashboardController`.
- Net effect: every other controller requires JWT but performs no permission check; tree is fetched separately.

### OLD controller inventory (49 live, 132 routes)

Grouped per folder. Summary counts:

| Folder | Controllers | Routes |
|---|---|---|
| AppointmentChangeLog | 2 | 7 |
| AppointmentRequest | 9 | ~40 |
| Core | 7 | ~14 |
| CustomField | 1 | 5 |
| DoctorManagement | 7 | ~30 |
| Document | 6 | ~20 |
| DocumentManagement | 3 | ~14 |
| Export | 1 | 3 |
| Lookups | 9 | ~28 |
| Note | 1 | 4 |
| SystemParameter | 1 | 5 |
| TemplateManagement | 1 | 4 |
| User | 1 | 5 |
| UserQuery | 1 | 5 |

**Total: 132 distinct HTTP verb + path combinations.**

## NEW version state

### NEW controller inventory (20 controllers, ~153 routes)

| Controller | Base path | Endpoints |
|---|---|---|
| AppointmentController | `/api/app/appointments` | 14 (CRUD + nav, lookups, booking helpers) |
| PatientController | `/api/app/patients` | 16 (+my-profile, for-appointment-booking variants) |
| DoctorController | `/api/app/doctors` | 10 |
| DoctorAvailabilityController | `/api/app/doctor-availabilities` | 11 (+preview, by-slot, by-date) |
| ExternalSignupController | `/api/public/external-signup` | 3 (anonymous + Bearer) |
| ExternalUserController | `/api/app/external-users` | 1 (`me`) |
| LocationController | `/api/app/locations` | 10 (+bulk delete) |
| StateController | `/api/app/states` | 5 |
| AppointmentTypeController | `/api/app/appointment-types` | 5 |
| AppointmentStatusController | `/api/app/appointment-statuses` | 8 (+bulk delete) |
| AppointmentLanguageController | `/api/app/appointment-languages` | 5 |
| WcabOfficeController | `/api/app/wcab-offices` | 11 (+bulk delete, Excel export, download-token) |
| AppointmentEmployerDetailController | `/api/app/appointment-employer-details` | 8 |
| AppointmentAccessorController | `/api/app/appointment-accessors` | 8 |
| ApplicantAttorneyController | `/api/app/applicant-attorneys` | 8 |
| AppointmentApplicantAttorneyController | `/api/app/appointment-applicant-attorneys` | 9 |
| BookController | `/api/app/book` | 5 (demo template) |

**Plus OpenIddict + Identity Module surface (30+ endpoints):** `/connect/*`, `/api/account/*`, `/api/identity/*`, `/abp/application-configuration`, etc.

**Total application endpoints: ~153**, +30 ABP framework = ~183.

## Delta

### Side-by-side by functional area

**Authentication / Authorization:**

| OLD URL | NEW URL | Status |
|---|---|---|
| POST `api/userauthentication/login` | POST `connect/token` (OpenIddict) | shape-differs |
| POST `api/userauthentication/postforgotpassword` | POST `api/account/send-password-reset-code` | shape-differs |
| PUT `api/userauthentication/putforgotpassword` | POST `api/account/reset-password` | shape-differs |
| PUT `api/userauthentication/putemailverification` | POST `api/account/verify-email` | shape-differs |
| POST `api/userauthorization/authorize` | (none; ABP builds from `/abp/application-configuration`) | old-only |
| POST `api/userauthorization/logout` | POST `connect/endsession` | shape-differs |
| POST `api/userauthorization/access` | GET `/abp/application-configuration` | shape-differs |

**Appointments (core):**

| OLD URL | NEW URL | Status |
|---|---|---|
| GET `api/appointments` | GET `/api/app/appointments` | exists-in-both (query params differ) |
| GET `api/appointments/{id}` | GET `/api/app/appointments/{id}` | exists-in-both |
| POST `api/appointments` | POST `/api/app/appointments` | exists-in-both |
| PUT `api/appointments/{id}` | PUT `/api/app/appointments/{id}` | exists-in-both |
| PATCH `api/appointments/{id}` | (none) | old-only |
| DELETE `api/appointments/{id}` | DELETE `/api/app/appointments/{id}` | exists-in-both |
| POST `api/appointments/search` (spm.spAppointments) | (none; filters baked into GET list) | old-only (intentional) |
| POST `api/appointments/search/GetById` | GET `/api/app/appointments/with-navigation-properties/{id}` | shape-differs |
| (none) | 5 lookup endpoints + attorney booking helpers | new-only |

**Appointment sub-entities:** OLD has nested routes under `/api/appointments/{appointmentId}/...` for 6 sub-entities; NEW flattens to `/api/app/<entity>/` with `AppointmentId` as a column.

| Sub-entity | OLD | NEW |
|---|---|---|
| AppointmentAccessor | nested | flat (`/api/app/appointment-accessors`) |
| AppointmentChangeRequest | nested | **absent** |
| AppointmentDocument | nested + flat | **absent** |
| AppointmentJointDeclaration | nested + flat | **absent** |
| AppointmentNewDocument | nested + special (S3 upload) | **absent** |
| AppointmentInjuryDetail | nested | **absent** |
| AppointmentEmployerDetail | (via Appointment) | flat (new-only) |
| ApplicantAttorney / AppointmentApplicantAttorney | (via Appointment) | flat (new-only) |

### MVP-blocking gaps (capability present in OLD, absent in NEW)

~54 OLD endpoints with no NEW counterpart:

| Gap group | Endpoints | Effort |
|---|---|---|
| G-API-01 | Document management (/api/documents, /api/packagedetails, /api/documentpackages) | Medium |
| G-API-02 | Appointment documents (both flat + nested variants; 3 controllers; S3 upload) | Medium-High |
| G-API-03 | Document upload/download (anon surface, security cleanup needed) | Medium |
| G-API-04 | Joint declarations (nested + flat + search) | Medium |
| G-API-05 | Templates CRUD (5 endpoints) | Small-Medium |
| G-API-06 | Notes CRUD (4 endpoints) | Small |
| G-API-07 | Custom fields CRUD (5 endpoints + lookups) | Medium |
| G-API-08 | User queries (5 endpoints) | Small |
| G-API-09 | Appointment change logs + search (5 endpoints) | Small-Medium |
| G-API-10 | Appointment change requests (4 endpoints) | Small |
| G-API-11 | Appointment injury details (4 endpoints) | Medium (MVP-critical for workers-comp) |
| G-API-12 | Appointment request report + search (5 endpoints) | Small-Medium |
| G-API-13 | CSV/PDF/XLSX export (3 endpoints; generic) | Medium-Large |
| G-API-14 | Dashboard counters (1 endpoint) | Small-Medium |
| G-API-15 | Scheduler triggers (9 notifications) | Medium-Large |
| G-API-16 | System parameters CRUD | Small-Medium |
| G-API-17 | JSON Patch endpoints (Appointments, Users, Documents) | Small (or N/A if ABP conv accepted) |
| G-API-18 | Composite-key DELETE on DoctorAvailabilities | Already shape-differs in NEW |
| G-API-19 | Flat list search stored proc on appointments | Verify `FilterText` covers same fields |
| G-API-20 | Doctor preferred locations + doctor-appointment-types (nested) | Small (absorbed into Doctor in NEW) |
| G-API-21 | 12 orphan lookups (access-type, phone-number-type, city, etc.) | Medium |

### Non-MVP gaps

- `ValuesController` scaffold (ASP.NET Core demo)
- Empty `NoteLookupsController` (all actions commented)
- `AppointmentRequestLookupsController.accesstypelookups` (trivial enum)

### Intentional architectural differences (NOT gaps)

| Topic | OLD | NEW | Why |
|---|---|---|---|
| Routing convention | Manual `[Route("api/[controller]")]` lowercase | ABP `/api/app/<entity>` + RemoteService(IsEnabled=false) | Per ADR 002 |
| List-and-filter shape | Positional path params `{orderBy}/{sort}/{page}/{count}` | Query params via `PagedAndSortedResultRequestDto` | OpenAPI standard |
| Permission model | Global JWT bypass list; permissions in Angular | Declarative `[Authorize(Permissions.*)]` per method | Analyzable code-first |
| Authentication endpoint | Bespoke `UserAuthenticationController` | OpenIddict on port 44368 | OAuth 2.0/OIDC |
| Search endpoints | Dedicated `*SearchController` running stored procs | LINQ filter in GET list | Eliminates 40 stub procs |
| Lookups | Per-feature `*LookupsController` | Per-entity inline on parent (`/state-lookup`, `/location-lookup`) | Co-location with owning entity |
| JSON Patch | `[HttpPatch]` with `JsonPatchDocument<T>` | Full-object PUT | ABP discourages patch |
| Bulk delete | One-at-a-time | `/all` + collection DELETE | ABP template pattern |
| Excel/CSV export | Generic `/api/csvexport` (XLSX/PDF/HTML) | Targeted per-entity (WcabOffices only) | ABP pattern |

### Extras in NEW

~72 endpoints:
- `/api/app/patients/*` 16 endpoints (OLD has no top-level Patient)
- `/api/app/applicant-attorneys/*` + `/api/app/appointment-applicant-attorneys/*`
- `/api/app/external-users/me` + `/api/public/external-signup/*`
- `/api/app/doctor-availabilities/preview` (bulk slot generation)
- `/api/app/wcab-offices/as-excel-file` + `/download-token`
- All `with-navigation-properties/{id}` variants (eager-load)
- All per-entity `-lookup` endpoints
- `/api/app/states/*`, `/appointment-statuses/*`, `/appointment-languages/*` (OLD treats these as lookups/enums)
- `/api/app/book/*` (ABP demo)
- OpenIddict: `/connect/token`, `/connect/authorize`, `/connect/userinfo`, `/connect/endsession`, `/.well-known/openid-configuration`
- ABP Identity: `/api/identity/users`, `/api/identity/roles`, `/api/identity/claim-types`
- ABP Account: `/api/account/register`, `/api/account/verify-email`, `/api/account/reset-password`, `/api/account/send-password-reset-code`
- ABP Feature Management, Permission Management, Tenant Management
- ABP infrastructure: `/abp/application-configuration`, `/abp/application-localization`, `/abp/service-proxy-script`

## Open questions

1. **Q1 Dashboard:** Which counters does each of the 7 roles need? OLD uses `spDashboardCounters`.
2. **Q2 Scheduler:** Which of the 9 notifications are still business-required? Is anon POST endpoint intentional or security bug?
3. **Q3 Anonymous file endpoints:** DocumentUpload/Download/CsvExport/Dashboard/Scheduler are all anonymous in OLD. Intentional or security bug inherited? Port decision required.
4. **Q4 PATCH:** Does Angular 7 client actively use PATCH anywhere? Grep `.patch(` in OLD services.
5. **Q5 Change-log vs audit-log:** Port as bespoke entity or reuse ABP `AbpAuditLogs` with `AppointmentId` filter?
6. **Q6 Document storage:** Which OLD surface (`AppointmentDocuments` nested, flat, or `AppointmentNewDocuments` S3) is canonical?
7. **Q7 Templates + Notes:** MVP or deferred?
8. **Q8 Custom fields:** Keep DIY form builder or replace with ABP `SettingManagement` + `ExtraProperties`?
9. **Q9 Reporting:** Per-entity (ABP idiom) or generic endpoint (OLD idiom)?
10. **Q10 UserQueries:** What business purpose?
11. **Q11 Swagger reachability:** Can Adrian confirm `http://localhost:44327/swagger/v1/swagger.json` live?
12. **Q12 Book demo:** Safe to remove?
