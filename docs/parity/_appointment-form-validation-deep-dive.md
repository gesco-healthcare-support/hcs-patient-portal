---
type: deep-dive
parent-audit: external-user-appointment-request.md
audited: 2026-05-01
status: investigation-complete
---

# Appointment form -- validation, restrictions, and field logic deep dive

Comprehensive walkthrough of how OLD's booking form (`appointment-add.component.ts`, 1394 lines) and the backend (`AppointmentDomain.cs` -- `CommonValidation`, `AddValidation`, `UpdateValidation`, `IsPatientRegistered`) gate, restrict, and pre-fill the appointment-request submission. Reverse-engineered from those files.

This is the operational source of truth for the booking-form parity work in `external-user-appointment-request.md`.

## The form has three modes

The single `AppointmentAddComponent` handles three operational modes. The mode is determined by the URL query param `type` and the user's role + state.

### Mode 1 -- Standard new booking

Default mode. User picks an appointment type, fills the patient intake form fully, picks a slot, submits. `RequestConfirmationNumber` is generated server-side as `A#####`.

### Mode 2 -- REVAL booking (`isRevolutionForm = true`)

Triggered when the user picks an appointment type of `AMEREEVAL`, `PQMEREEVAL`, or `OTHER` (with `type != "1"`). Behavior:

1. The form prompts for the **original confirmation number** (the `A#####` of an Approved earlier appointment).
2. On entry + click, the form calls `appointmentsService.search(...)` -- backed by `AppointmentsSearchController` -- to look up the original.
3. The original must satisfy:
   - `appointmentStatusId == Approved` -- else: `"You can 'Re-eval' appointment only if your appointment is approved."`
   - `appointmentTypeId` family matches the chosen REVAL family:
     - PQME or PQME-REVAL <-> chosen PQME or PQME-REVAL
     - AME or AME-REVAL <-> chosen AME or AME-REVAL
     - OTHER <-> chosen OTHER
   - Mismatch -> `validation.message.custom.confirmationNumberValidation` toast.
