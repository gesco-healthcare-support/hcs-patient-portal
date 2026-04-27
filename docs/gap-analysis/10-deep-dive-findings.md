# 10 -- Deep-Dive Findings (2026-04-23 follow-up)

Second pass after the initial 9 tracks. Consolidates new facts from code deep-reads + live API probes + web research. Corrects 4 claims in the initial tracks and surfaces 7 new critical gaps (5 MVP-blocking, 2 post-MVP).

## Part 1 -- Errata to earlier tracks (correct claims that were wrong)

### Erratum 1 -- OLD does NOT generate PDFs server-side (Track 03, Track 06)

Earlier tracks claimed OLD exports "CSV/PDF/XLSX via iTextSharp". Direct code read of `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Export\CSVExportController.cs` shows:

- **Lines 243-270 `CreatePDF()`** and **lines 273-300 `CreatePDFForDemographics()`** -- both iTextSharp-powered, both **never called** (call sites commented out on line 100 and 239).
- Line 55: Download type = 1 returns an HTML string in `ReportViewModel.HtmlString`; Angular renders the HTML and uses `window.print()` to produce the PDF client-side.
- Line 64-66: Download type != 1 returns an XLSX via ClosedXML. This path IS active.

**Impact on gap analysis:**
- Gap `G-API-13` "CSV/PDF/XLSX export" stays MVP-blocking, but the **implementation burden on NEW is smaller than implied**: NEW only needs server-side XLSX (via ABP's pattern, which WcabOffice already demonstrates with `GetListAsExcelFileAsync`) plus an HTML-returning endpoint if the UI wants browser-print. Server-side PDF (iTextSharp/QuestPDF) is optional, not a port requirement.

### Erratum 2 -- OLD SMS notifications are entirely disabled (Track 02, Track 06)

Earlier tracks said OLD "sends email + SMS on status transitions." Direct code read of `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs:839-881` (the `SendSMS` switch block) shows **every case is commented out**. Line 877 hard-sets `isSendSMS = false` before reaching any Twilio call. The ITwilioSmsService is wired in DI but status transitions never invoke it.

SchedulerDomain (the 9 recurring jobs, `SchedulerDomain.cs`) DOES invoke Twilio for 6 of 9 jobs, but:
- Job 7 `AppointmentPendingReminderStaffUsersNotification`: email send commented out (line 309).
- Job 8 `AppointmentPendingDocumentSendToResponsibleUser`: both SMS and email commented out (lines 352, 361) -- **completely disabled**.
- Remaining 6 jobs would invoke Twilio if `isSMSEnable: true` in server-settings.json, but in the running deployment `isSMSEnable: false`.

**Impact on gap analysis:**
- Gap `CC-02` SMS send severity drops from MVP-blocking to **"needs-decision"**. If OLD has been running SMS-off for years in production, porting SMS to NEW is non-urgent.
- Adrian should confirm with the clinic: do any OLD deployments actually have `isSMSEnable: true` in their `server-settings.json`? If no, SMS port is non-MVP.

### Erratum 3 -- OLD scheduler has hardcoded `AppointmentId=1` / `UserId=1` (Track 02)

`P:\PatientPortalOld\PatientAppointment.Domain\Core\SchedulerDomain.cs` lines 78-79, 122, 155, 179, 207, 237, 264, 338: every stored-proc invocation is called with literal `1` for both `@AppointmentId` and `@UserId` parameters. The commented line 78 shows the original intent was `UserClaim.UserId`. This is **likely a bug in OLD itself**, not a design feature.

**Impact on gap analysis:**
- Gap `G2-11` (9 scheduler jobs in NEW) stays MVP-blocking, but the spec for each job is **what the stored-proc is DESIGNED to return when given a real UserId/AppointmentId**, not what it returns with the hardcoded `1`. Re-spec the 9 jobs from the proc body, not from the caller.
- Possible follow-up: does this bug mean the recurring reminders have been silently misfiring in production? Adrian may want to confirm with the clinic whether they receive daily reminder emails for appointment #1 only.

### Erratum 4 -- OLD CustomField schema is fixed-type, NOT dynamic forms (Track 02, Track 03)

Earlier tracks implied OLD has "dynamic form-builder custom fields." The actual model (`P:\PatientPortalOld\PatientAppointment.DbEntities\Models\CustomField.cs` + `CustomFieldsValue.cs`):

- Only 7 field types (`CustomFieldType` enum: Alphanumeric, Numeric, Picklist, Tickbox, Date, Radio, Time).
- Values stored as `string` column; **no type coercion**; `CustomFieldDomain.CommonValidation()` is empty (line 105-109).
- Scoped per `AppointmentTypeId`; no per-role or per-tenant scoping in the schema.
- Hard limit: max 10 active custom fields per appointment type.

**Impact on gap analysis:**
- Gap `G2-N2`/`03-G12` scope narrows: NEW can replace this with ABP `ExtraProperties` + `ObjectExtensionManager.MapEfCoreProperty<T, TProperty>()` -- no bespoke `CustomField`/`CustomFieldsValue` table needed. See Part 4 research finding for details.
- Estimated effort drops from 2+ days to **~1 day** using the ABP-native pattern.

## Part 2 -- NEW security + quality gaps (new MVP-blocking items)

### NEW-SEC-01 -- Appointments view route has NO permission guard (MVP-blocking)

From `W:\patient-portal\development\angular\src\app\appointments\CLAUDE.md` (entity-scoped doc): the route `/appointments/view/:id` only applies `authGuard`, not `permissionGuard`. This means **any authenticated user -- including unassigned external users -- can view any appointment by crafting the URL with a known ID**. This is a cross-tenant leak vector if the tenant filter fails or if admins impersonate across tenants.

Confirmed by reading `W:\patient-portal\development\angular\src\app\app.routes.ts:99-102` -- `/appointments/add` also uses `authGuard` only. The list route `/appointments` does use `permissionGuard: CaseEvaluation.Appointments`.

**Impact:** This is a security gap, not just a feature gap. Adrian should treat as MVP-blocking.

### NEW-SEC-02 -- AppService methods don't enforce Create/Edit permissions (MVP-blocking)

Earlier tracks documented the NEW permission tree (62 permissions). Closer read of the AppService class-level `[Authorize]` attributes vs method-level attributes reveals: for most entities, only a class-level `[Authorize(CaseEvaluationPermissions.<Entity>.Default)]` is applied. Individual `CreateAsync` / `UpdateAsync` / `DeleteAsync` methods **do not carry their own `.Create` / `.Edit` / `.Delete` permission attributes**.

Result: a user with only `.Default` (view) permission can successfully POST / PUT / DELETE via the HTTP API. The frontend `*abpPermission="'...Create'"` directive hides the buttons, but the backend enforcement isn't there.

Listed in `Appointments\CLAUDE.md` under "Known gaps" (the CLAUDE.md was honest about this). Not yet flagged in the initial gap docs.

**Impact:** Security-critical because a patient with view access could create/edit appointments for themselves or others via API tools. MVP-blocking.

### NEW-SEC-03 -- DoctorTenantAppService.CreateAsync is non-transactional; partial failure leaves orphaned tenants (MVP-blocking)

`W:\patient-portal\development\src\HealthcareSupport.CaseEvaluation.Application\Doctors\DoctorTenantAppService.cs:57`: UoW declared with `requiresNew: true, isTransactional: false`. The outer `base.CreateAsync()` creates the `SaasTenant` row in the host DB, then the inner steps switch tenant context to create `IdentityUser`, `Doctor`, and `Role`. If any inner step throws, the outer `SaasTenant` is already committed.

Additional issue: hardcoded `Gender.Male` (line 137) and empty `LastName` (line 138) in the `Doctor` creation. Tenant onboarding will always produce a "Test Male Firstname blank-LastName" doctor record that the operator has to fix manually.

**Impact:** MVP-blocking. Tenant onboarding is central to NEW's SaaS model; partial failures during demo will leave the system in a broken state that a manual DB edit is needed to recover from.

### NEW-SEC-04 -- ExternalSignup creates Patient with hardcoded defaults (MVP-blocking)

`ExternalSignupAppService.RegisterAsync` (lines 211-224) creates a `Patient` entity with:
- Hardcoded `Gender.Male`
- `DateTime.UtcNow.Date` as `DateOfBirth` (not elicited from the signup form)
- Hardcoded `PhoneNumberType.Home`
- `stateId: null`, `appointmentLanguageId: null`

The signup form surely doesn't collect these (it's an anonymous public registration); this means every externally-signed-up patient is born today, lives in no state, and speaks no language. Legally and data-quality-wise this is broken.

