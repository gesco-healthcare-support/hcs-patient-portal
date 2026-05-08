---
feature: external-user-appointment-request
date: 2026-05-04
phase: 19a-frontend (backend 11a-11k implemented 2026-05-04; Angular UI exists but needs rework -- see NEW stack delta)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointments/add/
old-html: appointment-add.component.html (852 lines)
old-ts: appointment-add.component.ts (1394+ lines)
new-feature-path: angular/src/app/appointment-add/
shell: external-user-authenticated (top-bar only; no side-nav)
screenshots: pending (partial -- old/patient/02-book-appointment.png, old/admin/02-book-appointment.png)
---

# Design: External User -- Appointment Request (Booking Form)

This is the most complex form in the system. It is a single long-scroll page
(not a wizard with steps) containing 8 collapsible card sections. The user fills
all sections and clicks "Book an appointment" at the top or bottom.

The form adapts per user role: Patients and Adjusters see a subset of sections;
Attorneys see all sections. REVAL flow adds a "Confirmation Number" search to
the Appointment Details section that prefills the form.

---

## 1. Route

| | OLD | NEW |
|---|---|---|
| URL | `/book-appointment` (TO VERIFY from app.lazy.routing.ts) | `/book-appointment` |
| Guard | `PageAccess` (external users only) | `[Authorize]` -- all authenticated roles can access |

External user navigation: "Book an Appointment" button on the home/dashboard page.
Internal users (Clinic Staff, Staff Supervisor, IT Admin) can also reach this form
to book on behalf of a patient (internal-user fast-path: slot=Booked, status=Approved).

---

## 2. Shell

External-user authenticated shell (top-bar only; no side-nav).
The "Back" button (`*ngIf="userTypeId == isExternalUser"`) links to `/home`.
Internal users do not see a Back button.

---

## 3. Page Header

```
New {{appointmentTypeName}} Appointment Request     [Book an appointment][Reset][Back]
```

- `appointmentTypeName` is empty until Appointment Type is selected; then it
  shows the type name (e.g., "New AME Appointment Request").
- Buttons: "Book an appointment" (`btn btn-primary`, `fas fa-plus` icon) + "Reset"
  (`btn btn-secondary`, `ion-md-refresh` icon) + "Back" (external users only)
- Duplicate "Book an appointment" + "Reset" buttons at the bottom of the page.

---

## 4. Section 1: Appointment Details

```
+-----------------------------------------------+
| [H6] Appointment Details                      |
+-----------------------------------------------+
| Appointment Type [select]                     |
| Confirmation Number [text] + [Search] button  |  <- only if isRevolutionForm (REVAL)
| Panel Number [text] or [text, disabled]       |
| Location [select]                             |
| Responsible User [select]                     |  <- only if isResponsibleMemberShow
| Appointment Date [date picker]                |  <- only if checkForAppointmentTypeSelected
| Appointment Time [select]                     |  <- only if showDoctorAvailabity
+-----------------------------------------------+
```

### Field details (Section 1)

**Appointment Type (required):**
- `formControlName="appointmentTypeId"`
- Select from `appointmentTypeLookups` (5 types: PQME=1, AME=2, PQME-REVAL=3, AME-REVAL=4, OTHER=5)
- `(change)="showRevelForm(userTypeId);AutoJump()"`: triggers REVAL state + scrolls to next field

**Confirmation Number (REVAL only):**
- `*ngIf="isRevolutionForm"` (true when PQME-REVAL or AME-REVAL selected)
- `formControlName="requestConfirmationNumber"`, placeholder "Confirmation Number"
- Search button: `[disabled]="!isValidRevelForm()"` -> `getRevelAppointmentForm()` which loads the original appointment and prefills the entire form

**Panel Number:**
- Two variants: `*ngIf="isReadOnlyPanelNumber"` (disabled) / `*ngIf="!isReadOnlyPanelNumber"` (editable)
- `isReadOnlyPanelNumber` is set when loading a REVAL form (panel number carried from original)

**Location (required):**
- Select from `doctorPreferredLocationLookUp` (locations the doctor serves for this appointment type)
- `(change)="getTimeSlotByLocation();AutoJump()"`: fetches available time slots filtered by location