4. If valid, `reEvalAppointment(response, appoinementTypeId)` pre-fills all intake fields (patient demographics, injuries, attorneys, employer, custom fields) from the original.
5. PanelNumber is **read-only** for AME / AME-REVAL flows (`isReadOnlyPanelNumber = true`) -- the panel is locked from the original; the user cannot change it.
6. PanelNumber is editable for PQME / PQME-REVAL / OTHER REVAL flows.
7. The user picks a NEW slot (and possibly a NEW location if the doctor's preferences allow it for the appointment type) and submits.

The server then runs the same `Add` flow as Mode 1 but with `IsRevolutionForm = true` -- which in `AppointmentDomain.AddValidation` enforces the original-must-be-Approved check (with IT Admin override).

### Mode 3 -- Re-Request (`IsReRequestForm = true`)

For appointments that were **rejected** by clinic staff. The user opens the rejected appointment, edits the intake form, and resubmits. Server validates the original is `Rejected`, marks it as `Rejected` again (idempotent), and inserts a NEW `Appointment` row with **the same `RequestConfirmationNumber`** as the rejected one. (See `AppointmentDomain.Add` lines 242-253.)

## Role-based field auto-fill (Mode 1 + Mode 2)

The form pre-fills role-specific fields from the logged-in user's profile so they don't re-type their own info:

| Logged-in role | Auto-fill | Read-only? |
|----------------|-----------|------------|
| `Patient` | `patient.email = userEmail` | No (user can override) |
| `PatientAttorney` (Applicant Attorney in NEW) | `appointmentPatientAttorneys[0].attorneyEmail = userEmail` | No |
| `DefenseAttorney` | (No auto-fill in code; UI may auto-add) -- TO VERIFY in Angular HTML | -- |
| `Adjuster` (non-REVAL) | `appointmentClaimExaminer.email = userEmail`, `name = loginUserName` | **Yes** (`isReadonlyAdjuster = true`) |
| `Adjuster` (REVAL) | Cleared on REVAL form switch (`appointmentClaimExaminer = new`) | No (re-fillable) |

The Adjuster pattern reflects OLD's assumption: an Adjuster booking an appointment IS the claim examiner for that case; pre-filling avoids redundancy.

## Location + appointment-type interlocked dropdowns

The booking form's dropdown chain has a hard order:

1. **Appointment Type** -- selected first.
2. **Location** -- filtered by appointment type via `doctorPreferredLocationLookUps.filter(d => d.appointmentTypeId == selectedType OR d.appointmentTypeId == AppointmentTypeEnum.ALL)`.
3. **Slot picker (DoctorsAvailability)** -- only appears after both are picked.

Notable mapping for REVAL flows in the location filter: `AMEREEVAL -> AME`, `PQMEREEVAL -> PQME` for the location lookup. This is because the doctor's preferred-location matrix lists base appointment types only; REVAL inherits the base type's locations.

Location dropdown:

- If the filtered list is empty -> the form shows no slots and `checkForAppointmentTypeSelected = false`. User cannot proceed; must pick a different type or contact staff to add locations.
- After location picked + appointment type set -> `doctorsAvailabilitiesDates()` loads available slots.

## Field-level validation (frontend, via `@rx/annotations`)

OLD uses decorator-based validators on each entity model file. The decorators are processed by `RxValidation.getFormGroup(...)` to build a reactive form.

Decorators in use (per the data-models import):

- `@required()` -- the field must be non-null/non-empty
- `@maxLength(n)` -- string length cap
- `@range(min, max)` -- numeric range
- `@nested(Type)` -- nested array of typed entities
- `@table('TableName')` -- EF table name reference
- `@property('FieldName')` -- column name in DB
- `@alpha()`, `@alphaNumeric()`, `@numeric()`, `@uppercase()`, `@lowercase()`, `@hexColor()` -- format validators
- `@pattern('regex')` -- regex match
- `@minDate(date)`, `@maxDate(date)` -- date bounds
- `@maxNumber(n)` -- numeric max
- `@email()` -- email format
- `@contains(string)` -- substring required
- `@compare(otherProperty)` -- equality with another field

Per-field constraints come from each entity's TypeScript file (e.g., `doctors-availability.ts` shows `@required()` on AppointmentTypeId, AvailableDate, FromTime, ToTime; `@range(0, 2147483647)` on the int FKs). The same pattern applies to `Appointment`, `Patient`, `AppointmentInjuryDetail`, etc.

Strict parity in NEW requires porting every decorator to Angular reactive form `Validators` + ABP DTO `[DataAnnotations]` server-side.

## Cross-field validation (frontend `addAppointment()`)

When the user clicks Submit, this sequence runs:

1. **`checkInjuryDetailFormGroupValidation()`** -- iterates each injury sub-form, runs each one's validators, collects errors.
2. If `appointmentFormGroup.valid == false`:
   - Run `validationPhoneNumber(formGroup)` -- additional phone-format validation across all phone fields (patient phone, attorney phones, claim examiner phone, employer phone).
   - Build error summary via `errorBind(formGroup, errors, "Appointment")` -- traverses the form tree and aggregates field-level errors into `validationError[]`.
3. Else: `validationInjuryDetails(toast, formGroup)` -- additional injury-specific cross-field rules.
4. If `appointmentFormGroupValidationSummaryViewModel.length > 0` -> show validation popup (`AppointmentValidationComponent`) with the full error list. Submission halts.
5. Else: format `DateOfBirth` to `yyyy-MM-dd`, POST to backend.

## Server-side validation (backend `AppointmentDomain.CommonValidation`)

Per `AppointmentDomain.cs` lines 630-730. Runs from both `AddValidation` (booking) and `UpdateValidation` (data-update path).

The function aggregates booleans, then emits validation messages in priority order:

### Rule 1 -- Authorized appointment type for role

```
isAuthorizedUserAppointmentType =
  RoleAppointmentType WHERE RoleId == currentUserRoleId AND AppointmentTypeId == appointment.AppointmentTypeId exists?
```

If no match -> `ValidationFailedCode.AppointmentCanNotBook` ("You are not authorized to book this appointment type"). This implements the role-vs-type permission matrix from the spec doc:

- Patient + Adjuster: PQME, PQME-REVAL only (NOT AME)
- Applicant Attorney + Defense Attorney: ALL types

### Rule 2 -- Injury date >= today rejected

For each `AppointmentInjuryDetail`:

- `DateOfInjury.Date > DateTime.Now.Date` -> `isDateCheck = true`. Final message: `ValidationFailedCode.InjuryDateLessThanToday` ("Injury date must be less than today's date").

### Rule 3 -- Patient DOB <= today

`appointment.Patient.DateOfBirth.Date >= DateTime.Now.Date` -> `ValidationFailedCode.ValidateDateOfBirth` ("Date of Birth must be earlier than today").

### Rule 4 -- Injury date >= patient DOB

For each injury: `Patient.DateOfBirth.Date > injury.DateOfInjury.Date` -> `isInjueryDateSmallThanDOB = true`. Final message: `"Injury  date must be less than to date of birth"` (typo: doubled space; literal text "less than" but logically means injury must be AFTER DOB; preserve text or fix per "OLD bug, fixed for correctness").

### Rule 5 -- Body parts required per injury (currently disabled)

For each injury, if `AppointmentInjuryBodyPartDetails.Count() == 0` -> `isBodyPartExist = true`. **The validation message is commented out in OLD code (lines 696-699):** `"Please enter at least one body part in each claim details"`. Strict parity exception: keep the rule active in NEW since OLD's disabling looks like an unfinished change. Document as "OLD bug (disabled rule), enabled for correctness".

### Rule 6 -- Unique (DateOfInjury, ClaimNumber) per appointment

```
appointment.AppointmentInjuryDetails.Select(x => new { x.DateOfInjury, x.ClaimNumber })
  .ToList().Count != distinct count
```

If duplicate -> `ValidationFailedCode.InjuryDetailExist` ("Injury detail already exists"). The user cannot list the same injury twice.

### Rule 7 -- Unique accessor emails

```
appointment.AppointmentAccessors.Select(x => x.EmailId).ToList()
  .Count != distinct count
```

If duplicate -> `ValidationFailedCode.ExistingAccessorEmail` ("Accessor email already exists in this list"). User cannot share with the same email twice.

### Rule 8 -- All stakeholder emails distinct

Collects: `Patient.Email` (if non-empty) + each `DefenseAttorney.AttorneyEmail` (non-empty) + each `PatientAttorney.AttorneyEmail` (non-empty). Adjuster email is referenced in code but commented out -- so currently the Adjuster's email is NOT in the cross-uniqueness check.

If the collected list has duplicates -> `"Please insert  different email-id "` (typo: doubled space).

This prevents the same person being multiple stakeholders on one appointment.

## Server-side validation (`AppointmentDomain.AddValidation`)

Wraps `CommonValidation` then layers booking-specific gates.

### Slot exists + Available

`DoctorsAvailability.DoctorsAvailabilityId == appointment.DoctorAvailabilityId AND BookingStatusId == Available` -- else `ValidationFailedCode.AppointmentBookingDateNotAvailable`.

### Lead time + per-type max time (external users only)

Internal users skip these gates. External users get:

- `DateTime.Now + AppointmentLeadTime >= AvailableDate` -> reject ("Please book at least N days in advance").
- Per type vs `AppointmentMaxTime{PQME|AME|OTHER}`:
  - PQME / PQME-REVAL: `AvailableDate <= DateTime.Now + AppointmentMaxTimePQME` else "You are not allowed to book appointment PQME"
  - AME / AME-REVAL: same with `AppointmentMaxTimeAME`
  - OTHER: `AppointmentMaxTimeOTHER`

### REVAL gate (`IsRevolutionForm`)

- Check that an existing Appointment row with the same `RequestConfirmationNumber` is `Approved`.
- If not Approved AND user is NOT IT Admin -> reject: `"You can not Re-eval this appointment request because it's not yet approved. Once it gets approved, You will be able to Re-eval this appointment request."`
- If not Approved AND user IS IT Admin -> reject with admin-flavored message: `"You can not Re-eval this appointment request because it's not yet approved. Please approve an appointment and try again."`

### Re-Request gate (`IsReRequestForm`)

- Original appointment must be `Rejected`.
- Else: `"You not allowed to re apply appointment"` (typo preserved).

### Accessor role-conflict check

For each accessor whose email matches an existing user with `StatusId == Active && IsVerified == true`:

- The existing user's `RoleId` must equal the accessor's `RoleId`.
- Mismatch -> `"Your added accessor '{email}' is already registered in our system with different user type. Please select proper Accessor user's type and try again"`.

This prevents inviting an existing Patient as an Adjuster (or vice versa) on a shared appointment.

## Server-side validation (`AppointmentDomain.UpdateValidation`)

Branches on flags:

- `IsStatusUpdate && IsInternalUserUpdateStatus` -> idempotency check (same status to same status rejected for Approved/Rejected/CheckedIn/CheckedOut/Billed).
- `IsDataUpdate && IsInternalUserUpdateStatus` (internal user editing intake fields):
  - `CommonValidation` runs again.
  - If `DoctorAvailabilityId` changed -> verify the new slot is Available. Date+time gates per appointment type apply.
  - If `DoctorAvailabilityId` did NOT change AND appointment is Approved -> reject: `"You can not update, beacause  appointment request is already approved"` (typo preserved). I.e., once approved, you cannot edit other intake fields without ALSO rescheduling.

## Patient deduplication (`AppointmentDomain.IsPatientRegistered`)

Per lines 732-780. The 3-of-6 rule:

```
patientList = vPatientDetail WHERE
  LastName == new.LastName OR
  PhoneNumber == new.PhoneNumber OR
  SocialSecurityNumber == new.SocialSecurityNumber OR
  DateOfBirth == new.DateOfBirth OR
  Email == new.Email OR
  ClaimNumber IN new.appointment.AppointmentInjuryDetails.Select(c => c.ClaimNumber)

for each candidate in patientList:
  counter = 0
  if candidate.LastName == new.LastName -> counter++
  if candidate.SocialSecurityNumber == new.SSN -> counter++
  if candidate.Email == new.Email -> counter++
  if candidate.PhoneNumber == new.Phone -> counter++
  if candidate.DateOfBirth == new.DOB -> counter++
  if any candidate.ClaimNumber matches any new injury claim -> counter++

  if counter >= 3:
    return existing patientId, isPatientRegistered = true
```

Notes:

- The candidate query is an OR-search, so any one match makes a patient a candidate; the per-candidate counter then enforces the 3-of-6 rule.
- The query joins from `vPatientDetail` (a SQL view), which itself joins Patient + AppointmentInjuryDetails to expose ClaimNumber. NEW will use a LINQ query over the Patient + Appointment + AppointmentInjuryDetail entities.
- `FirstName` is intentionally NOT in the dedup -- avoids false-positives on family members with same last name.

## Slot status transitions on Update (`UpdateDoctorAvailbilty`)

When the appointment status changes (internal user editing), the slot transitions per:

| AppointmentStatus | Slot BookingStatus |
|-------------------|--------------------|
| Pending | Reserved |
| Approved | Booked |
| Rejected | Available |
| CancelledNoBill | Available |
| (CancelledLate, RescheduledNoBill, RescheduledLate) | NOT in the switch -- handled by `AppointmentChangeRequestDomain.Update` |
| (default / other) | No change |

Strict parity: replicate this exact mapping in NEW's `Appointment.SetStatus(...)` state machine -- including the implicit "no-op for missing cases" since reschedule/cancel-late paths go through the change-request flow.

## Cleanup of stakeholders on accept-reschedule

When `AppointmentChangeRequestDomain.Update` accepts a reschedule, it inserts a NEW `Appointment` row with the same `RequestConfirmationNumber`. The new row inherits the old's stakeholder records by reference (since FKs point to the same Patient, the same attorney rows, etc.). However, on update of the OLD appointment to Rescheduled status, no stakeholder cleanup occurs -- the old row stays linked to the same children.

Strict parity: when implementing reschedule cascade in NEW, only the parent Appointment row should diverge; child entities (AppointmentDefenseAttorney, AppointmentEmployerDetail, AppointmentInjuryDetail with sub-entities, etc.) are deep-copied so the new appointment has its own children. Otherwise editing the new appointment's children would mutate the old one's history.

## Submission UX (success messages)

After successful POST:

- External user: `"We will send you confirmation for your appointment booking request and once your appointment gets approved, you will receive another email."`
- Internal user: `"Appointment request has been booked and approved successfully."`

After successful update (per lines elsewhere):

- Internal: `"Updated successfully"` (or similar -- TO READ)

If `appointmentId == 0` is returned -> `"Something went wrong"`. This is OLD's catch-all error toast.

After 500ms timeout, redirect via `redirectPage(router)` -- typically to the appointment list or the just-booked appointment's view page.

## Restricting form submissions -- summary

The form gate hierarchy (in priority order; first failure stops submission):

1. **Frontend reactive validators** -- decorator-based per-field rules (required/maxLength/range/email/pattern). User cannot click Submit successfully if any field fails.
2. **Frontend cross-field validators** in `addAppointment()`:
   - phone-number format check
   - per-injury body-part presence (TO VERIFY)
   - validation summary popup if any errors
3. **Backend `CommonValidation`** -- 8 rules (role-vs-type, injury date, DOB, injury vs DOB, body parts, unique injuries, unique accessors, unique stakeholders).
4. **Backend `AddValidation`** -- slot exists Available + lead time + per-type max time + REVAL gate + Re-Request gate + accessor role conflict.
5. **Backend dedup** -- 3-of-6 patient match (does NOT block submission; just sets `IsPatientAlreadyExist = true` and links to existing PatientId).

Submission only succeeds when all gates pass. Internal users skip the lead-time + max-time gates (they can book retroactively or beyond windows).

## Known OLD bugs / typos surfaced

Strict-parity allows fixing these for correctness:

1. `"You can not Re-eval ... not yet approved"` -- typo "Re-eval" inconsistency vs "REVAL" elsewhere; preserve for parity (this is jargon).
2. `"You not allowed to re apply appointment"` -- missing "are"; fix to `"You are not allowed to re-apply for the appointment."`
3. `"Injury  date must be less than to date of birth"` -- doubled space + awkward phrasing. Fix to `"Injury date must be after the date of birth."`
4. `"You can not update, beacause  appointment request is already approved"` -- typos. Fix to `"You cannot update; the appointment request is already approved."`
5. `"Please insert  different email-id "` -- doubled space + awkward. Fix to `"Each stakeholder email must be unique."`
6. Body-part-required rule is commented out. Enable in NEW.
7. Adjuster email is commented out of the stakeholder-uniqueness check. Decide: include for consistency (recommended) or preserve OLD's omission.

Document all fixes as "OLD bug, fixed for correctness" in the corresponding gap-table rows when implementing.

## NEW-implementation checklist (cross-link `external-user-appointment-request.md`)

Implementing this on ABP requires:

- DTO `[Required]` / `[StringLength]` / `[Range]` / `[EmailAddress]` annotations matching OLD's `@rx/annotations` decorators.
- `AppointmentManager.CreateAsync` runs all 8 `CommonValidation` checks plus the booking gates -- as discrete validator methods (one per rule) that emit `BusinessException` with localization keys.
- `IPatientRepository.FindByDeduplicationKeysAsync(dto)` implements the 3-of-6 rule via LINQ.
- `Appointment.SetStatus(target)` enforces the slot-transition map.
- Angular reactive form: `FormBuilder` with custom cross-field validators for the 8 server-side rules, mirrored client-side for fast feedback. (Server is the authoritative gate; client is a UX optimization.)
- The validation popup component (`AppointmentValidationComponent`) -> NEW Angular standalone component showing the error summary list when submission gates fail.
- Role-aware auto-fill in `ngOnInit`: read `currentUser.IsExternalUser` + role, prefill role-matching fields, mark adjuster claim-examiner readonly.
- REVAL flow: a separate route (e.g. `/appointments/reval`) that prompts for confirmation number, calls a `GetForRevalAsync(confirmationNumber, targetType)` AppService that runs the type-family + Approved-status checks and returns the prefilled DTO.
- Re-Request flow: `RequestAgainAsync(rejectedAppointmentId, updatedDto)` AppService.
- All validation messages come from `Domain.Shared/Localization/CaseEvaluation/en.json` -- keys mirror OLD's `ValidationFailedCode` enum where present.
