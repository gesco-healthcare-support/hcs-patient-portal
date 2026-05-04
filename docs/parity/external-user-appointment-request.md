---
feature: external-user-appointment-request
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentInjuryDetailDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentAccessorDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.ts
old-docs:
  - socal-project-overview.md (lines 257-385)
  - data-dictionary-table.md (Appointments + 13 related tables)
audited: 2026-05-01
re-verified: 2026-05-04
status: in-progress
priority: 1
strict-parity: true
depends-on:
  - external-user-registration  # accessor-email validation creates user accounts
  - it-admin-system-parameters  # lead time + max time per type read here
  - staff-supervisor-doctor-availability  # slot picker depends on this
---

# External user appointment request (booking)

## Purpose

The 7-step booking flow: external user (Patient, Adjuster, Applicant Attorney, Defense Attorney) selects appointment type + location, fills patient intake form (with multiple injuries, attorneys, employer, claim examiner, primary insurance sub-objects), uploads documents, picks a slot, optionally adds accessors (sharees), and submits. Result: a Pending appointment with a `RequestConfirmationNumber` (`A#####`), reserved slot, and notification emails to all stakeholders.

**Strict parity with OLD.** Replicate `UserDomain.Add` + `AppointmentDomain.Add` semantics on ABP.

## OLD behavior (binding)

### Form scope (per `appointment-add.component.ts` imports + lookups)

The booking form binds these models simultaneously:

- `Appointment` (root)
- `Patient` (with `phoneNumberType`, `isInterpreter`, `isOther` flags + interpreter vendor name + others-language name)
- `AppointmentInjuryDetail[]` (multiple injuries -- each with its own sub-objects)
  - per injury: `AppointmentClaimExaminer` (single, not array per code), `AppointmentPrimaryInsurance[]`, `AppointmentInjuryBodyPartDetail[]`
- `AppointmentDefenseAttorney[]` (per appointment, attorney roles only)
- `AppointmentPatientAttorney[]` -> NEW name **AppointmentApplicantAttorney[]** (per appointment, attorney roles only)
- `AppointmentEmployerDetail[]` (per appointment)
- `AppointmentAccessor[]` (sharees)
- `CustomFieldsValue[]` (IT-Admin-configured up to 10 additional intake fields)

Lookups loaded on form init (11 lookups):

`wcabofficeLookUps`, `languageLookUps`, `statesLookUps`, `phoneNumberTypeLookUps`, `internalUserNameLookUps`, `customFieldLookUps`, `cityLookUps`, `doctorsAvailabilitiesLookUps`, `locationLookUps`, `genderLookUps`, `doctorPreferredLocationLookUps`

System parameters loaded (single row): `AppointmentLeadTime`, `AppointmentMaxTimePQME`, `AppointmentMaxTimeAME`, `AppointmentMaxTimeOTHER`, `IsCustomField` flag, etc.

### Appointment type matrix (per spec doc + code)

OLD has FIVE appointment types, not four:

| Type | ID | Bookable by | Notes |
|------|----|-------------|------|
| PQME | 1 | Patient, Adjuster, Applicant Atty, Defense Atty | Panel Qualified Medical Examiner |
| AME | 2 | Applicant Atty, Defense Atty | Agreed Medical Examiner; requires JDF |
| PQME-REVAL | 3 | Patient, Adjuster, Applicant Atty, Defense Atty | Re-evaluation of approved PQME |
| AME-REVAL | 4 | Applicant Atty, Defense Atty | Re-evaluation of approved AME |
| OTHER | 5 | (TO VERIFY) | Surfaced via `SystemParameters.AppointmentMaxTimeOTHER` and `AppointmentDomain.AddValidation` line 147 |

The "OTHER" type was NOT in the overview spec doc but exists in OLD code/system parameters. **Strict parity: include it.**

### `AppointmentDomain.AddValidation` (binding rules)

For external users (`UserType.ExternalUser`):

1. `DoctorsAvailability.DoctorsAvailabilityId` must exist with `BookingStatusId == BookingStatus.Available`. Else `AppointmentBookingDateNotAvailable` validation message.
2. **Lead time gate:** `DateTime.Now + AppointmentLeadTime >= AvailableDate` -> reject (must book at least N days in advance per `SystemParameters.AppointmentLeadTime`).
3. **Max time gate (per appointment type):**
   - PQME / PQME-REVAL: `AvailableDate <= DateTime.Now + AppointmentMaxTimePQME`
   - AME / AME-REVAL: `AvailableDate <= DateTime.Now + AppointmentMaxTimeAME`
   - OTHER: `AvailableDate <= DateTime.Now + AppointmentMaxTimeOTHER`
