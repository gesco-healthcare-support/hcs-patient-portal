---
type: index
audited: 2026-05-01
purpose: Pointer + summary index for OLD app source documentation; starting point for per-feature parity audits.
---

# OLD app documentation index

This file maps OLD app source documents to their canonical locations, summarizes what's in each, and provides cross-references for the per-feature parity audits.

- OLD app source root: `P:\PatientPortalOld\` (READ ONLY).
- Project codename: **SoCal Practice Management**.
- Domain: California workers' compensation medical evaluations (PQME, AME, RE-EVAL).
- Source year range: 2017-2023 (formal SDE practice).

---

## NEW app naming overrides (confirmed 2026-05-01)

When porting to NEW, apply these renames. All other entity/field/role names stay verbatim from OLD.

| OLD | NEW | Reason |
|-----|-----|--------|
| Patient Attorney | **Applicant Attorney** | California workers' comp uses "applicant" for the worker filing a claim; their attorney is the Applicant Attorney. More accurate domain terminology. |
| Adjuster | (unchanged: **Adjuster**) | Earlier discussion floated "Claim Examiner" -- rejected. OLD canonical name is Adjuster (overview line 123). "Claim Examiner" is per-injury contact metadata in OLD schema (table `AppointmentClaimExaminers` keyed to `AppointmentInjuryDetailId`), NOT a user role. |

Affected surfaces in NEW:

- Role name string: `Patient Attorney` -> `Applicant Attorney`
- Schema: `AppointmentPatientAttorneys` table -> `AppointmentApplicantAttorneys`; `AppointmentPatientAttorneyId` field -> `AppointmentApplicantAttorneyId`
- API endpoints: any `/PatientAttorney/...` paths -> `/ApplicantAttorney/...`
- UI labels: "Patient Attorney" copy -> "Applicant Attorney"
- Permission keys: `PatientAttorney.*` -> `ApplicantAttorney.*`
- Localization: replace string keys + values

OLD-doc text below continues to use "Patient Attorney" verbatim -- we don't rewrite OLD docs. Audit docs use NEW names when describing NEW behavior.

---

## Source documents

OLD source docs live at `P:\PatientPortalOld\Documents_and_Diagrams\`. Read them
directly -- the old-extracted copies that previously lived at
`.claude/old-extracted/` have been removed. All originals are readable via Claude
because `P:\PatientPortalOld` is registered as an additional directory in
`.claude/settings.local.json`.

### Architecture

| File | Path under `Documents_and_Diagrams\` | Purpose |
| --- | --- | --- |
| Project overview | `Architecture\SoCal Project Overview Document.docx` | **Master business spec.** All features, roles, business rules, intake form, statuses, notification matrix, multi-tenant plan. **Start here for any feature audit.** |
| Technical architecture | `Architecture\Project Technical Architecture.docx` | Layered architecture (Angular + .NET REST + DDD + Repository + UoW), tech stack, security, OWASP standards. |

### Schema

| File | Path under `Documents_and_Diagrams\` | Purpose |
| --- | --- | --- |
| Data dictionary (tables) | `ER Digram\Data Dictionary (Table).docx` | Full field-level schema for 65 tables. Entities, FKs, constraints, defaults. |
| Data dictionary (views) | `ER Digram\Data Dictionary (Views).docx` | DB views (read-only query patterns, used by reports). Read on-demand. |
| Data dictionary (CSV) | `ER Digram\DataDictionary.xlsx` | Same content as table doc but tabular. Easier to grep. |
| ER diagram | `ER Digram\ER-Diagram.png` | Visual schema. **Note:** PNG resolution too low to read labels; use data dictionary instead. |

### API surface

| File | Path under `Documents_and_Diagrams\` | Purpose |
| --- | --- | --- |
| API documentation | `Swagger\SoCal API Documentation.docx` | 49 controllers, 233 endpoints. Per-endpoint request/response schemas. |
| API matrix | `Swagger\Socal-API-Matrix.xlsx` | Tabular endpoint inventory (URL, method, description). 233 endpoints: 101 GET, 31 POST, 44 PATCH, 28 DELETE, 29 PUT. |
| Postman collection | `Postman Collection\Patient Appointment API.postman_collection.json` | Concrete request/response examples. 37K-line file; grep for specific endpoints. Read directly. |

### Workflow

| File | Path under `Documents_and_Diagrams\` | Purpose |
| --- | --- | --- |
| Process flow PNG | `Workflow\Socal Process Flow Diagram.png` | **End-to-end user journey.** Login/Register -> User Type fork -> External (book PQME/AME/REVAL) or Internal (admin/supervisor flows) -> Submit -> Validate -> Notify -> Upload Documents -> Approve Docs -> Confirm -> Check-In/Out -> Reports. |
| Process flow drawio | `Workflow\Socal Process Flow Diagram.drawio` | Editable source for the PNG. Same content. |
| Project solution PNG | `Workflow\Socal Project Solution.png` | 7 backend modules + 1 frontend module. |

### Misc

| File | Path under `Documents_and_Diagrams\` | Purpose |
| --- | --- | --- |
| Readme | `Readme.txt` | Index of the 5 categories above. |

---

## Multi-tenant plan -- found

**Location: `P:\PatientPortalOld\Documents_and_Diagrams\Architecture\SoCal Project Overview Document.docx`.**

Line 113 (Project Brief):

> "SoCal Practice Management wants to develop a web-based application to smooth and streamline the current PQME and AME appointment booking process for the doctors they are managing. **Each doctor will have a separate website and database for appointment booking**, and the administrative areas of each of these websites will be **managed by SoCal staff**."

Line 599 (View Reports - Schedule Report):

> "Schedule Report (This needs to show the merged data of **all the three doctor's website**. This will be done via **replication of selective, non-sensitive appointment booking records** in a separate read-only database)"

**Strategy:** **database-per-doctor** with central admin staff (SoCal team), plus a separate **replicated read-only database** for cross-doctor reports (with PHI scrubbed via "selective, non-sensitive records").

**Phase 2 implication:** ABP defaults to row-level multi-tenancy via `TenantId` columns. OLD plan is database-per-tenant. This is a real architectural choice when Phase 2 starts -- match OLD or use ABP defaults. Not a Phase 1 decision; surfacing now so we don't silently lock in row-level by accident.

---

## Project structure (OLD)

7 backend modules + 1 frontend module:

- `PatientAppointment.Api` -- Web API, depends on Domain + Infrastructure
- `PatientAppointment.Domain` -- Business logic; depends on BoundedContext + DbEntities + Infrastructure + Models + UnitOfWork
- `PatientAppointment.BoundedContext` -- DDD bounded contexts; depends on DbEntities + Models
- `PatientAppointment.UnitOfWork` -- UoW pattern impl; depends on BoundedContext + DbEntities
- `PatientAppointment.Infrastructure` -- Repos + cross-cutting; depends on BoundedContext + DbEntities + Models + UnitOfWork
- `PatientAppointment.Models` -- DTOs / view models; depends on DbEntities
- `PatientAppointment.DbEntities` -- EF entities (root)
- `PatientAppointment.Portal` -- Angular frontend (single project)

OLD-to-NEW mapping is documented in branch CLAUDE.md ("OLD-to-NEW translation map" section).

---

## Tech stack (OLD)

- .NET Core 2.0 / .NET Framework 4.8
- EF Core 2.0
- MS SQL 2019
- Redis Cache
- Angular 7
- Redux + RxJs + NodeJs + Express + Webpack
- IIS 8.0 + Windows Server 2016
- Third-party packages of note:
  - **ClosedXML 0.104.2** (Excel export -- replace with NEW lib)
  - **iTextSharp 5.5.13.4** (PDF generation -- replace)
  - **NodaTime 2.3.0** (date/time)
  - **DocumentFormat.OpenXml.Framework 3.1.1** + **OpenXmlPowerTools 4.5.3.2** (DOCX output -- explicitly out of scope per branch CLAUDE.md)

---

## Quick lookup by topic

When auditing a feature, use this section to jump to the relevant OLD doc passage.

### Roles and permissions

- **External roles (4):** Patient, Adjuster, Patient Attorney, Defense Attorney -- `socal-project-overview.md` lines 119-127
- **Internal roles (3):** Clinic Staff, Staff Supervisor, IT Admin -- lines 129-135
- **"Appointment Owner"** is NOT a role -- it's the user who created an appointment -- line 173 (footnote)
- External use case matrix -- lines 141-171
- Internal use case matrix -- lines 175-217
- Schema: `Roles`, `RoleUserTypes`, `RoleAppointmentTypes`, `RolePermissions` (custom RBAC -- replace with ABP)

### External user flow (Phase 1 priority)

- Registration -- `socal-project-overview.md` lines 221-227 (user selects type, predefined fields populated)
- Login -- lines 229-235 (Email + Password, no MFA in spec)
- Forgot password -- lines 237-239 (reset link to registered email)
- Manage profile -- lines 241-245 (view/edit profile + reset password)
- Appointment request workflow -- lines 257-275 (7 steps)
- Patient record validation (dedup rule: 3 of 6 fields match: Last name, DOB, Phone, Email, SSN, Claim Number) -- lines 277-293
- Patient intake form fields (per role) -- lines 295-349
- REVAL flow (load original by confirmation number, modify, resubmit) -- lines 353-357
- Appointment statuses -- lines 361-385 (Pending, Approved, Rejected, NoShow, Cancelled-NoBill, Cancelled-Late, Rescheduled-NoBill, Rescheduled-Late, Checked-In, Checked-Out, Billed)
- View appointment (requires login + grant via AppointmentAccessor) -- lines 397-403
- Upload package documents (email link triggered after staff approval; multi-reminder) -- lines 405-413
- Upload Joint Declaration (AME only; auto-cancel if missing close to due date) -- lines 415-419
- Reschedule request (window from system params; supervisor approves) -- lines 421-427
- Cancellation request (supervisor approves) -- lines 429-433
- Submit query (logged-in user emails staff; no further tracking) -- lines 435-437

### Internal user flow (dependencies for external flow)

- Dashboard (4-6 counters + 2-4 widgets) -- `socal-project-overview.md` lines 247-255
- View all appointments (filter by confirmation #, type, location, status, doc status, JDF status) -- lines 439-453
- Change log (per-field audit, filterable) -- lines 455-469
- Accept/reject appointment request (with patient match check; on accept, select responsible team member) -- lines 471-485
- Accept/reject package documents (per-document; rejection notes to uploader) -- lines 487-493
- Accept/reject JDF -- lines 495-501
- Accept/reject reschedule (creates new appt record with same confirmation # + new date in Approved status) -- lines 503-513
- Accept/reject cancellation -- lines 515-523
- Check-in/check-out (day-of view, manual status update) -- lines 525-527
- Manage doctor location preference -- lines 529-531
- Manage doctor appointment type preference -- lines 533-535
- **Manage doctor availability and timeslots** -- lines 537-539 -- **booking dependency for external flow**
- Manage appointment request fields (custom fields, up to 10 additional) -- lines 541-549
- Manage users (IT Admin: add/remove internal; block external) -- lines 551-553
- Manage system parameters -- lines 555-567
- Manage notification templates (events list embedded) -- lines 569-593
- View reports (Schedule Report, Appointment Request Report, Excel ODBC Link) -- lines 595-613

### Schema topics (data-dictionary-table.md)

Critical entities for external user flow (in order of dependency):

- `Users` -- UserId, RoleId, EmailId, names, DOB, phone, IsActive
- `Roles`, `RolePermissions`, `RoleUserTypes`, `RoleAppointmentTypes` -- custom RBAC (replace with ABP)
- `Patients` -- full patient profile, with translator/interpreter fields (`InterpreterVendorName`, `OthersLanguageName`), language preference (`LanguageId`)
- `Appointments` -- main entity. Key fields: `RequestConfirmationNumber`, `OriginalAppointmentId` (for reschedules -- new row, same confirmation #), `DoctorAvailabilityId`, `PrimaryResponsibleUserId`, `AppointmentApproveDate`
- `AppointmentInjuryDetails` -- multiple per appointment, with `WcabOfficeId`, `ClaimNumber`, `BodyParts` (nvarchar Max -- likely JSON), `IsCumulativeInjury`, `DateOfInjury` + `ToDateOfInjury` (range for cumulative)
- `AppointmentClaimExaminers`, `AppointmentPrimaryInsurance` -- per-injury contact info
- `AppointmentDefenseAttorneys`, `AppointmentPatientAttorneys`, `AppointmentEmployerDetails` -- per-appointment contact info
- `AppointmentAccessor` (sharing) -- per-appointment grant: `FirstName`, `LastName`, `EmailId`, `AccessTypeId` (view/manage), `RoleId`
- `AppointmentDocuments` + `AppointmentNewDocuments` + `AppointmentJointDeclarations` -- 3 document tables (note: appears to be a refactor in progress in OLD)
- `AppointmentChangeRequests` -- handles BOTH reschedule and cancellation in one table; type implicit by which fields populated (`ReScheduleReason` vs `CancellationReason`)
- `AppointmentChangeLogs` -- per-field audit log with `IsMailSent`, `IsInternalUserUpdate`
- `Doctors`, `DoctorPreferredLocations`, `DoctorsAppointmentTypes`, `DoctorsAvailabilities` -- doctor config (single doctor per OLD app instance)
- `AppointmentTypes` -- PQME, AME, PQME-REVAL, AME-REVAL
- `AppointmentStatuses` -- 11 statuses (see external flow above)
- `Locations` -- clinic locations w/ `ParkingFee` decimal
- `WcabOffices` -- California WCAB offices (PK + name + abbreviation + address)
- `SystemParameters` -- single-row config: `AppointmentLeadTime`, `AppointmentMaxTimeAME`, `AppointmentMaxTimePQME` (separate windows AME vs PQME), `AutoCancelCutoffTime`, `ReminderCutoffTime`, `AppointmentDurationTime`, `AppointmentDueDays`, `AppointmentCancelTime`, `JointDeclarationUploadCutoffDays`, `PendingAppointmentOverDueNotificationDays`
- `Templates` -- email + SMS body in same record, keyed by `TemplateTypeId` + `TemplateCode`
- `SMTPConfigurations` -- outbound email config (replace with ABP email service)
- `GlobalSettings` -- `TwoFactorAuthentication`, `SocialAuth`, `AutoTranslation` flags exist (so OLD app already supports 2FA + social auth as configurable)

### API endpoints (api-matrix CSV)

Critical for external user flow:

- **Authentication:** `/api/UserAuthentication/login` (POST), `/postforgotpassword` (POST), `/putforgotpassword` (PUT), `/putemailverification` (PUT)
- **Authorization:** `/api/UserAuthorization/authorize` (POST), `/logout` (POST), `/access` (POST)
- **Users:** `/api/Users` CRUD
- **User lookups:** `/api/UserLookups/{cities,externaluserrole,gender,roletype,states}lookups`
- **Appointment booking:** `/api/Appointments` CRUD + `/search` + `/search/GetById`
- **Appointment lookups:** `/api/AppointmentRequestLookups/*` (16 lookup endpoints incl gender, languages, locations, states, WCAB offices, doctors-availabilities, etc.)
- **Sharing:** `/api/appointments/{id}/AppointmentAccessors` CRUD
- **Reschedule/Cancel:** `/api/appointments/{id}/AppointmentChangeRequests` CRUD
- **Documents:** `/api/AppointmentDocuments` + `/api/appointments/{id}/AppointmentDocuments` + `/api/AppointmentNewDocuments` + `/api/AppointmentJointDeclarations` + `/api/DocumentUpload/*`
- **Injuries:** `/api/appointments/{id}/AppointmentInjuryDetails` CRUD

### Notifications

- Trigger events list -- `socal-project-overview.md` lines 569-593
- Schema: `Templates` (BodyEmail + BodySms in same row), `TemplateTypes`, `SMTPConfigurations`
- Reminders are multi-step (incomplete docs, JDF upload, due date approaching, due date + docs pending)

### Auto-cancellation rule

- AME appointments with missing JDF auto-cancel near due date -- `socal-project-overview.md` line 419
- Cutoff: `SystemParameters.JointDeclarationUploadCutoffDays`

---

## Cross-cutting docs (read alongside the per-feature audits)

These four `_*.md` files supplement the feature audits with cross-cutting investigation, cleanup tracking, and branding parameterization:

- `_slot-generation-deep-dive.md` -- complete walkthrough of OLD's slot-generation logic (two modes: by-date-range and by-day-of-week+month), all validation rules, the preview-then-confirm UX, and slot-status transitions. Cross-link from `staff-supervisor-doctor-management.md`.
- `_appointment-form-validation-deep-dive.md` -- complete walkthrough of OLD's booking form: 3 form modes (standard, REVAL, Re-Request), role-based auto-fill, location filter chain, frontend + backend validation rules (8 server-side rules + cross-field gates), patient deduplication 3-of-6 logic, slot transitions on update, and the typos OLD ships with that NEW should fix as "OLD bug, fixed for correctness". Cross-link from `external-user-appointment-request.md`.
- `_cleanup-tasks.md` -- the two cleanup tasks (Phase 0): remove Doctor user role + login from NEW, and remove `AppointmentSendBackInfo` (NEW extension not in OLD). Each with file paths to change, acceptance criteria, and sequencing. Run before Phase 1 feature work.
- `_branding.md` -- aggregated branding/theming inventory across all 32 feature audits + OLD asset paths + the parameterization plan (CSS custom properties + config-driven branding object + localization keys). Specifies the migration map from OLD hardcoded literals to NEW tokens. Phase 1 implementation order included.

## Stale / unused entities in OLD (do NOT port)

Discovered during the comprehensive entity-table audit on 2026-05-01:

- **`AppointmentWorkerCompensation`** -- table exists in `PatientAppointment.DbEntities/Models/`, frontend has `appointment-worker-compensation.ts` model, but NO Domain class, NO controller, NO API endpoint references it. Has a typo bug (`ModifiedById` typed as `DateTime?`). The fields it holds (`ClaimNumber, DateOfInjury, WcabAdj, WcabOfficeId`) are all already covered by the richer `AppointmentInjuryDetails` entity. **Conclusion: stale/orphaned. Do not port to NEW.**

## Things NOT to port (per branch CLAUDE.md precedence)

- **AWS S3** file storage paths (e.g., `DocumentAwsFilePath` columns) -- replace with NEW app's storage strategy
- **DOCX output** via `OpenXmlPowerTools` / `DocumentFormat.OpenXml` -- replace with non-editable PDF
- **iTextSharp** for PDF generation -- replace with NEW app's PDF lib
- **ClosedXML** for Excel -- replace with NEW app's Excel lib
- **Custom RBAC tables** (`Roles`, `RolePermissions`, `ApplicationModules`, `ApplicationObjects`) -- replaced by ABP permission system
- **`ApplicationUserTokens`** (custom JWT) -- replaced by OpenIddict in NEW
- **`LanguageContents` / `ModuleContents`** (custom i18n) -- replaced by ABP localization
- **`ApplicationExceptionLogs` / `RequestLogs` / `AuditRecords`** (custom audit) -- replaced by ABP auditing
- **`SMTPConfigurations`** (custom SMTP) -- replaced by ABP email service
- **`ApplicationTimeZones`** (custom TZ table) -- replaced by ABP / NodaTime
- **`CacheCollections` / `CacheKeys`** (custom cache) -- replaced by ABP distributed cache
- **`GlobalSettings`** (custom global config) -- replaced by ABP `ISettingProvider`
- **In-house custom NuGet packages** -- replace with ABP / standard libs

## Things TO port

- All **entity names** (Patient, Appointment, AppointmentInjuryDetails, AppointmentDocuments, AppointmentAccessor, AppointmentChangeRequests, etc.)
- All **field names** within entities (preserves data dictionary terminology used by stakeholders)
- All **business rules** (3-of-6 patient dedup, JDF auto-cancel, reschedule creates new record with same confirmation number, reval pre-loads from old confirmation #, etc.)
- All **status values** (appointment statuses, document statuses)
- All **UI labels** and **validation messages** (verbatim from OLD app)
- **Permission matrix** (which role can do what -- lines 141-217 of overview doc)
- **Notification trigger events** (lines 573-593)
- **Sharing semantics** (`AppointmentAccessor`: per-appointment grant via email, with view-vs-manage access type)
- **System parameters** (lead time, max time AME/PQME, cutoffs, durations, etc.)

---

## Open questions for Adrian (surfaced from the read)

These came out of the thorough read; flagging now to avoid blocking the audit.

1. **Account verification scope.** OLD app has `/api/UserAuthentication/putemailverification` endpoint, suggesting email-verify-on-registration. Is this Phase 1 scope, or do we use ABP's built-in verification flow?

2. **Role naming clarification.** OLD docs and OLD code use **Patient / Adjuster / Patient Attorney / Defense Attorney**. In your initial message you said **Applicant Attorney / Claim Examiner** -- those are NOT roles in the OLD model. "Claim Examiner" is a per-injury contact in the OLD schema (table `AppointmentClaimExaminers`), distinct from any user role. Were those renames you want to apply in NEW, or slip-ups in your description? My default: use OLD names verbatim unless you say otherwise.

3. **AppointmentDocuments vs AppointmentNewDocuments.** Two near-identical tables exist in OLD schema. Likely a refactor in progress in OLD code that was never finished. Do we port the NEW or the OLD shape into NEW app, or merge into one entity?

4. **Branding location.** You said OLD's colors and UI design, but customizable for other doctors. There's no `Branding` or `DoctorBrand` table in OLD schema -- branding is likely hard-coded in templates/CSS. Need to investigate the OLD Angular code (`patientappointment-portal/`) to find the coupling points before defining theming hooks. Confirm: I should investigate this as part of Phase 1 audit, output to `docs/parity/_branding.md`?

5. **2FA + Social Auth.** OLD's `GlobalSettings` has `TwoFactorAuthentication` and `SocialAuth` flags. Phase 1 scope, or skip until later? My read: skip for Phase 1 -- they were configurable, not enabled, and ABP supports both natively when we want them.

---

## Outstanding reads (deferred, on-demand)

These large files were not fully read during the initial audit pass -- read them directly from `P:\PatientPortalOld\Documents_and_Diagrams\` via grep / chunked reads on demand:

- `ER Digram\Data Dictionary (Views).docx` -- DB views, read-only query patterns
- `Swagger\SoCal API Documentation.docx` -- full API body with schemas (TOC read; bodies on-demand)
- `Postman Collection\Patient Appointment API.postman_collection.json` (37,543 lines) -- concrete payload examples

---

## Audit hand-off

Next step: external user flow audit (task #5). Per branch `CLAUDE.md` and the agreed `docs/parity/<feature>.md` template, the slice is:

1. Registration (4 external roles)
2. Login + email verification
3. Forgot password
4. Appointment booking (PQME + AME + RE-EVAL variants, intake form, slot selection, sharing)
5. View appointment (owner + accessors)
6. Reschedule request
7. Cancellation request
8. Account verification (if confirmed in scope per Q1 above)

Internal-user dependencies expected to surface (will pause and ask before expanding):

- Doctor availability + timeslots (`DoctorsAvailabilities`)
- Doctor location preference + appointment type preference
- Slot generation by Staff Supervisor
- Approval workflow (Clinic Staff for booking, Staff Supervisor for reschedule/cancel)
- Email/SMS notification triggers
- Custom field configuration (IT Admin)
- System parameters (lead time, max time, cutoffs)