**Responsible User (internal only):**
- `*ngIf="isResponsibleMemberShow"` -- visible to internal users only (Clinic Staff booking on behalf)
- Select from `appointmentLookupGroup.internalUserNameLookUps`

**Appointment Date (required, conditional):**
- `*ngIf="checkForAppointmentTypeSelected"` -- shown after Appointment Type + Location are selected
- Custom date picker (`rx-date`) with 3-month view
- `[datesAvailable]="datesAvailable"` -- available slots highlighted in green
- `isDisabledPreviusDate="true"` -- past dates not selectable
- Tooltip: "Dates highlighted with green color are available dates for booking an Appointment Request"
- `(onSelected)="getDoctorsAvailabilities($event)"` -- fetches time slots for selected date

**Appointment Time (required, conditional):**
- `*ngIf="showDoctorAvailabity"` -- shown after Appointment Date is selected and slots exist
- Select from `doctorsAvailabilitiesLookUps` (time slots for selected date+location)
- Value: `doctorsAvailabilityId`, display: `appointmenTime` (typo in OLD -- keep in proxy binding)
- `(change)="AutoJumpToPatientSection()"` -- scrolls to Patient Demographics section

---

## 5. Section 2: Patient Demographics

```
+-----------------------------------------------+
| [H6] Patient Demographics                     |
+-----------------------------------------------+
| Last Name [text]   First Name [text]          |
| Middle Name [text] Gender [radio]             |
| Date of Birth [date]  Email [email, readonly?]|
| Cell Phone Number [masked 999-999-9999]       |
| Phone Number [masked] + Phone Type [radio]    |
| Social Security # [masked 999-99-9999]        |
| Street [textarea]  Unit # [text]              |
| City [text]        State [select]  Zip [masked]|
| Language [select]  Language Name [text, cond.] |
| "Do you need an interpreter?" [radio Yes/No]  |
| Interpreter Vendor [text, conditional]        |
| Referred By [text]                            |
+-----------------------------------------------+
```

### Field details (Section 2)

All fields are under `formGroupName="patient"`.

**Last Name, First Name, Middle Name (required):**
- `type="text"`, standard form controls

**Gender (required):**
- Radio group from `appointmentLookupGroup.genderLookUps`
- `applicationObjectId` / `applicationObjectName` properties

**Date of Birth (required):**
- `rx-date` picker, placeholder "MM/DD/YYYY"
- `(blur)="checkDateValidation($event)"` + `(onSelected)="checkDateValidation($event)"`
- Validates patient is not in the future; sets age-related fields

**Email (required):**
- `[readonly]="isPatient && userRoleId != roleEnum.ITAdmin"`
- When booked by a Patient, their own email pre-fills and is read-only
- IT Admin can override the email even for Patient bookings

**Cell Phone Number (required):**
- `rx-mask` with mask "999-999-9999", formControlName `cellPhoneNumner` (typo in OLD -- keep for proxy)

**Phone Number + Phone Number Type:**
- `rx-mask` with mask "999-999-9999"
- Radio group from `appointmentLookupGroup.phoneNumberTypeLookUps` (Home, Work, Mobile)
- `(change)="phoneNumberTypeValidation(appointmentFormGroup)"` toggles validation on Phone
- `(onComplete)="selectPhoneNumberType()"` sets type when phone is completed

**Social Security # (optional):**
- `rx-mask` with mask "999-99-9999"

**Street, Unit #, City, State, Zip:**
- Standard address fields. Street is `<textarea>`.
- State is a `<select>` from `appointmentLookupGroup.statesLookUps`
- Zip: `rx-mask` mask "99999"

**Language (required):**
- Select from `appointmentLookupGroup.languageLookUps`
- `(change)="$event.target.value==7 ? otherLanguage(true) : otherLanguage(false)"` -- language ID=7 triggers the other-language name field

**Language Name (conditional):**
- `*ngIf="appointmentFormGroup.value.patient.isOther"` (set when language=7 "Other")
- `formControlName="othersLanguageName"`, placeholder "Language Name"

**Interpreter question (required):**
- Label changes by role: "Do you need an interpreter?" (Patient) vs "Does the patient need an interpreter?" (other roles)
- Radio: Yes / No
- `(change)="setValidator(true/false)"` adds/removes validation on Interpreter Vendor field
- `isInterpreter` property tracks current value