4. **REVAL form gate** (`IsRevolutionForm` -- this misspelling is in OLD code; means "REVAL form"):
   - Original appointment (by `RequestConfirmationNumber`) must be `Approved`. Else "You can not Re-eval this appointment request because it's not yet approved."
   - IT Admin can REVAL non-approved appointments (override).
5. **Re-Request form gate** (`IsReRequestForm`):
   - Original appointment must be `Rejected`. Else "You not allowed to re apply appointment."
6. **Accessor role conflict check:** for each accessor whose email matches an existing user, verified+active, the accessor's `RoleId` must equal the user's `RoleId`. Else "Your added accessor '<email>' is already registered in our system with different user type."

Internal users skip lead time + max time checks.

### `AppointmentDomain.Add` (binding flow)

1. **Patient deduplication** via `IsPatientRegistered` (3-of-6 rule from spec):
   - Match if any 3 of: LastName, DOB, Phone, Email, SSN, ClaimNumber match an existing patient.
   - Match found -> set `appointment.PatientId = existingId`, `IsPatientAlreadyExist = true`. Don't create new Patient row.
   - No match -> insert new `Patient` row.
2. **Slot status transition:**
   - External user -> `BookingStatus.Reserved`, `AppointmentStatus = Pending`.
   - Internal user -> `BookingStatus.Booked`, `AppointmentStatus = Approved` (internal users skip clinic-staff approval).
3. **DueDate** = `DoctorsAvailability.AvailableDate`.
4. **Re-Request handling** (`IsReRequestForm`):
   - Mark old appointment as `Rejected` (idempotent; was already Rejected to qualify).
   - Reset `appointment.AppointmentId = 0` (insert new row).
   - Reuse the same `RequestConfirmationNumber` -- the new appointment gets the same confirmation # as the rejected original.
5. **Accessor IDs reset** (`accessor.AppointmentAccessorId = 0`) -- inserted as new rows.
6. Set `CreatedById = UserClaim.UserId`, `CreatedDate = DateTime.Now`.
7. Insert `Appointment` + cascade-insert all child rows (Patient, InjuryDetails, BodyParts, ClaimExaminers, PrimaryInsurances, DefenseAttorneys, PatientAttorneys, EmployerDetails, Accessors, CustomFieldsValues).
8. **Confirmation number generation:**
   - Re-Request: keep original.
   - Otherwise: `ApplicationUtility.GenerateConfirmationNumber(appointment.AppointmentId)` -- TO VERIFY exact format; NEW uses `A#####` with 5 zero-padded digits.
9. **Internal user only:** auto-create package documents via `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail` (since they skip approval, package docs are queued immediately).
10. **Accessors > 0:** call `AppointmentAccessorDomain.CreateAccountOfAppointmentAccessors(appointment)` -- this creates User accounts for accessor emails that don't already have one.
11. **Notifications:** Email + SMS to all stakeholders via `GetAppointmentStackHoldersEmailPhone` -> `SendSMS` + `SendEmail`. Twilio for SMS.

### Adjuster auto-fill UX rule

Per `appointment-add.component.ts` lines 145-149: when the booker's role is **Adjuster** and form is not REVAL, the form auto-fills `appointmentClaimExaminer.email = userEmail` and `appointmentClaimExaminer.name = loginUserName` and makes those fields readonly. Adjusters ARE the claim examiner on appointments they create.

### Patient intake form fields (per overview lines 295-349 + form code)

Common (all 4 external roles):

- `PanelNumber` (varchar 50)
- Patient: `FirstName, LastName, MiddleName, Email, DateOfBirth, Address, Street, City, StateId, ZipCode, PhoneNumber, CellPhoneNumner` (typo in OLD), `PhoneNumberType, GenderId, SocialSecurityNumber` (with checkbox "has SSN?"), `LanguageId, OthersLanguageName, InterpreterVendorName`
- `AdjusterName, AdjusterEmail` (single per appointment in spec, but `AppointmentClaimExaminer` table is per-injury -- spec is incomplete; code is authoritative)
- `AppointmentInjuryDetail[]`: `DateOfInjury, ToDateOfInjury` (range for cumulative), `ClaimNumber, WcabAdj, WcabOfficeId, IsCumulativeInjury, BodyParts` (multiple per injury)
- `AppointmentEmployerDetail[]`: `EmployerName, Occupation, PhoneNumber, Street, City, StateId, Zip`
- `AppointmentPrimaryInsurance[]` (per injury): `Name, PhoneNumber, FaxNumber, Street, City, StateId, Zip, InsuranceNumber, Attention`
- `AppointmentClaimExaminer` (per injury): `Name, Email, PhoneNumber, Fax, Street, City, StateId, Zip, ClaimExaminerNumber`

