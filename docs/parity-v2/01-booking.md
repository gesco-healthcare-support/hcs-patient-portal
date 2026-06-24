# 01. Booking workflow -- OLD vs NEW behavioral parity
> OLD = P:\PatientPortalOld (intent/behavior source of truth). NEW = this repo.
> Exhaustive re-read 2026-05-29. We replicate intent + behavior, not code/features.

## Coverage
- OLD reviewed:
  - `PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsController.cs`
  - `PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsSearchController.cs`
  - `PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs`
  - `PatientAppointment.Domain\AppointmentRequestModule\AppointmentInjuryDetailDomain.cs`
  - `PatientAppointment.Domain\AppointmentRequestModule\AppointmentAccessorDomain.cs`
  - `PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs` (GenerateConfirmationNumber)
  - `PatientAppointment.Models\Enums\Roles.cs`, `Enums\AppointmentType.cs`
  - `patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.ts`
  - `patientappointment-portal\src\app\components\appointment-request\appointments\domain\appointment.domain.ts`
- NEW reviewed:
  - `src\...Application\Appointments\AppointmentsAppService.cs` (+ `.Approval.cs` cross-ref)
  - `src\...Application\Appointments\BookingPolicyValidator.cs`, `AppointmentBookingValidators.cs`, `BookingFlowRoles.cs`, `ConfirmationNumberRetryPolicy.cs`
  - `src\...Domain\Appointments\AppointmentManager.cs`, `AppointmentLifecycleValidators.cs` (cross-ref)
  - `src\...Application\AppointmentInjuryDetails\AppointmentInjuryDetailsAppService.cs`
  - `src\...Domain\AppointmentInjuryDetails\AppointmentInjuryDetailManager.cs`
  - `src\...Application\AppointmentAccessors\AppointmentAccessorsAppService.cs`
  - `src\...Application\Patients\PatientsAppService.cs` (GetOrCreate booking flow), `Domain\Patients\PatientMatching.cs`
  - `src\...Application.Contracts\Appointments\AppointmentCreateDto.cs`
  - `angular\src\app\appointments\appointment-add.component.ts`
  - `angular\src\app\appointments\sections\appointment-add-claim-information.component.ts`
  - `angular\src\app\appointments\sections\*` (demographics, schedule, attorney, employer, authorized-users, custom-fields headers reviewed)
- OLD reference docs: none consulted beyond code (Documents_and_Diagrams not needed for this slice).

## Summary
| Class | Count |
|---|---|
| Missing behavior | 4 |
| Partial behavior | 3 |
| Intent deviation | 1 |
| Equivalent (different implementation) | 12 |
| OLD-bug (do not port) | 3 |

## Behavioral gaps (decide)