**Interpreter Vendor (conditional):**
- `*ngIf="appointmentFormGroup.value.patient.isInterpreter"`
- `formControlName="interpreterVendorName"`, placeholder "Interpreter Vendor"

**Referred By (optional):**
- `formControlName="referredBy"`, placeholder "Referred By"

---

## 6. Section 3: Employer Details

```
+-----------------------------------------------+
| [H6] Employer Details                         |
+-----------------------------------------------+
| Employer Name [text]  Occupation [text]       |
| Phone Number [masked]                         |
| Street [textarea]  City [text]               |
| State [select]     Zip [masked]               |
+-----------------------------------------------+
```

- Rendered via `*ngFor` over `appointmentFormGroup.controls.appointmentEmployerDetails.controls`
- In OLD there is 1 employer per appointment; `*ngFor` iterates 1 item
- NEW must support 1:N employers (Exception 1)
- Fields: `employerName`, `occupation`, `phoneNumber` (masked), `street` (textarea),
  `city`, `stateId` (select), `zip` (masked 99999)

---

## 7. Section 4: Claim Information

The most complex section. Uses an in-page Bootstrap modal (`#myModal`) rather than
a separate popup dialog. The main page shows a summary table of added injuries;
clicking "Add +" or "Edit Claim" opens the modal overlay.

```
+-----------------------------------------------+
| [H6] Claim Information        [Add + button]  |
|   "Please add injury and body part details"   |  <- warning if isInjuryDetailExist
+-----------------------------------------------+
| [Table of added injuries]                    |
|   Date Of Injury | Claim # | WCAB |           |
|   Insurance Co | Claim Examiner | Action      |
+-----------------------------------------------+
```

The Bootstrap modal (`#myModal`) contains:

```
+-----------------------------------------------+
| [Modal] Claim Information               [X]  |
| --- Claim Info sub-card ---                  |
|   Cumulative Trauma Injury  [Yes/No radio]   |
|   Date Of Injury / From Date [date picker]   |
|   To Date [date picker]  <- if cumulative    |
|   Claim Number [text]                        |
|   WCAB Office (Venue) [select]               |
|   ADJ# [text]                               |
|   Body Parts [textarea]                      |
| --- Insurance sub-card (toggle on/off) ---   |
|   Company Name [text]  Attention [text]      |
|   Phone [masked]       Fax [masked]          |
|   Street [textarea]    STE [text]            |
|   City [text]  State [select]  Zip [masked]  |
| --- Claim Examiner sub-card (toggle on/off) -|
|   Name [text]  Email [text, readonly?]       |
|   Phone [masked]  Fax [masked]               |
|   Street [textarea]  STE [text]              |
|   City [text]  State [select]  Zip [masked]  |
| [Add / Save] [Close]                         |
+-----------------------------------------------+
```

### Field details (Section 4)

**Cumulative Trauma Injury:**
- Radio: Yes (`[value]="true"`) / No (`[value]="false"`, pre-checked)
- `(change)="isCumulativeTraumaInjury(true/false)"` -- toggling shows/hides "To Date" field

**Date Of Injury / From Date:**
- Label changes: "Date Of Injury" (non-cumulative) or "From Date" (cumulative)
- `rx-date`, `formControlName="dateOfInjury"`

**To Date (conditional):**
- `*ngIf="appointmentInjuryDetailFormGroup.value.isCumulativeInjury"`
- `formControlName="toDateOfInjury"`

**Claim Number:**
- `formControlName="claimNumber"`, placeholder "Claim Number"

**WCAB Office (Venue):**
- Select from `appointmentLookupGroup.wcabofficeLookUps`

**ADJ#:**
- `formControlName="wcabAdj"`, placeholder "ADJ#"

**Body Parts:**
- `<textarea>`, `formControlName="bodyParts"`, placeholder "Body Parts"

**Insurance section (togglable):**
- Header: "Insurance" with toggle switcher
- `(change)="addValidationForPrimaryInsurance(...)"` adds/removes required validators
- Disabled when `isAdjusterLogin` (Adjusters cannot edit insurance -- they ARE the examiner)
- Fields: `name` (Company Name), `attention`, `phoneNumber`, `faxNumber`, `street`, `insuranceNumber` (STE), `city`, `stateId`, `zip`