Attorney-roles only (Applicant Attorney + Defense Attorney):

- `AppointmentApplicantAttorney[]`: `AttorneyName, AttorneyEmail, FirmName, FirmAddress, PhoneNumber, FaxNumber, Street, City, StateId, Zip, WebAddress`
- `AppointmentDefenseAttorney[]`: same fields as ApplicantAttorney

After submit -> server returns `RequestConfirmationNumber` shown to user.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentsController.cs` | API: POST /api/Appointments |
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs` (1086 lines) | `Add`, `AddValidation`, `IsPatientRegistered`, `Update*`, `Get`, `GetAppointmentStackHoldersEmailPhone`, `SendSMS`, `SendEmail` |
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentInjuryDetailDomain.cs` (197 lines) | Injury sub-flows |
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentAccessorDomain.cs` (389 lines) | `CreateAccountOfAppointmentAccessors` -- auto-create user for accessor email; sharing semantics |
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDocumentDomain.cs` | `AddAppointmentDocumentsAndSendDocumentToEmail` -- internal-user auto-package-doc creation |
| `patientappointment-portal/.../appointments/add/appointment-add.component.{ts,html,routing,module}.ts` (1394+ lines TS) | The full-page booking form |
| `patientappointment-portal/.../appointment-injury-details/...` | Injury sub-form components |
| `patientappointment-portal/.../appointment-accessors/...` | Accessor sub-form components |
| `Infrastructure/Utilities/ApplicationUtility.GenerateConfirmationNumber` | Confirmation # format -- TO VERIFY format (likely `A#####`) |

## NEW current state

Per `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md` (verified -- current as of audit date):

### Already implemented in NEW

- `Appointment` aggregate root (FullAuditedAggregateRoot, IMultiTenant) with: TenantId, PanelNumber, AppointmentDate, IsPatientAlreadyExist, RequestConfirmationNumber, DueDate, InternalUserComments, AppointmentApproveDate, AppointmentStatus, PatientId, IdentityUserId, AppointmentTypeId, LocationId, DoctorAvailabilityId, PatientEmail, ApplicantAttorneyEmail, DefenseAttorneyEmail, ClaimExaminerEmail
- `AppointmentStatusType` enum -- 13 values (Pending=1 ... CancellationRequested=13)
- `AppointmentManager` domain service: Create + Update
- `AppointmentsAppService` -- `[RemoteService(IsEnabled=false)]`, manual controller exposes 14 endpoints
- 5-step slot validation (slot status, location match, type match, date match, time-in-range)
- `RequestConfirmationNumber` auto-gen as `A#####` (overrides client-supplied value)
- Slot transitions to `Booked` on create
- 4 permission keys: `Appointments`, `.Create`, `.Edit`, `.Delete`
- 14-method controller, 11 active xUnit facts + 4 skipped (gap markers)
- Angular `appointment-add.component.ts` (1594 lines) with patient/employer/applicant-attorney/authorized-users blocks; `minimumBookingDays = 3` client-side
- Angular `appointment-view.component.ts` (969 lines)
- Sub-entities exist: `AppointmentEmployerDetail`, `AppointmentAccessor`, `AppointmentApplicantAttorney`, `AppointmentInjuryDetail`, `AppointmentBodyPart`, `AppointmentClaimExaminer`, `AppointmentDefenseAttorney`, `AppointmentLanguage`, `AppointmentPrimaryInsurance`

### Known gaps in NEW (per its own CLAUDE.md gotchas)

1. **No state-machine guard** -- `AppointmentStatus` setter is public; any caller can set any status from any state.
2. **Slot is NOT released on delete/cancel** -- once Booked, stays Booked even if appointment is deleted. (`DeleteAsync` does not free the slot.)
3. **Race on confirmation # generation** -- no unique constraint on `(TenantId, RequestConfirmationNumber)`; concurrent inserts can duplicate.
4. **Server does not enforce 3-day lead time** -- Angular only.
5. **`view/:id` route lacks permissionGuard** -- any authenticated user in same tenant can deep-link.
6. **Permission gap on Create/Update at API** -- methods carry only `[Authorize]`, not `Appointments.Create`/`Edit`. Tests skip with "KNOWN GAP".
7. **3 parallel form approaches** in Angular -- modal (FormBuilder), Add (FormBuilder), View (ngModel). View page divergence likely to drift.
8. **`getList` proxy sends 18 query params** but server `GetAppointmentsInput` defines 7 -- proxy out of sync.
9. **`console.log('Date check:', ...)` left in `appointment-add.component.ts:1546`.**