### G-01-01 -- Injury date validations (date <= today, date >= DOB) not enforced
- **Class:** Missing
- **OLD:** `AppointmentDomain.cs:645-695` (`CommonValidation`) rejects booking when any injury `DateOfInjury.Date > today` (InjuryDateLessThanToday), or when `Patient.DateOfBirth.Date > DateOfInjury.Date` ("Injury date must be less than to date of birth"). Also `AppointmentInjuryDetailDomain.cs:160-168` re-checks both on the standalone add/edit endpoint. Client mirrors it in `appointment.domain.ts:1031-1060` (`checkDateValidationForInjury`).
- **NEW:** *absent* -- `AppointmentInjuryDetailManager.CreateAsync/UpdateAsync` (`AppointmentInjuryDetailManager.cs`) only checks claim/body-part length + the (claim#, DOI) duplicate. The Angular modal `appointment-add-claim-information.component.ts:147` only marks `injuryDateOfInjury` as `Validators.required` -- no max-date, no DOB comparison. No server-side guard anywhere.
- **What it is:** A future-dated injury, or an injury predating the patient's birth, is accepted and persisted in NEW.
- **Why it existed:** Workers'-comp injuries are historical events; a future date or pre-birth date is always data entry error and corrupts the legal intake record + downstream report.
- **What it does / user impact:** Bad injury dates flow into the PDF report and the WCAB record with no warning. OLD blocked the whole booking.
- **Plain-English:** OLD stopped you from typing an injury date in the future or before the patient was born. NEW lets those through.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-01-02 -- Cumulative-trauma injury date-range validation missing
- **Class:** Missing
- **OLD:** `appointment.domain.ts:1031-1052` (`checkDateValidationForInjury`): for a cumulative injury, requires From < To, To <= today, and From != To. Validated before the injury row is added to the form.
- **NEW:** *absent* -- `appointment-add-claim-information.component.ts:146-148` carries `injuryCumulative` + `injuryToDateOfInjury` but applies no validator to `injuryToDateOfInjury` and never compares From/To. The injury manager ignores `ToDateOfInjury` semantics entirely.
- **What it is:** A cumulative injury with To-date before From-date, To-date in the future, or From==To is accepted.
- **Why it existed:** Cumulative trauma is a date range (e.g. repetitive-strain exposure window); an inverted or future range is nonsensical and breaks duration math on the report.
- **What it does / user impact:** Invalid date ranges persist silently.
- **Plain-English:** For a "cumulative" injury OLD made sure the start date came before the end date and the end date was not in the future. NEW does neither.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-01-03 -- Patient-existence flag (IsPatientAlreadyExist) never populated by the booking client
- **Class:** Partial
- **OLD:** `AppointmentDomain.cs:203-218` runs `IsPatientRegistered` (3-of-6 dedup) and stamps `appointment.IsPatientAlreadyExist = true/false` on every booking; the staff queue + emails use it to distinguish a returning patient from a brand-new one.
- **NEW:** Server-side plumbing exists -- `AppointmentsAppService.cs:804` sets `appointment.IsPatientAlreadyExist = input.IsPatientAlreadyExist`, and `PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync` correctly returns `IsExisting` (email fast-path + 3-of-6 dedup, lines 138-322). BUT the Angular `onSubmit` payload (`appointment-add.component.ts:1007-1043`) never reads `patientProfile.patient.isExisting` and never sets `payload.isPatientAlreadyExist`; the DTO defaults to `false` (`AppointmentCreateDto.cs:52`). The DTO doc-comment explicitly says the client "must populate this" -- it does not.
- **What it is:** The dedup result is computed but discarded; every NEW appointment records `IsPatientAlreadyExist = false`.
- **Why it existed:** Staff triage and the returning-patient handling depend on knowing the patient pre-existed.
- **What it does / user impact:** The flag is always false in NEW, so any feature keying off "returning patient" is wrong.
- **Plain-English:** NEW figures out whether the patient already existed, then throws the answer away when saving the appointment. The stored flag is always "new patient."
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-01-04 -- Role-to-AppointmentType authorization is hardcoded to AME-only (OLD was a full configurable matrix)
- **Class:** Partial
- **OLD:** `AppointmentDomain.cs:640-642` checks `RoleAppointmentType` -- a DB join table mapping which role may book which AppointmentType -- for EVERY type, and rejects with AppointmentCanNotBook if the row is absent. The client (`appointment-add.component.ts:682`) also filters the AppointmentType dropdown by the booker's role (`filterLookup(appointmentTypeLookups, [roleId])`). `RoleAppointmentType` is a seeded, admin-meaningful table (`DbEntities\Models\RoleAppointmentType.cs`).
- **NEW:** `AppointmentsAppService.cs:672-686` hardcodes ONE rule: external callers cannot book an AME / AME-REVAL type unless they hold Applicant/Defense Attorney (`BookingFlowRoles.IsAmeAppointmentType` + `IsAttorneyCaller`). No general role-to-type matrix; `GetAppointmentTypeLookupAsync` (`:508-521`) returns all types unfiltered by role.
- **What it is:** NEW enforces only the AME-attorney restriction; OLD enforced an arbitrary configurable role x type permission grid and filtered the picker accordingly.
- **Why it existed:** Different appointment types (PQME, AME, OTHER) have legal rules about who may request them; OLD let the office configure this per role.
- **What it does / user impact:** Any non-AME type is bookable by any role in NEW; the type dropdown is not narrowed by role. If the only rule that ever mattered in production was AME-attorney, NEW matches intent; if other role/type rules were configured, they are lost.
- **Plain-English:** OLD had an editable table of "which user types are allowed to book which exam types." NEW replaced the whole table with a single fixed rule (only attorneys can book AME). Other restrictions, if any existed, are gone.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-01-05 -- Per-appointment stakeholder email uniqueness not enforced
- **Class:** Missing
- **OLD:** `AppointmentDomain.cs:700-728` (`CommonValidation`) collects patient email + every applicant-attorney email + every defense-attorney email into one list and rejects the booking ("Please insert different email-id") if any duplicate exists across those stakeholders.
- **NEW:** *absent* -- `AppointmentsAppService.CreateAppointmentInternalAsync` performs no cross-party email-uniqueness check; the section validators only check format/required per field. (Note the duplicate-accessor-email check IS ported -- see Equivalent.)
- **What it is:** Patient, applicant attorney, and defense attorney can all share the same email on one appointment in NEW.
- **Why it existed:** Distinct parties must have distinct contact emails so notifications reach the right recipient and the legal record is unambiguous.
- **What it does / user impact:** Colliding stakeholder emails are accepted; one party's notifications could land in another's inbox.
- **Plain-English:** OLD refused to let the patient and the attorneys share one email address on the same case. NEW allows it.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-01-06 -- Accessor role-type conflict check (registered user with different role) not enforced
- **Class:** Missing
- **OLD:** `AppointmentDomain.cs:185-195` (Add path) and `AppointmentAccessorDomain.cs:129-140` (`CommonValidation`): when an added accessor email already belongs to an active verified User whose `RoleId` differs from the accessor's chosen role, OLD rejects ("already registered in our system with different user type. Please select proper Accessor user's type"). Auto-create-vs-invite of the accessor account is handled in `AppointmentAccessorDomain.CreateAccountOfAppointmentAccessors` (creates a User if new, emails if existing).
- **NEW:** *absent* -- `AppointmentAccessorsAppService.CreateAsync` (`:92-107`) only guards `IdentityUserId`/`AppointmentId` for Guid.Empty and calls the manager; no role-conflict check against an existing IdentityUser's role.
- **What it is:** NEW does not block adding an accessor whose email maps to an existing user with a different role.
- **Why it existed:** Prevents granting a registered Patient (etc.) accessor rights under the wrong role, which would mis-scope their visibility.
- **What it does / user impact:** Role mismatches at accessor-add time are silently accepted.
- **Plain-English:** OLD warned you if you tried to add someone as (say) an attorney-accessor when they already exist in the system as a patient. NEW does not.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-01-07 -- Reval pre-load does not copy the prior appointment's intake data
- **Class:** Partial
- **OLD:** `appointment-add.component.ts:298-374` + `reEvalAppointment` (`:542-567`) and `appointment.domain.ts:325-417`: on a reval, OLD looks up the source by confirmation number and re-binds the FULL prior appointment -- patient, injuries, body parts, attorneys, employer, custom fields -- into the new form (resetting child PKs to 0) so the user edits a pre-populated copy. Server reuses the source confirmation number for re-request (`AppointmentDomain.cs:262-266`) and validates Approved-source for reval (`:162-175`).
- **NEW:** Server-side reval/re-submit gates ARE ported (`AppointmentManager.LoadRevalSourceAsync` / `LoadResubmitSourceAsync`; `AppointmentsAppService.CreateRevalAsync` / `ReSubmitAsync`, `:587-616`), and the confirmation-number reuse-vs-fresh logic is correct (`AppointmentLifecycleValidators.ResolveConfirmationNumber`). BUT the Angular booking form has no reval/re-submit entry path: `appointment-add.component.ts` reads only `?type=1|2` (initial vs re-evaluation heading flag, `:429-431`) and never calls `CreateRevalAsync`/`ReSubmitAsync` nor pre-loads a source appointment's intake. The reval endpoints are unreachable from the UI, and even if reached they take a fresh `AppointmentCreateDto` (no injury/attorney copy).
- **What it is:** The reval/re-request backend exists but the user-facing pre-populated-copy flow is not wired; the booker would re-enter everything.
- **Why it existed:** Reval/re-request reuses almost all prior data; re-keying it is error-prone and was the whole point of the feature.
- **What it does / user impact:** No usable reval/re-request from the booking UI; data is not carried forward.
- **Plain-English:** OLD let you re-book a prior approved/rejected case with all the details already filled in. NEW has the back-end plumbing but no working screen to do it, and it would not copy the old details forward.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-01-08 -- Self-represented confirmation modal on AA toggle-off (NEW-only deviation)
- **Class:** Intent deviation
- **OLD:** No such gate. Applicant-attorney section is optional; OLD never forced an acknowledgement when it was left empty (`appointment-add.component.ts` / `appointment.domain.ts` have no equivalent).
- **NEW:** `appointment-add.component.ts:488-605` (`confirmAaToggleOff`) pops an ABP confirmation modal ("applicant is self-represented?") when the booker turns the Applicant Attorney section off; declining reverts the toggle back ON. AA section is also enabled-by-default (`:362`).
- **What it is:** NEW adds a mandatory confirmation step OLD did not have, and defaults the AA section ON.
- **Why it existed:** NEW-side UX guard so a booker cannot silently omit the attorney; flagged in-code as a deliberate NEW addition (2026-05-28).
- **What it does / user impact:** Extra click + different default vs OLD; changes the booking interaction. Low risk but is a user-visible behavior difference.
- **Plain-English:** NEW asks "is the applicant representing themselves?" if you skip the attorney section. OLD just let you skip it.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later
- **Resolution:** Resolved by #269 (2026-05-29): AA add modal + DA section toggle shipped.

## Equivalent -- different implementation (no action; checked for coverage)
- **Confirmation number format**: OLD `ApplicationUtility.GenerateConfirmationNumber` = `"A" + id.ToString("D5")` keyed on int PK; NEW `AppointmentsAppService.GenerateNextRequestConfirmationNumberAsync` + `AppointmentBookingValidators.FormatConfirmationNumber` produce `A#####` via MAX(existing)+1 with overflow guard at 99999. Same output format + uniqueness; NEW adds a unique index + retry policy (race fix). Outcome same.
- **Lead-time gate (external only)**: OLD `AppointmentDomain.cs:115-126` skips for InternalUser, rejects slot when `today + AppointmentLeadTime >= AvailableDate`. NEW `BookingPolicyValidator` + `AppointmentBookingValidators.IsSlotWithinLeadTime`. Equivalent. (NEW comment notes internal-user bypass is handled by status path; the lead-time validator itself runs for all, but per-type/lead values mirror OLD.)
- **Per-type max-time gate**: OLD branches PQME/PQMEREEVAL -> MaxTimePQME, AME/AMEREEVAL -> MaxTimeAME, OTHER -> MaxTimeOTHER (`:127-156`). NEW `ResolveMaxTimeDaysForType` substring-routes the same three buckets. Equivalent.
- **Internal-user fast-path (auto-approve + Booked vs Pending + Reserved)**: OLD `AppointmentDomain.cs:221-240`. NEW `BookingFlowRoles.IsInternalUserCaller` -> initialStatus Approved/Pending + slot cascade via `AppointmentStatusChangedEto`. Same outcome; slot is now capacity-driven not single-row-flip (see slot rework). Defer slot mechanics to area 07.
- **3-of-6 patient dedup (LastName/DOB/Phone/Email/SSN/ClaimNumber)**: OLD `IsPatientRegistered` (`:732-780`), threshold 3. NEW `AppointmentBookingValidators.CountMatchingDeduplicationFields` + `PatientsAppService` wiring (email fast-path then 3-of-6). Field set matches except ClaimNumber lives on AppointmentInjuryDetail in NEW so it is passed null (one fewer field available) -- noted; outcome equivalent for the 5 patient-resident fields. (Resulting flag is dropped on save -- that is G-01-03, separate.)
- **Per-injury duplicate (same Claim# + DateOfInjury)**: OLD `AppointmentInjuryDetailDomain.AddValidation:48-52` / `CommonValidation` + client `addInjury` (`:858-880`). NEW `AppointmentInjuryDetailManager.CreateAsync/UpdateAsync` `(AppointmentId, ClaimNumber, DateOfInjury)` duplicate guard + client dedup in modal. Equivalent.
- **Adjuster/Claim-Examiner self-email prefill + readonly**: OLD `appointment-add.component.ts:145-149` + `appointment.domain.ts:1109-1112`. NEW `BookingFlowRoles.ResolveClaimExaminerEmail` (server override) + `claimExaminerPrefillForInjuryModal` + readonly insurance fieldset for CE. Equivalent intent (note: OLD's server-side AdjusterEmail force was commented out; NEW reimplements it server-side defensively -- documented in BookingFlowRoles).
- **AA/DA own-email prefill + readonly for attorney bookers**: OLD `appointment.domain.ts:261-285`. NEW `applyOwnRoleAttorneyPrefill` (`:534-563`). Equivalent.
- **AME/AME-REVAL requires attorney + AA/DA required validators for AME**: OLD client `setCustomValidators` (`appointment.domain.ts:437-470`) makes attorney fields required for AME. NEW `applyAttorneySectionValidators` + server `IsAmeAppointmentType` gate. The required-field side is equivalent; the role-restriction side is the partial G-01-04.
- **Custom fields render-by-type + rebuild on AppointmentType change**: OLD `generateCustomeField`/`getHtmlContent` (`appointment.domain.ts:764-892`) + `clearFormDataAsPerAppointmentType`. NEW `loadCustomFieldsForAppointmentType` + `buildCustomFieldGroup` + `serializeCustomFieldValues` + server `PersistCustomFieldValuesAsync`. Same type-driven validators (alphanumeric/numeric/date/tickbox) + empty-drop semantics. Equivalent.
- **Accessor add at booking (auto-create account vs invite existing)**: OLD `AppointmentAccessorDomain.CreateAccountOfAppointmentAccessors`. NEW persists accessor rows post-create (`createAppointmentAccessorsIfProvided` -> `AppointmentAccessorsAppService.CreateAsync`); account auto-create/invite is handled in the external-signup/identity area. Defer the email + account-provisioning specifics to areas 04/06; the booking-time add of accessor rows is present.
- **Panel-number required for PQME/PQMEREEVAL/OTHER**: OLD client `isPanelNumberValidation` (`appointment-add.component.ts:1017-1027`). NEW carries `panelNumber` with maxLength; per-type required treatment is applied via field-config (`applyFieldConfigsForAppointmentType`) rather than hardcoded. Treat as equivalent intent via the configurable field-config mechanism (verify in area 07 if the seed sets it).
- **Past-date booking rejected**: OLD enforces via lead-time gate (external) only. NEW additionally hard-rejects any past `AppointmentDate` at the domain layer (`AppointmentManager.EnsureAppointmentDateNotInPast`) on Create + date-changing Update. Stricter than OLD but same user intent (no past bookings); NEW-only hardening, not a gap.

## OLD bugs (do not port)
- **Swallowed booking exception returns success-shaped result**: `AppointmentDomain.Add` `catch` (`:295-309`) logs and `return appointment` -- the controller then returns `Ok(result)` even though the booking failed mid-transaction. NEW lets exceptions propagate as proper error responses. Do not port the swallow.
- **Reval "not approved" branch adds a validation message even for IT Admin then still blocks**: `AppointmentDomain.cs:170-173` -- the `isItAdmin` branch sets a message and blocks identically to the non-admin branch (the admin override is hint-only and never actually bypasses). NEW preserves the gate but via distinct error codes; the OLD dead "admin can bypass" expectation is not real. Replicated faithfully (gate blocks both) -- no action, noted so it is not mistaken for a missing admin bypass.
- **Body-part-required validation dead-commented**: OLD `CommonValidation:696-699` ("Please enter at least one body part") is commented out, so OLD never enforced >=1 body part server-side. NEW's modal DOES require >=1 body-part row (`appointment-add-claim-information.component.ts:153-164`). NEW is stricter; OLD's commented-out check is a non-behavior -- do not treat NEW's requirement as a deviation to remove.

## Open questions / could-not-verify
- **G-01-04 scope**: Could not confirm from code alone whether OLD's `RoleAppointmentType` table held rules beyond "AME -> attorneys". Needs the seed data / running OLD app to know if collapsing to the single AME rule loses real configured restrictions. If the only production rule was AME-attorney, NEW matches intent.
- **Panel-number per-type required**: NEW relies on per-AppointmentType field-config seeding to mark `panelNumber` required for PQME/OTHER. Could not verify the seed sets it; if unseeded, this becomes a Missing gap (cross-check with area 07 admin/master-data).
- **Reval/re-submit UI (G-01-07)**: Confirmed no UI call path in `appointment-add.component.ts`. Could not find any other component invoking `CreateRevalAsync`/`ReSubmitAsync`; needs a running-app check or area-02 confirmation that reval is intentionally deferred.
- **Accessor account auto-create/invite emails**: Booking-time accessor row insert is present; the OLD account-creation + invite-email behavior (`CreateAccountOfAppointmentAccessors`) belongs to areas 04 (emails) / 06 (auth) -- not re-verified here.