**Impact:** MVP-blocking. Either (a) collect the real values in the signup form, (b) leave them nullable and prompt user to complete profile after first login, or (c) make the signup flow require admin approval before creating a Patient row.

### NEW-SEC-05 -- Missing Strict-Transport-Security header (MVP-blocking for production)

OLD sends `Strict-Transport-Security: max-age=31536000` (good). NEW does not send this header (confirmed via `curl -I http://localhost:44327/`). Production deployment behind HTTPS must add this header or downgrade attacks are feasible.

**Impact:** Easy fix (one line in `CaseEvaluationHttpApiHostModule.cs:92-95` where `X-Frame-Options: DENY` is already set), but currently absent.

### NEW-QUAL-01 -- Test coverage has critical blind spots (MVP-blocking for confidence)

From NEW deep-read: 17 test classes exist, covering basic CRUD for Appointments, Books, Doctors, Patients, DoctorAvailabilities. **Zero tests** for:
- `DoctorTenantAppService.CreateAsync` (tenant provisioning)
- `ExternalSignupAppService` (anonymous signup)
- `OpenIddictDataSeedContributor` (OAuth client registration)
- State machine transitions (since NEW doesn't enforce them, nothing to test, but also no regression fence)
- Permission enforcement (because enforcement is missing per NEW-SEC-02)
- Multi-tenancy filter effectiveness (critical for data isolation)
- AppointmentAccessors / AppointmentApplicantAttorneys (full CRUD services, zero tests)

**Impact:** Without test coverage on tenant provisioning + permission enforcement, the MVP demo could regress silently between builds.

### NEW-QUAL-02 -- `console.log` debug statement in production code (non-MVP)

From `Appointments\CLAUDE.md` -- a `console.log` call is left in the appointment-add component near line 1413. Not a functional bug but a sign that the Angular 20 component didn't get a final pre-commit review.

**Impact:** Non-MVP housekeeping item. Add to a code-quality sweep before launch.

## Part 3 -- Live API observations (things the static-code analysis missed)

### NEW Swagger has 317 paths / 438 verbs, not the ~153 I listed in track 04

Curled `http://localhost:44327/swagger/v1/swagger.json` and counted paths + method operations. Top controllers by endpoint count:

```
45  User              (ABP Identity -- CRUD, claim types, lock, unlock, 2FA, etc.)
31  UserExtended      (CaseEvaluation's extension to IdentityUserAppService)
23  Account           (register, verify email, reset password, change password, profile pic, etc.)
16  OrganizationUnit  (ABP org unit hierarchy)
16  Patient           (as documented)
14  Appointment       (as documented)
12  DoctorTenant      (tenant creation + lookup, more than I realized)
12  Settings          (per-tenant setting CRUD)
12  Tenant            (SaaS tenant admin)
11  AuditLogs         (audit trail query)
11  DoctorAvailability
11  WcabOffice
10  Role, Edition, FileDescriptors, Location, AccountSettings, Doctor
```

**Impact:** Track 04's "~153 business endpoints" was an undercount. NEW actually has ~153 business + ~165 framework/admin endpoints = 317 total. For Adrian's MVP scoping, this is good news -- a lot of OLD's admin-UI features are **already exposed at the API level by ABP Commercial modules**; only the Angular UI pages need to be wired up.

Specifically, things like "manage users" (OLD `/users`), "system parameters" (OLD `/system-parameters`), and "edit email templates" (OLD `/templates`) all have ABP backend endpoints already running; the gap is only the Angular UI layer, which is much less work than "endpoint + service + UI" from scratch.

### NEW OIDC supports more grant types than documented

Live OIDC discovery response includes:
- `urn:ietf:params:oauth:grant-type:device_code` (device flow)
- `urn:ietf:params:oauth:grant-type:token-exchange` (delegation flow)
- `implicit` (legacy; typically disabled in 2026)

The track 05 doc listed only 6 grants; actually 9 are enabled. Device code + token exchange are unusual for a medical scheduling app -- Adrian may want to verify they are intentional (and disable them if not, per OWASP).

**Impact:** Minor hardening opportunity, not a gap.

### NEW admin is `admin@abp.io` with password `1q2w3E*` and has role `admin`

Password grant works:
```
POST /connect/token grant_type=password username=admin password=1q2w3E*
```
Returns access_token with claims: `iss=http://localhost:44368/`, `aud=CaseEvaluation`, `sub=8efb09dc-...`, `role=admin`, `email=admin@abp.io`, `scope=CaseEvaluation offline_access`, `client_id=CaseEvaluation_App`. Both access (3599 sec) and refresh tokens issued.

With the access_token, `GET /api/app/appointments` returns `{"totalCount":0,"items":[]}` HTTP 200 -- confirms zero seed data. `GET /api/multi-tenancy/tenants` returns **HTTP 404** -- endpoint path may not match ABP Pro's module or the admin lacks SaaS permissions despite being host-admin. Worth investigating before a tenant-onboarding demo.

**Impact:** The 404 on `/api/multi-tenancy/tenants` is a live-system oddity that blocks the tenant-creation demo path; needs follow-up.

### OLD Scheduler trigger endpoint IS anonymous AND fires

`POST http://localhost:59741/api/scheduler/postscheduler` with empty JSON body returned `Hello socal` HTTP 200. Anyone on the network can invoke the 9 notification-dispatch jobs. The static analysis flagged this as a concern; live probe confirmed it's **actually exploitable**.

Because the running OLD instance has all 9 stored procs stubbed to no-ops (the bring-up replaced them with empty `SELECT` statements), the trigger itself does no harm. In production, this would be a denial-of-email vector (attacker spams the notification system). Not a direct data leak, but bad hygiene.

**Impact:** Not an MVP issue for NEW (NEW replaces this with ABP BackgroundJobs), but flag it to Gesco operations if any OLD deployments are still internet-facing.

### OLD DocumentDownload path traversal attempt is blocked

Three variants tested: `../server-settings.json`, URL-encoded `%2F`, Windows-style `..\server-settings.json`. All returned HTTP 400. The `filePath.Contains("..")` guard (`DocumentDownloadController.cs:36`) catches naive attempts. Not foolproof (doesn't use `Path.GetFullPath` + directory-root containment check), but adequate for inline defense.

## Part 4 -- Applicable web research (ABP patterns for the MVP path)

These are accelerators for closing the MVP gaps, confirmed against current ABP 10.x docs and community sources.

### Per-tenant branding via `ISettingManager.SetForTenantAsync` + custom `BrandingProvider`

- LeptonX exposes CSS vars `--lpx-logo`, `--lpx-logo-icon`, `--lpx-brand`.
- Pattern: `SettingDefinitionProvider` with `isVisibleToClients: true, isInherited: false`; save via `ISettingManager.SetForTenantAsync(tenantId, key, value)`.
- `IBrandingProvider` is synchronous -- workaround is to override the Lepton logo component and read from `IAbpLazyServiceProvider`.
- Est. effort: 1-2 days per the reference walkthroughs.
- Covers BRAND-01, BRAND-02 post-MVP gaps (track 09, 06, 07 + README).

### Recurring background jobs: Hangfire + `IDynamicBackgroundWorkerManager`

- ABP built-in `IBackgroundJobManager` is one-shot only. Hangfire is the canonical recurring path.
- `HangfireBackgroundWorkerBase` exposes `RecurringJobId` + `CronExpression`. Dashboard is `UseAbpHangfireDashboard`.
- Since ABP 9.x, `IDynamicBackgroundWorkerManager` allows add/update/remove schedules at runtime across providers (Default, Hangfire, Quartz) -- useful if per-tenant schedules differ.
- **Tenant-scoped jobs require explicit handling:** ABP does NOT auto-resolve a tenant inside a job body. Persist `TenantId` in job args, wrap body in `using (_currentTenant.Change(tenantId)) { ... }`.
- Est. effort: depends on how many of OLD's 9 jobs are actually needed. Per-job, ~0.5-1 day once infrastructure is in.
- Covers gaps `CC-03`, `03-G09`, `G2-11` (tracks 06, 03, 02).

### S3 blob storage: `Volo.Abp.BlobStoring.Aws` v9.1.1, compatible with ABP 10.x

- NuGet package `Volo.Abp.BlobStoring.Aws` provides S3 provider for ABP BlobStoring abstractions.
- **Critical security upgrade over OLD:** set `UseCredentials = true` (IAM role on EC2/ECS/Lambda host) OR `UseTemporaryCredentials` (cross-account temp creds). Never hardcode keys.
- Adrian's HIPAA stance: configure bucket-level SSE-KMS + CloudTrail object-level logging at the S3 layer. BAA is between Gesco and AWS, not ABP.
- Est. effort for full port: 2-3 days (container setup, provider wiring, CC-04 gap).

### File Management Module (Volo commercial): MVP-ready

- `Volo.FileManagement` (Pro) provides hierarchical folders, per-tenant size limits, file CRUD UI, all on top of BlobStoring. Add via `abp add-module Volo.FileManagement`.
- Replaces OLD's 3 document-management tables (`Documents`, `DocumentPackages`, `PackageDetails`) at the infrastructure layer. Application-specific behaviors (per-appointment document lists, S3 bucket-per-type) still need domain code.
- Est. effort: 1 day to wire + 2-3 days to map OLD's document-per-appointment semantics.

### Dynamic custom fields: `ObjectExtensionManager.Instance.MapEfCoreProperty<T, TProperty>()`

- Native ABP pattern for per-entity extension fields. Stored either as separate columns (strongly typed) OR in a JSON `ExtraProperties` column.
- Every `AggregateRoot` already implements `IHasExtraProperties`.
- For Gesco's fixed-type schema (Alphanumeric/Numeric/Picklist/Tickbox/Date/Radio/Time), this is a cleaner substitute than OLD's `CustomField` + `CustomFieldsValue` tables.
- Alternative: `Volo.Forms` (Pro) is Google-Forms style for survey-like data. `EasyAbp.DynamicForm` is the community option.

### PDF generation: QuestPDF is the winner

- MIT license under $1M org revenue (Gesco qualifies today). $699/yr above that threshold.
- iText7 is AGPL or $15K-$210K/yr commercial -- rule out.
- iText5 (OLD's library) is unmaintained and same licensing issue -- already not used in OLD anyway (per erratum 1).
- No built-in ABP reporting/PDF module. Direct `Volo.Abp.DependencyInjection` registration.
- Est. effort for the Patient Demographics PDF (the main OLD use case): 1 day to port the template + 0.5 day for service wiring.

### Appointment state machine: `dotnet-state-machine/Stateless` library

- Community-canonical pattern for workflow state in ABP aggregates. MIT license.
- Instantiate inside `Appointment` aggregate; persist the status enum on the entity. No official ABP package but pattern is well-documented.
- Addresses G2-01 (NEW has 13-state enum defined but no transition enforcement). Est. effort: 2-3 days to define + test all 30 transitions.

### Password hash migration: possible via custom `IPasswordHasher<IdentityUser>`

- ASP.NET Core Identity's built-in `PasswordHasher` auto-recognizes legacy ASP.NET Identity v2 PBKDF2/HMAC-SHA1 hashes and rehashes on login. **Doesn't help here** because OLD uses a custom `Rx.Core.Security` hash format, not ASP.NET Identity.
- Options:
  1. Implement a custom `IPasswordHasher<IdentityUser>` that recognizes the Rx.Core.Security format, verifies, and rehashes to ASP.NET Core Identity format on success.
  2. Force password reset for all users at cutover. Simpler, safer for medical data, but disruptive.
- Est. effort for option 1: 1-2 days after reverse-engineering the Rx.Core.Security format.

### Angular 20 `CORE_OPTIONS` crash: open issue, no official fix yet

- `NullInjectorError: No provider for InjectionToken CORE_OPTIONS!` when running `ng serve` against ABP 8.3+ LeptonX with Angular 20 standalone components.
- Tracked in `abpframework/abp` issue #23035, support thread #10558. Partial fixes across 9.x/10.x patches; not fully resolved as of April 2026.
- Gesco's repo workaround (`npm run build` + `npx serve` to bypass HMR) is the current recommended mitigation. Keep until ABP 10.2+ confirms a fix.

## Part 5 -- New gap entries to merge into README + track docs

These have been flagged here for inclusion in the main gap table.

| new-gap-id | severity | track | summary | effort |
|---|---|---|---|---|
| NEW-SEC-01 | MVP-blocking | 07, 05 | `/appointments/view/:id` (+ `/add`) routes only have `authGuard`, not `permissionGuard` -- any authenticated user can view/add appointments regardless of permission grants | S (1 day) |
| NEW-SEC-02 | MVP-blocking | 03, 05 | Most AppService `CreateAsync`/`UpdateAsync`/`DeleteAsync` methods lack method-level `[Authorize(...Create)]` attributes -- HTTP-level permission enforcement is missing for mutations | M (2-3 days to audit + fix every service) |
| NEW-SEC-03 | MVP-blocking | 02, 03 | `DoctorTenantAppService.CreateAsync` runs with `isTransactional: false`; SaasTenant committed before IdentityUser/Doctor creation can fail -- orphaned tenant rows possible | S (0.5 day -- change to transactional UoW) |
| NEW-SEC-04 | MVP-blocking | 03 | `ExternalSignupAppService.RegisterAsync` creates Patient with hardcoded `Gender.Male`, `DateOfBirth = today`, `PhoneNumberType.Home` -- data-quality and legal issues | S (1 day -- either collect real values in signup form, or leave nullable + complete-profile flow) |
| NEW-SEC-05 | MVP-blocking | 06 | NEW does not send `Strict-Transport-Security` header; OLD does. HTTPS downgrade vulnerability in production | XS (1 line in `CaseEvaluationHttpApiHostModule.cs`) |
| NEW-QUAL-01 | MVP-blocking | (cross-cutting) | Zero tests for tenant provisioning, permission enforcement, external signup, multi-tenancy filter. Demo risk | M (3-5 days to add coverage for critical paths) |
| NEW-QUAL-02 | non-MVP | 08 | `console.log` debug statement in `appointment-add` component ~line 1413. Code-quality sweep item | XS |

## Sources (web research references)

- [LeptonX Theme Module | ABP Docs](https://docs.abp.io/en/commercial/latest/themes/lepton-x/index)
- [ABP Branding | Docs](https://docs.abp.io/en/abp/latest/UI/AspNetCore/Branding)
- [Implementing Custom Tenant Logo](https://abp.io/community/articles/implementing-custom-tenant-logo-feature-in-abp-framework-a-stepbystep-guide-sba96ac9)
- [White Labeling in ABP Framework](https://abp.io/community/articles/white-labeling-in-abp-framework-5trwmrfm)
- [ABP Settings](https://abp.io/docs/latest/framework/infrastructure/settings)
- [ABP Background Jobs](https://abp.io/docs/latest/framework/infrastructure/background-jobs)
- [Hangfire Background Job Manager](https://abp.io/docs/latest/framework/infrastructure/background-jobs/hangfire)
- [Dynamic Background Jobs](https://abp.io/community/articles/dynamic-background-jobs-and-workers-in-abp-wfdkdsq9)
- [Volo.Abp.BlobStoring.Aws NuGet](https://www.nuget.org/packages/Volo.Abp.BlobStoring.Aws)
- [BLOB Storing AWS Provider](https://abp.io/docs/latest/framework/infrastructure/blob-storing/aws)
- [File Management Module (Pro)](https://abp.io/docs/latest/modules/file-management)
- [ABP Multi-Tenancy](https://abp.io/docs/latest/framework/architecture/multi-tenancy)
- [Angular 20 Upgrade issue #23035](https://github.com/abpframework/abp/issues/23035)
- [CORE_OPTIONS support #7377](https://abp.io/support/questions/7377/Frontend-Error-after-ABP-upgrade--No-provider-for-InjectionToken-COREOPTIONS)
- [Safely migrating passwords in ASP.NET Core Identity](https://andrewlock.net/safely-migrating-passwords-in-asp-net-core-identity-with-a-custom-passwordhasher/)
- [Riok/Mapperly GitHub](https://github.com/riok/mapperly)
- [ABP Object Extensions](https://docs.abp.io/en/abp/latest/Object-Extensions)
- [ABP Forms Module (Pro)](https://docs.abp.io/en/commercial/latest/modules/forms)
- [QuestPDF License](https://www.questpdf.com/license/)
- [Stateless state machine GitHub](https://github.com/dotnet-state-machine/stateless)