## Gap analysis (strict parity with OLD)

Severity: **B** = blocker, **I** = important, **C** = cosmetic.

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| `AppointmentStatus` field | int FK to `AppointmentStatuses` table | `AppointmentStatusType` enum (13 values) | None -- equivalent. Statuses match. | -- |
| State machine | OLD `UpdateValidation` enforces transitions per status | NEW: no guard, public setter | **Add state-machine guard** in `Appointment.SetStatus(...)` method; reject invalid transitions | B |
| Re-Request flow (rejected -> new with same confirmation #) | `IsReRequestForm` re-uses confirmation # | Not implemented | **Add `ReSubmitAppointmentAsync`** to AppointmentManager; verify original is `Rejected`; new appt gets same confirmation # | B |
| REVAL flow (PQME/AME -> RE-EVAL with prefilled intake) | `IsRevolutionForm` checks original is Approved; UI loads original to prefill | Not implemented in NEW Add flow | **Add `CreateRevalAppointmentAsync(originalConfirmationNumber)`** in AppointmentManager; load original, prefill DTO, validate original Approved (or IT Admin override) | B |
| OTHER appointment type | 5th type beyond PQME/AME/REVALs | NEW seeds AppointmentTypes; verify "OTHER" present | **Verify NEW's `AppointmentTypeDataSeedContributor` includes OTHER** (or add it) | I |
| Lead time + per-type max time | `SystemParameters.AppointmentLeadTime` + `AppointmentMaxTimePQME/AME/OTHER` enforced server-side | NEW Angular only; server does not check | **Add server-side lead-time + max-time validation** in `AppointmentManager.CreateAsync` | B |
| Patient dedup (3-of-6 rule) | `IsPatientRegistered(appointment, out int patientId)` | Not implemented | **Add `IPatientRepository.FindMatchByDeduplicationRule(lastName, dob, phone, email, ssn, claimNumber)`**; in CreateAsync: if match -> use existing PatientId + set `IsPatientAlreadyExist=true`; else create new Patient | B |
| Internal user skips approval | OLD: internal user -> status=Approved + slot=Booked immediately; auto-creates package docs | NEW: same logic? TO VERIFY in `AppointmentsAppService.CreateAsync` | **Verify**; if missing, add | B |
| Slot transition: external -> Reserved, internal -> Booked | OLD has 2-step (Reserved on book, Booked on approval) | NEW always sets `Booked` | **Add Reserved status to slot booking statuses if not present**; transition Reserved on external book, Booked on approval. Match OLD. | B |
| Slot release on cancel/reschedule | TO VERIFY OLD behavior in cancellation flow | NEW: slot stays Booked forever (gap noted in NEW's CLAUDE.md) | **Add slot release** in cancel + reschedule reject paths. (Resolved during cancellation/reschedule audit.) | B |
| Patient intake fields (translator/interpreter, others-language) | `Patients.LanguageId`, `OthersLanguageName`, `InterpreterVendorName` | Verify Patient entity has these in NEW | **Verify**; add if missing | B |
| Multiple injuries per appointment | OLD: `AppointmentInjuryDetail[]`, each with `BodyParts[]`, `PrimaryInsurance[]`, `ClaimExaminer[]` | NEW: AppointmentInjuryDetail entity exists; verify multi-instance | **Verify Angular form supports add/remove injury rows** + sub-rows; add if missing | I |
| `AppointmentEmployerDetail[]` (NEW currently treats as 1:1 per CLAUDE.md) | OLD allows multiple | NEW: "One-to-one employer detail per appointment" per CLAUDE.md | **Change to 1:N**; allow multiple employer rows | I |
| `AppointmentClaimExaminer` | OLD: per-injury entity (FK to AppointmentInjuryDetailId) | NEW: AppointmentClaimExaminer entity exists | **Verify FK to InjuryDetail (not Appointment)**; verify Angular form scopes claim examiner to each injury | I |
| `AppointmentPrimaryInsurance` | OLD: per-injury, multiple per injury | Verify NEW shape | **Verify** | I |
| Adjuster auto-fill as Claim Examiner | OLD: form pre-fills CE.email/name from logged-in adjuster + readonly | NEW: TO VERIFY | **Add to NEW Angular `appointment-add.component.ts`**: detect Adjuster role, prefill + readonly | C |
| `AppointmentAccessor` (sharing) | Adding accessor with non-existing email auto-creates user account; existing user must have matching role | NEW has AppointmentAccessor entity | **Add `CreateAccountOfAppointmentAccessorsAsync`** equivalent; validate role match for existing users (via IObjectExtensionManager IsExternalUser + RoleId) | B |
| Custom Fields | IT Admin configures up to 10 additional intake fields; rendered dynamically | NEW has `AppointmentTypeFieldConfigs` (renamed) | **Verify dynamic rendering in NEW form**; ensure `IsCustomField` system flag respected | I |
| Notifications (Email + SMS) on book | OLD: SendEmail + SendSMS to all stakeholders | NEW: `SubmissionEmailHandler` + `SendAppointmentEmailJob` exist; SMS not visible | **Add SMS support** (Twilio in OLD; ABP can use any SMS provider via `ISmsSender`); strict parity = SMS to all stakeholders | I |
| `OriginalAppointmentId` for reschedule (new row, same confirmation #) | OLD field on Appointment | Not in NEW Appointment | **Add `OriginalAppointmentId` Guid? FK** | B (resolved during reschedule audit) |
| `ReScheduleReason`, `ReScheduledById`, `CancellationReason`, `CancelledById`, `RejectionNotes`, `RejectedById`, `PrimaryResponsibleUserId` | OLD fields on Appointment | Not in NEW Appointment | **Add all fields** | B (resolved during reschedule/cancel/staff-approval audits) |
| Confirmation # uniqueness | No constraint per OLD code; race condition exists in OLD too | NEW: same gap | **Add unique constraint** on `(TenantId, RequestConfirmationNumber)` -- security/correctness improvement, not parity-breaking | I |
| `console.log('Date check:', ...)` in NEW | -- | Production debug log | **Remove** | C |
| `AppointmentSendBackInfo` (NEW extension) | Not in OLD | Exists in NEW | **Remove** per task #8 | -- (cleanup task) |

## Internal dependencies surfaced

- **`SystemParameters` table** -- IT Admin slice. Contains lead time + per-type max times read by booking. Audit slice: `it-admin-system-parameters.md`.
- **`DoctorsAvailability` slot generation** -- Staff Supervisor slice. Booking depends on slots existing. Audit slice: `staff-supervisor-doctor-availability.md`.
- **`DoctorPreferredLocations` + `DoctorsAppointmentTypes`** -- Staff Supervisor configures which locations + types each doctor accepts. Lookup endpoints filter through these. Audit slice: `staff-supervisor-doctor-preferences.md`.
- **`AppointmentTypes` master + the 5 types (incl OTHER)** -- IT Admin or seeded; verify NEW seeds all 5 types.
- **`CustomFields` / `AppointmentTypeFieldConfigs`** -- IT Admin slice.
- **WCAB office master data** -- already in `WcabOffices` table; lookup-only, no separate audit.
- **City / State / Country / Language master data** -- lookup-only.
- **`AppointmentChangeLogs`** -- audit trail for field changes; written by domain events. Audit slice: `change-log.md`.

## Branding/theming touchpoints

- **Email templates** for booking notifications: `EmailTemplate.AppointmentRequest*` (TO LOCATE; multiple templates per stakeholder type)
- **SMS templates** in `Templates` table (`BodySms` field) -- per `TemplateCode`
- **Form labels + copy** -- "Welcome to {ClinicName}", "Patient Intake Form", section headings
- **Logo + primary color** -- form header, success page

## Replication notes

### ABP-specific wiring (strict parity)

- **AppointmentManager.CreateAsync** (DomainService): full booking flow. Cascade-saves child entities via `IRepository.InsertAsync` calls within a single Unit of Work.
- **State machine:** add `Appointment.SetStatus(AppointmentStatusType target)` method that validates transition via lookup table. Throw `InvalidStatusTransitionException` if not allowed. Move setter to `protected` to force use.
- **Patient dedup:** new method on `IPatientRepository` -- `FindByDeduplicationKeysAsync(lastName, dob, phone, email, ssn, claimNumber)`. Returns `Patient?`. Logic: count matches across the 6 fields per row, return first row with count >= 3. (Implementation note: ABP repo + LINQ-to-EF should handle this; benchmark on Patient table size.)
- **Slot validation:** retain existing 5-step + add lead-time + per-type max-time gates server-side.
- **Confirmation # unique constraint:** `builder.HasIndex(x => new { x.TenantId, x.RequestConfirmationNumber }).IsUnique()` in `OnModelCreating`.
- **Internal-user fast-path:** check `CurrentUser.IsInRoleAsync(...)` for any internal role; if true -> `slot=Booked`, `status=Approved`, trigger package-doc creation.
- **External-user path:** `slot=Reserved`, `status=Pending`. Add `BookingStatusType.Reserved` to slot enum.
- **Accessor account creation:** check `IIdentityUserManager.FindByEmailAsync(accessor.Email)`. If null -> create user with random password, mark `IsExternalUser=true`, assign role from accessor's intended role, send invitation email. If exists -> verify role match (via `IsExternalUser` + accessor's role match).
- **Notifications:** `IDistributedEventBus.PublishAsync(new AppointmentSubmittedEto { ... })`. Subscribers: `SubmissionEmailHandler` (already exists), new `SubmissionSmsHandler`. SMS via `ISmsSender` registered with Twilio module (or `Volo.Abp.Sms.Twilio` package).

### Things NOT to port

- Twilio direct SDK use (OLD has `ITwilioSmsService`); NEW uses `ISmsSender` abstraction.
- Custom `IExceptionUow` ApplicationExceptionLogs writes; ABP has structured logging via `ILogger<T>` + audit logs.
- Stored proc `spm.spAppointmentRequestList` (OLD lists via stored procs); NEW uses LINQ-to-EF.
- `IsRevolutionForm` typo -- in NEW, name the flag `IsReval` correctly.

### Open items requiring code reads during impl

- Verify `AppointmentTypeDataSeedContributor` includes OTHER.
- Verify `Patient` entity has translator/interpreter fields.
- Verify `AppointmentEmployerDetail` allows 1:N (currently 1:1 per NEW CLAUDE.md).
- Locate `EmailTemplate.AppointmentRequest*` templates in OLD.
- Verify NEW form supports dynamic add/remove of injury rows.
- Confirm `AppointmentClaimExaminer` FK is to `AppointmentInjuryDetailId` (per OLD), not `AppointmentId`.

### Verification (manual test plan)

1. Login as Patient -> book PQME -> 7-step flow completes -> confirmation # shown
2. Same flow as Adjuster -> Claim Examiner pre-fills with adjuster email + readonly
3. Same flow as Applicant Attorney -> can pick AME or PQME; adds attorney info; can add multiple injuries
4. Same flow as Defense Attorney -> ditto
5. Patient with same lastname+dob+email as existing -> dedup matches; `IsPatientAlreadyExist=true`
6. Book outside lead-time window -> rejected with "Please book at least N days in advance"
7. Book outside per-type max-time -> rejected
8. REVAL flow: book PQME-REVAL with original confirmation # of an Approved PQME -> succeeds; intake fields prefilled
9. REVAL flow with original Pending -> rejected (unless IT Admin)
10. Re-Request flow: pick a Rejected appointment -> resubmit; new appt has same confirmation #
11. Add accessor with new email -> account created + invitation email sent
12. Add accessor with existing user email + matching role -> succeeds
13. Add accessor with existing user email + different role -> rejected
14. Submit -> emails + SMS to patient, attorneys, claim examiner, employer (if email present)
15. Internal user (Clinic Staff) books on behalf -> status=Approved immediately, package docs auto-queued

Tests:
- `AppointmentManagerTests.CreateAsync_ExternalUser_*` (lead time, max time, slot reserved, status=Pending)
- `AppointmentManagerTests.CreateAsync_InternalUser_*` (slot booked, status=Approved, auto-package-docs)
- `AppointmentManagerTests.CreateAsync_PatientDedup_3of6Fields_LinksExistingPatient`
- `AppointmentManagerTests.CreateAsync_RevalFlow_*`
- `AppointmentManagerTests.CreateAsync_ReRequestFlow_*`
- `AppointmentAccessorTests.CreateAsync_NewEmail_CreatesUserAndSendsInvite`
- `AppointmentAccessorTests.CreateAsync_ExistingEmail_DifferentRole_Rejects`
- `Appointment.SetStatus_InvalidTransition_Throws`
- Synthetic data only.

## Phase 11a annotations [2026-05-04]

> **Phase 11a slice -- pure validators extracted, manager rewrite is
> Phase 11b/11c.** The full <c>AppointmentManager.CreateAsync</c>
> rewrite (10-step orchestration with Patient dedup repository method,
> Confirmation # generator, sub-entity wiring, Accessor manager, event
> raise) is large enough to warrant its own commit. Phase 11a lands the
> pure helpers it will consume, plus 16 unit tests, so 11b can focus on
> orchestration with confidence in the rule logic.

| Aspect | OLD source | NEW Phase 11a status |
|--------|-----------|----------------------|
| Confirmation-number format `A` + 5-digit zero pad | `ApplicationUtility.GenerateConfirmationNumber` | [IMPLEMENTED 2026-05-04 - pending testing] -- `AppointmentBookingValidators.FormatConfirmationNumber(int)` extracted as `internal static`. Tenant-scoped int sequence is Phase 11b. |
| Race-safe confirmation # generation | OLD's `MAX(...) + 1` was racy under concurrent bookings | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 11f) -- DB-level unique index on `Appointment(TenantId, RequestConfirmationNumber)` (`IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber`, auto-filtered to `TenantId IS NOT NULL`) backs the in-memory MAX+1 read. `AppointmentsAppService.CreateAsync` wraps the generate + Manager.CreateAsync sequence in `ConfirmationNumberRetryPolicy.RunWithRetryAsync` (5 attempts default) -- the loser of a concurrent insert sees a unique-constraint violation, re-reads MAX, retries. Migration `Phase11f_AppointmentConfirmationNumberUniqueIndex`. |
| Lead-time gate (slot >= today + leadTime) | `AppointmentDomain.cs` Add path | [IMPLEMENTED 2026-05-04 - pending testing] -- `IsSlotWithinLeadTime` helper (Phase 11a). Phase 11b wires it into `AppointmentsAppService.CreateAsync` via `BookingPolicyValidator.ValidateAsync`; throws `BusinessException(AppointmentBookingDateInsideLeadTime)` with `leadTimeDays` data. |
| Per-type max-time gate (slot <= today + maxTime) | `AppointmentDomain.cs` Add path | [IMPLEMENTED 2026-05-04 - pending testing] -- `IsSlotWithinMaxTime` + `ResolveMaxTimeDaysForType` helpers (Phase 11a). Phase 11b wires them in via `BookingPolicyValidator.ValidateAsync`; throws `BusinessException(AppointmentBookingDatePastMaxHorizon)` with `maxTimeDays` data. |
| Permission gate on `AppointmentsAppService.CreateAsync` | -- | [IMPLEMENTED 2026-05-04 - pending testing] -- closed the prior `[Authorize]`-only gap by adding `[Authorize(Appointments.Create)]`. Mirrors NEW's existing Edit/Delete permission gates. |
| Booking-policy orchestrator | -- | [IMPLEMENTED 2026-05-04 - pending testing] -- `BookingPolicyValidator` reads `SystemParameter` + `AppointmentType.Name` and orchestrates lead-time + max-time. Pure helper `EvaluateBookingPolicy` extracted as `internal static` for unit-testability; 11 boundary-case unit tests. |
| Patient dedup field set divergence (NEW finding 2026-05-04) | OLD's 6 fields: LastName / DOB / Phone / Email / SSN / ClaimNumber | NEW's existing `IPatientRepository.FindBestMatchAsync` uses: FirstName / LastName / DOB / SSN / Phone / ZipCode | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 11k) -- new repo method `IPatientRepository.GetDeduplicationCandidatesAsync(tenantId, lastName, dob, phone, email, ssn, claimNumbers)` implements the OLD-parity SQL prefilter (any-of-5-fields OR; ClaimNumber prefilter dropped because NEW's `Patient` does not carry the column -- claim numbers live on `AppointmentInjuryDetail`. Caller compensates by counting matches across the remaining 5 + injury-side ClaimNumber via `AppointmentBookingValidators.IsPatientDuplicate`). The legacy `FindBestMatchAsync` stays in place for any non-OLD-parity caller; OLD-parity callers (booking flow) use the new method. AppService wiring is part of Phase 11h. |
| Patient dedup 3-of-6 rule (LastName / DOB / Phone / Email / SSN / ClaimNumber) | `AppointmentDomain.cs:736-776` `IsPatientRegistered` | [IMPLEMENTED 2026-05-04 - pending testing] -- `CountMatchingDeduplicationFields` + `IsPatientDuplicate(threshold = 3)`. Case-insensitive trim; nulls do not match nulls. Dedup repo method (`IPatientRepository.FindMatchByDeduplicationRule`) is Phase 11b. |
| AppointmentManager.CreateAsync rewrite (10 steps) | `AppointmentDomain.cs` Add | [DESCOPED 2026-05-04 - Phase 11b] -- consumes the Phase 11a helpers; orchestrates Patient creation, sub-entity wiring, status transitions, slot Available -> Reserved/Booked, accessor management, event raise. |
| Re-Submit lifecycle gate (source must be `Rejected`) | `AppointmentDomain.cs:176-184` `IsReRequestForm` validation | [IMPLEMENTED 2026-05-04 - pending testing] -- `AppointmentLifecycleValidators.CanResubmit(status)` (Phase 11e). Maps to error code `AppointmentReSubmitSourceNotRejected` + verbatim OLD message `"You not allowed to re apply appointment"`. Manager wiring + AppService endpoint deferred to Phase 11g. |
| Reval lifecycle gate (source must be `Approved`; admin override surfaces hint, not free pass) | `AppointmentDomain.cs:162-174` `IsRevolutionForm` validation | [IMPLEMENTED 2026-05-04 - pending testing] -- `AppointmentLifecycleValidators.CanCreateReval(status, isItAdmin)` + `ResolveRevalRejectionCode(isItAdmin)` (Phase 11e). Two error codes: `AppointmentRevalSourceNotApproved` (verbatim OLD line 168 patient-facing) + `AppointmentRevalSourceNotApprovedAdminHint` (verbatim OLD line 172 admin hint). Manager wiring + AppService endpoint deferred to Phase 11g. |
| Confirmation # carry-forward decision (ReSubmit reuses, Reval generates fresh) | `AppointmentDomain.cs:262-271` Add path | [IMPLEMENTED 2026-05-04 - pending testing] -- `AppointmentLifecycleValidators.ResolveConfirmationNumber(flow, source, generated)` (Phase 11e) is the pure decision; the caller threads through the right value at Manager time. |
| `ReSubmitAppointmentAsync` Re-Request flow (end-to-end Manager + AppService wiring) | `AppointmentDomain.cs` Add path | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 11g) -- `AppointmentManager.LoadResubmitSourceAsync(confNum)` looks up source via `IAppointmentRepository.FindByConfirmationNumberAsync`, gates via `AppointmentLifecycleValidators.CanResubmit`, throws `BusinessException(AppointmentReSubmitSourceNotRejected)`. AppService surfaces `IAppointmentsAppService.ReSubmitAsync(confNum, AppointmentCreateDto)`; controller route `POST api/app/appointments/re-submit/{sourceConfirmationNumber}`. The shared booking pipeline (`CreateAppointmentInternalAsync`) reuses the source confirmation number via `AppointmentLifecycleValidators.ResolveConfirmationNumber(ReSubmit, ...)`. |
| `CreateRevalAppointmentAsync` REVAL flow (end-to-end Manager + AppService wiring) | `AppointmentDomain.cs` Add path | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 11g) -- `AppointmentManager.LoadRevalSourceAsync(confNum, callerIsItAdmin)` looks up source, gates via `AppointmentLifecycleValidators.CanCreateReval`, picks the right error code via `ResolveRevalRejectionCode`. AppService surfaces `IAppointmentsAppService.CreateRevalAsync(confNum, AppointmentCreateDto)`; controller route `POST api/app/appointments/create-reval/{sourceConfirmationNumber}`. Generates a fresh confirmation number per OLD line 268. |
| `IAppointmentRepository.FindByConfirmationNumberAsync(confNum)` | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 11g) -- new repo method on `IAppointmentRepository` with EF Core impl in `EfCoreAppointmentRepository`. ABP's `IMultiTenant` data filter scopes the query to the calling tenant; ordered by `CreationTime` descending so chained ReSubmit-of-ReSubmit picks the most recent row. |
| `AppointmentLifecycleValidators` accessibility (Domain visibility) | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 11g) -- moved from `Application/Appointments/` (internal) to `Domain/Appointments/` (public) so `AppointmentManager` can compose without an architectural inversion. Helpers remain pure; tests still exercise them by name with no other change. |
| `IAppointmentAccessorManager.CreateOrLinkAsync` | `AppointmentAccessorDomain.cs` | [DESCOPED 2026-05-04 - Phase 11b] -- creates AbpUser, sends invitation email, links accessor row. |
| `CloneForRescheduleAsync` cascade-copy (11.2) | `AppointmentChangeRequestDomain.cs` | [DESCOPED 2026-05-04 - Phase 11c] -- depends on Phase 12 approval flow first. |
| Angular `appointment-add.component.ts` cleanup (`console.log` removal) | UI-only | [IMPLEMENTED 2026-05-04 - pending testing] -- removed the production-bound `console.log('Date check:', ...)` debug log inside `isDateBeforeMinimum` (line 1955). Replaced with a comment pointing readers to the server-side `BookingPolicyValidator` as the authoritative gate. |
| Max-time UI gate becomes informational | UI-only | [DESCOPED 2026-05-04 - Phase 11d-extended] -- the UI's hardcoded `minimumBookingDays = 3` would ideally read from `SystemParameter.AppointmentLeadTime` via a config endpoint. Server-side enforcement (Phase 11b) means stale UI values are non-blocking; UI catches up later. |
| Multi-injury / multi-employer support | UI-only | [DESCOPED 2026-05-04 - Phase 11d-extended] -- multi-row support already exists in entity / DTO shape; the standalone Angular form needs FormArray rework. Non-blocking for backend integration. |