**Claim Examiner section (togglable):**
- Header: "Claim Examiner" with toggle switcher
- `(change)="addValidationForClaimExaminer(...)"`
- Disabled when `isAdjusterLogin`
- **Email field readonly rule:** `[readonly]="isAdjuster && userRoleId != roleEnum.ITAdmin && isReadonlyAdjuster"` -- Adjuster's email prefills the CE email as readonly. Only IT Admin can override.
- Fields: `name`, `email`, `phoneNumber`, `fax`, `street`, `claimExaminerNumber` (STE), `city`, `stateId`, `zip`

**Add/Save button:**
- `*ngIf="!isInjuryUpdate"` shows "Add" button -> `addInjury()` pushes a new row into the FormArray
- `*ngIf="isInjuryUpdate"` shows "Save" button -> `editInjuryDetail()` updates the existing row

**Injury summary table columns:** Date Of Injury, Claim Number, WCAB (office + adj#), Insurance Company, Claim Examiner, Action (Edit Claim / Delete Claim)

---

## 8. Section 5: Additional Authorized User (Accessor)

Visible only when `showFormBaseOnRole` is true (non-Adjuster roles; TO VERIFY exact condition).

```
+-----------------------------------------------+
| [H6] Additional Authorized User  [Add button] |
+-----------------------------------------------+
| "Add email of a user who can manage this      |
|  appointment details on behalf of yourself."  |
|   (shown when no accessors added)             |
| [Table: Name | Email | User Role | Rights | Action] |
+-----------------------------------------------+
```

- "Add" button opens `AppointmentAccessorAddComponent` modal (sub-component)
- Table shows added accessors with edit (pencil) + delete (trash) per row
- `getAccessorRole(roleId)` resolves role ID to name; `getAccessType(accessTypeId)` resolves access type to name

---

## 9. Section 6: Applicant Attorney Details

Visible only when `showFormBaseOnRole` is true.

```
+-----------------------------------------------+
| [H5] Applicant Attorney Details  [toggle]     |
+-----------------------------------------------+
| (fields visible only when toggle is ON)       |
| Name [text]  Email [email, readonly?]         |
| Firm Name [text]  Web Address [text]          |
| Phone [masked]    Fax [masked]                |
| Street [textarea] City [text]                 |
| State [select]    Zip [masked]                |
+-----------------------------------------------+
```

- **Email readonly rule:** `[readonly]="isPatientAttorney && userRoleId != roleEnum.ITAdmin"` -- Applicant Attorney's own email prefills and is readonly
- Toggle `(change)="addValidationForApplicantAttorney(...)"` enables/disables required validators for all fields
- Rendered via `*ngFor` over `appointmentFormGroup.controls.appointmentPatientAttorneys.controls`
- In OLD there is one record per appointment (FormArray of size 1)
- `fieldset [disabled]="isActiveApplicantAttorney"` disables the toggle when the section is disabled

---

## 10. Section 7: Defense Attorney Details

Visible only when `showFormBaseOnRole` is true.

```
+-----------------------------------------------+
| [H5] Defense Attorney Details  [toggle]       |
+-----------------------------------------------+
| (same fields as Applicant Attorney)           |
+-----------------------------------------------+
```

- Same structure as Section 6
- **Email readonly rule:** `[readonly]="isDefenseAttorney && userRoleId != roleEnum.ITAdmin"`
- `(change)="addValidationForDefenceAttorney(...)"`
- `fieldset [disabled]="isActiveDefenceAttorney"`

---

## 11. Section 8: Additional Details (Custom Fields)

Visible only when `isCustomeFileds` is true (set when `SystemParameter.IsCustomField == true`
AND the appointment type has configured custom fields).

```
+-----------------------------------------------+
| [H6] Additional Details                       |
+-----------------------------------------------+
| [Dynamic fields from customFieldsValues]      |
+-----------------------------------------------+
```

- `*ngFor` over `appointmentFormGroup.controls.customFieldsValues.controls`
- Each field rendered based on `filedTypeId` (typo in OLD -- keep in binding):
  - `customeFieldsEnums.Alphanumeric` -> `<input type="text">`
  - `customeFieldsEnums.Numeric` -> `<input type="text">` (no number validation in OLD)
  - `customeFieldsEnums.Date` -> `<rx-date>` date picker
- Label from `customFieldsValuesFormGroup.value.fieldLabel`

---

## 12. Section Visibility Matrix by Role

| Section | Patient | Adjuster | Applicant Atty | Defense Atty | Internal Users |
|---|---|---|---|---|---|
| Appointment Details | Yes | Yes | Yes | Yes | Yes |
| Patient Demographics | Yes | Yes | Yes | Yes | Yes |
| Employer Details | Yes | Yes | Yes | Yes | Yes |
| Claim Information | Yes | Yes | Yes | Yes | Yes |
| Additional Authorized User | Yes | No | Yes | Yes | Yes |
| Applicant Attorney | No | No | Yes | Yes | Yes |
| Defense Attorney | No | No | Yes | Yes | Yes |
| Additional Details | Yes (if enabled) | Yes (if enabled) | Yes (if enabled) | Yes (if enabled) | Yes (if enabled) |
| Responsible User | No | No | No | No | Yes |

`showFormBaseOnRole`: Patient=Yes, Adjuster=No (VERIFY in TS), Applicant Atty=Yes, Defense Atty=Yes.

---

## 13. Auto-Scroll (`AutoJump`) Behavior

The form auto-scrolls to the next unanswered field after certain selections:
- After Appointment Type: scroll to Location
- After Location: scroll to Appointment Date
- After Appointment Date (time selected): scroll to Patient Demographics
- Various State selects also call `AutoJump()`

This is a UX quality-of-life feature in OLD using `document.querySelector(...).scrollIntoView()` or similar. NEW should replicate the behavior or replace with a smooth scroll to the next required empty field.

---

## 14. REVAL Form Pre-Fill

When Appointment Type = PQME-REVAL (3) or AME-REVAL (4):
1. `isRevolutionForm = true` -- shows Confirmation Number field + Search button
2. User enters original appointment's `requestConfirmationNumber`
3. Clicks "Search" -> `getRevelAppointmentForm()` calls backend to load original
4. All patient, injury, attorney, employer fields pre-fill from the original appointment
5. `panelNumber` becomes readonly (`isReadOnlyPanelNumber = true`)
6. Backend validates original appointment is `Approved` (or shows error for non-IT Admin)

---

## 15. Buttons Summary

| Button | Location | Class | Action |
|---|---|---|---|
| Book an appointment | Top + Bottom | `btn btn-primary btn-sm` | `addAppointment()` -- POST to server |
| Reset | Top + Bottom | `btn btn-secondary btn-sm` | `resetFormGroup()` -- clears all fields |
| Back | Top (external only) | `btn btn-primary btn-sm` | `routerLink="/home"` |
| Search | Appointment Details (REVAL) | `btn btn-primary` | `getRevelAppointmentForm()` |
| Add | Claim Information header | `btn btn-primary` | Opens `#myModal` |
| Add / Save | Inside claim modal | `btn btn-primary` | `addInjury()` / `editInjuryDetail()` |
| Close | Inside claim modal | `btn btn-secondary` | `closeModal()` / `data-dismiss="modal"` |
| Edit Claim | Injury table row | `btn btn-primary btn-sm` | `getInjuryDetail(i)` + opens `#myModal` |
| Delete Claim | Injury table row | `btn btn-primary btn-sm` | `deleteInjury(i, injury)` |
| Add | Accessor section | `btn btn-primary` | `addAccessorDetail()` |
| Edit | Accessor table | `.oi.oi-pencil` | `editAccessorDetail(row, i)` |
| Delete | Accessor table | `fas fa-trash-alt` | `deleteAccessorDetail(i)` |

---

## 16. Role Visibility Matrix

| Role | Access |
|---|---|
| Patient | Can book; Patient Demographics email prefilled + readonly |
| Adjuster | Can book; Claim Examiner email prefilled + readonly; Insurance/CE sections disabled |
| Applicant Attorney | Can book; all sections visible; own email readonly in Applicant Attorney section |
| Defense Attorney | Can book; all sections visible; own email readonly in Defense Attorney section |
| Clinic Staff | Can book on behalf; Responsible User field shown; slot=Booked, status=Approved |
| Staff Supervisor | Same as Clinic Staff |
| IT Admin | Full access; all readonly fields become editable |
| Doctor | No access to booking form (Doctor is not a user role) |

---

## 17. Branding Tokens

| Element | Token |
|---|---|
| Page heading H2 | `--text-color-primary` |
| Section card headers | `--brand-primary` background (`socal-card-title` class on "Appointment Details") |
| Available date highlights | `--status-approved` (green) in date picker |
| "Book an appointment" button | `--brand-primary` via `btn-primary` |

---

## 18. NEW Stack Delta

1. **Multi-step POST orchestration:** OLD posts the entire form (appointment + all sub-entities) in one POST. NEW uses multi-step: POST `/api/app/appointments` first, then POST per sub-entity endpoint (injury details, employer details, accessors, attorneys). Angular form already follows this pattern in the existing `appointment-add.component.ts`. No change to the Angular form's call sequence.

2. **Bootstrap modal -> Angular Material dialog:** Replace OLD's in-page Bootstrap `#myModal` for Claim Information with an Angular Material `MatDialog` component. The mat-dialog is the equivalent of OLD's Bootstrap modal. Behavior (open, pre-fill on edit, save/close) is identical.

3. **rx-date -> Angular Material datepicker / mat-datepicker:** OLD uses `rx-control-design` library's `rx-date`. NEW uses standard Angular Material datepicker. The "available dates highlighted green" feature requires a `mat-datepicker` custom date class function that checks `datesAvailable[]`. This must replicate OLD's green-highlight UX exactly.

4. **rx-mask -> ngx-mask or Angular CDK:** Replace `rx-mask` directives with `ngx-mask` (or a similar Angular-native mask library). Masks are: `999-999-9999` (phone), `999-99-9999` (SSN), `99999` (zip).

5. **Lead-time + per-type max-time: server enforces, UI shows error:** The `minimumBookingDays = 3` client-side gate in OLD was removed (Phase 11d). NEW relies on the server throwing `AppointmentBookingDateInsideLeadTime` / `AppointmentBookingDatePastMaxHorizon` errors. Angular form should surface these as field-level error messages on the Appointment Date field.

6. **Slot availability visualization:** The `datesAvailable` array (green dates) comes from `GET /api/app/doctors-availabilities/available-dates?locationId=...&appointmentTypeId=...`. The NEW date picker must call this endpoint when Location + AppointmentType are both selected.

7. **`isCustomeFileds` flag:** Read from `GET /api/app/system-parameters` (`isCustomField` boolean). If true, load `GET /api/app/custom-fields?appointmentTypeId=...` to build the Additional Details section.

8. **`console.log` removed:** Already done in Phase 11d (removed `console.log('Date check:', ...)` from `appointment-add.component.ts`).

9. **AutoJump smooth scroll:** Replace OLD's `AutoJump()` calls (likely using DOM manipulation) with Angular-idiomatic smooth scroll using `ViewChild` + `nativeElement.scrollIntoView({ behavior: 'smooth' })`.

---

## 19. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Single employer detail | OLD HTML iterates `*ngFor` over employers array but in practice only 1 row; `AppointmentDomain.Add` allows 1:N but UI only adds 1 | NEW supports 1:N employer rows (add/remove multiple) | Per gap analysis: the entity schema and backend support multiple; the UI limitation was an oversight. FormArray supports it natively. Confirm with Adrian whether single-employer or multi-employer is the target |
| 2 | `#myModal` in-page Bootstrap modal | Claim Information uses Bootstrap in-page modal overlay | Replace with `MatDialog` or `mat-expansion-panel` + inline form | Framework migration. Behavior identical: open on "Add +", pre-fill on "Edit Claim", save to FormArray |
| 3 | `rx-date` green date highlighting | `datesAvailable` array drives custom date styling in `rx-date` control | Implement as `dateClass` function in Angular Material datepicker | Same UX; different implementation. The "available dates = green" rule is the strictly-parity behavior |
| 4 | `cellPhoneNumner` typo in form control name | `formControlName="cellPhoneNumner"` (missing 'e' in Number) | Use correct name `cellPhoneNumber` in NEW form + DTO | Fix typo at form + API proxy boundary. Backend DTO should use `CellPhoneNumber` |
| 5 | `appointmenTime` typo in lookup field | Time slots display bound to `item.appointmenTime` | Bind to corrected `appointmentTime` from NEW proxy | Proxy generates from C# DTO; C# uses correct spelling |
| 6 | Client-only lead-time validation | OLD Angular had `minimumBookingDays = 3` client-side check | Server enforces; Angular surfaces server error on Appointment Date field | Phase 11b server-side enforcement is the authoritative gate. UI error message: "Appointment date must be at least {N} days from today" |
| 7 | No success page | After `addAppointment()` succeeds, OLD shows a toast with confirmation number and navigates to home | NEW should show the confirmation number prominently: either a modal or a dedicated confirmation screen at `/book-appointment/confirmation` | UX improvement; confirmation number is critical information the user must record |

---

## 20. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `appointments/add/appointment-add.component.html` | 1-96 | Appointment Details section (type, REVAL fields, panel #, location, slot) |
| `appointments/add/appointment-add.component.html` | 98-238 | Patient Demographics section (all patient fields) |
| `appointments/add/appointment-add.component.html` | 240-283 | Employer Details section (FormArray) |
| `appointments/add/appointment-add.component.html` | 285-595 | Claim Information section (inline modal, injury table) |
| `appointments/add/appointment-add.component.html` | 596-648 | Additional Authorized User (accessor section) |
| `appointments/add/appointment-add.component.html` | 650-793 | Applicant Attorney + Defense Attorney sections |
| `appointments/add/appointment-add.component.html` | 797-828 | Additional Details (custom fields section) |
| `appointments/add/appointment-add.component.html` | 835-846 | Bottom "Book an appointment" / "Reset" buttons |
| `docs/parity/external-user-appointment-request.md` | all | Full parity audit (domain behavior, gap analysis, Phase 11a-11k status) |
| `docs/parity/_appointment-form-validation-deep-dive.md` | all | Deep-dive on validation rules, 3-of-6 dedup, booking policy |
| `docs/parity/_slot-generation-deep-dive.md` | all | Doctor availability slot generation logic |

---

## 21. Verification Checklist

- [ ] External user (Patient) navigates to `/book-appointment` and sees the booking form
- [ ] Appointment Type select shows 5 options (PQME, AME, PQME-REVAL, AME-REVAL, OTHER)
- [ ] Selecting PQME-REVAL or AME-REVAL shows the Confirmation Number + Search fields
- [ ] Selecting a type and location loads available dates (highlighted green in date picker)
- [ ] Past dates and dates before the lead-time window are not selectable
- [ ] Selecting a date loads Appointment Time select with available slots
- [ ] Patient email field prefills with logged-in patient's email and is readonly
- [ ] All required fields blank: "Book an appointment" button blocks (form invalid)
- [ ] "Add +" in Claim Information opens the claim modal
- [ ] Adding a claim with cumulative injury=Yes shows From Date + To Date fields
- [ ] Toggling Insurance / Claim Examiner sections on/off changes validation requirements
- [ ] Adjuster: Insurance and CE sections are disabled; CE email prefills with Adjuster's own email (readonly)
- [ ] Applicant Attorney: all sections visible; own email in Applicant Attorney section is readonly
- [ ] Defense Attorney: own email in Defense Attorney section is readonly
- [ ] Adding an accessor with a new email submits successfully
- [ ] Adding an accessor with an existing user email + different role shows role mismatch error
- [ ] Custom fields section appears only when SystemParameters.IsCustomField=true AND type has configured fields
- [ ] "Book an appointment" submits; confirmation number shown to user
- [ ] Slot for selected date/time is marked Reserved (external user)
- [ ] Internal user booking: slot is Booked immediately, appointment status=Approved
- [ ] Selecting REVAL + entering a valid Approved appointment's confirmation # + Search prefills the form
- [ ] Attempting REVAL on a non-Approved appointment shows the error message
- [ ] Re-Request flow: booking with source Rejected appointment keeps same confirmation number
- [ ] Booking outside lead-time window shows server-side error on Appointment Date field
- [ ] Booking outside per-type max-time window shows server-side error
- [ ] Patient with matching dedup (3 of: LastName, DOB, Phone, Email, SSN) is linked to existing patient record
- [ ] Notifications (email) sent to patient + relevant stakeholders after booking
