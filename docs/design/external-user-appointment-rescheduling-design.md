---
feature: external-user-appointment-rescheduling
date: 2026-05-04
phase: 17-frontend (backend phases 11c, 11j done; supervisor orchestration pending)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointment-change-requests/add/
old-html: appointment-change-request-add.component.html
old-ts: appointment-change-request-add.component.ts
new-feature-path: angular/src/app/appointments/
shell: external-user-authenticated (top-bar only, no side-nav)
screenshots: pending (OLD server on port 4201; capture deferred to batch pass)
---

# Design: External User -- Appointment Rescheduling Modal

## 1. Routes

No dedicated route. The reschedule modal is launched imperatively from the
view-appointment page.

- **Launch context:** `/appointments/view/:id` (appointment must be `Approved`
  and have no pending change request)
- **Backend submit:** `POST api/app/appointment-change-requests/reschedule/{appointmentId}`
- **Backend availability lookup (dates):**
  `GET api/app/doctor-availabilities/lookup?type=1&date=YYYY-MM-DD&locationId=N`
- **Backend availability lookup (time slots for selected date):**
  `GET api/app/doctor-availabilities/lookup?type=0&date=YYYY-MM-DD&locationId=N`
- **Backend JDF download (blank template):** new endpoint TBD (replaces
  `api/DocumentDownload/DownloadFile?filePath=...`)

The OLD component is also reused in a second mode (`applyRescheduleRequest=false`)
to re-upload a rejected Joint Agreement Letter (JAL). That mode is reached from
a "pending reschedule" banner on the view-appointment page. See Section 9 below.

## 2. Shell

External-user authenticated shell: top navigation bar only; no left side-nav.
ABP LeptonX top-bar with brand logo and user menu. No sidebar rendered.

## 3. Screen Layout

The OLD app renders this as an `RxPopup`-injected Bootstrap modal (`modal-dialog modal-lg`).

In the NEW app, implement as an Angular Material `MatDialog` with `maxWidth: '800px'`
(approximate lg equivalent). Two structurally distinct modal bodies share the same
Angular Material dialog host -- use a discriminator `@Input() mode: 'submit' | 'reupload'`.

### 3a. Submit State (`mode = 'submit'`, `applyRescheduleRequest = true`)

```
+------------------------------------------------------------------+
| [H] Re-Schedule Request                    [x close]            |
|     Do you want to Re-Schedule an appointment?                   |
|     Please fill Out the details below.                           |
+------------------------------------------------------------------+
| (radio) Beyond limit?  [Yes] [No]                               |
|                                                                  |
| (shown after radio selection -- isShowSection guard)             |
|   [isBeyodLimit=Yes only]                                        |
|     instruction label + "Click here" download link              |
|                                                                  |
|   Select New Appointment Date                                    |
|   [date picker -- 3 months, available dates highlighted]        |
|                                                                  |
|   [shown after date pick, if slots exist]                        |
|   Select New Appointment Time                                    |
|   [select dropdown]                                              |
|                                                                  |
|   [isBeyodLimit=Yes only]                                        |
|   File Name: [readonly text]                                     |
|                      [Upload Document btn]                       |
|                                                                  |
|   Reason for Re-schedule                                         |
|   [textarea 3 rows]                                              |
+------------------------------------------------------------------+
| [Save btn -- disabled if invalid]  [Close btn]                  |
+------------------------------------------------------------------+
```

### 3b. Re-Upload State (`mode = 'reupload'`, `applyRescheduleRequest = false`)

Rendered when a Beyond-Limit reschedule's JAL document was rejected by supervisor
and the user needs to re-submit a corrected document.

```
+------------------------------------------------------------------+
| [H] Re-Schedule Request                    [x close]            |
+------------------------------------------------------------------+
| Existing Appointment Date & Time: <existing>                    |
| Requested Appointment Date & Time: <requested>                  |
| Reason for Re-schedule:                                         |
| <reason text>                                                    |
|                                                                  |
| Document Name: <name>                                            |
| Document Status: <status>                                        |
| Download Document: [download icon link]                         |
|                                                                  |
| Rejection Reason (By <reviewer>):                               |
| <rejection notes>                                               |
|                                                                  |
| File Name: [readonly text]                                       |
|                      [Upload Document btn]                       |
+------------------------------------------------------------------+
| [Save btn -- disabled until file chosen]  [Close btn]           |
+------------------------------------------------------------------+
```

## 4. Form Fields

### 4a. Submit State Fields

**Field 1: Beyond-Limit Radio (`isBeyodLimit`)**
- OLD: `appointment-change-request-add.component.html:29-42`
- Control name: `isBeyodLimit` (OLD typo -- see Exception 1)
- Type: radio group
- Values: `1` (Yes), `0` (No)
- Label: "Would you like to re-schedule your appointment beyond
  {{appointmentMaxTime}} days of time limit?"
  - `appointmentMaxTime` is a runtime integer derived from system parameters
    keyed to appointment type:
    - PQME / PQME-REVAL: `SystemParameters.AppointmentMaxTimePQME`
    - AME / AME-REVAL: `SystemParameters.AppointmentMaxTimeAME`
    - OTHER: `SystemParameters.AppointmentMaxTimeOTHER`
    - OLD source: `appointment-change-request-add.component.ts:89-97`
- Required: Yes (form remains invalid until one option is selected)
- Initial value: `undefined` (not pre-selected; form invalid until chosen)
- `(change)`: calls `setValidators()` -- resets date/time/reason and toggles
  `appointmentChangeRequestDocuments` FormArray
- Layout: horizontal inline radios with `custom-radio` Bootstrap class
- `isShowSection` guard: the date picker, time select, file upload, and reason
  textarea only render AFTER the user selects Yes or No

**Field 2: Appointment Date (`availableDate`)**
- OLD: `appointment-change-request-add.component.html:57-59`
- Control name: `availableDate`
- Type: date picker
- OLD widget: `<rx-date>` with `[showMonths]="3"`, `[datesAvailable]` whitelist,
  `isDisabledPreviusDate="true"`, `calendarCenter="re-schedule-calendar-center"`
- NEW widget: replace with `mat-datepicker` with a custom date filter function
  that restricts to the server-returned available date set (equivalent to
  `datesAvailable` whitelist); show 1 month view (3-month picker has no direct
  Material equivalent -- single month with prev/next is acceptable parity)
- Label: "Select New Appointment Date"
  - Tooltip (? icon): "Please select 'Re-Schedule' date"
- Required: Yes (`requiredValidator()` set on reschedule branch)
- `(onSelected)` -> `getDoctorsAvailabilities($event)`: fires date-validation
  logic and fetches time slots for the chosen date
- Date validation on selection (external users only; internal users bypass):
  - Lead time floor: selected date must be > today + `SystemParameters.AppointmentLeadTime`
    - Error toast: "You are not allowed to book an appointment with in  {N}  days
      of selected date." (note: double spaces in OLD -- NEW may normalize to single
      space; see Exception 4)
  - Normal upper bound (when `isBeyodLimit=0`): selected date must be <=
    today + `appointmentMaxTime`
    - Error toast: "You are not allowed to book an appointment outside {N} days
      of period from selected date."
  - Beyond-limit lower bound (when `isBeyodLimit=1`): selected date must be >
    today + `appointmentMaxTime` (inverted -- requires beyond-limit date)
    - Error toast: "You are not allowed to book an appointment beyond {N} days
      of period from selected date."
  - All slots booked for valid date: time select hidden, toast:
    "All the appointment slots are booked for this date."
  - OLD source: `appointment-change-request-add.component.ts:202-309`
- Available-date whitelist: loaded on component init by calling the lookup API
  with `type=1` (all available dates for the location)
  - OLD source: `doctorsAvailabilitiesDates()`, ts:190-200

**Field 3: Appointment Time (`doctorAvailabilityId`)**
- OLD: `appointment-change-request-add.component.html:63-71`
- Control name: `doctorAvailabilityId`
- Type: `<select>` (plain HTML select in OLD)
- NEW widget: `ng-select` (consistent with rest of NEW app)
- Label: "Select New Appointment Time"
- Visibility: `*ngIf="showDoctorAvailabity"` -- only shown after a valid date
  is picked AND the server returns at least one slot
- Option binding: `[value]="item.doctorsAvailabilityId"`,
  display: `{{item.appointmenTime}}` (OLD typo -- see Exception 5)
- Default option: `Select` (disabled, selected when no choice made)
- Required: Yes (`requiredValidator()`)
- Slots loaded per date: API call `type=0, date, locationId`
  - OLD source: ts:289-309

**Field 4a: JDF File Name (read-only text)**
- OLD: `appointment-change-request-add.component.html:74-76`
- Control name: none (display only; bound to component `filePath` string)
- Type: `input[type="text"]`, `readonly`
- Label: "File Name"
- Placeholder: "File Path"
- Visibility: `*ngIf="appointmentChangeRequestFormGroup.value.isBeyodLimit"` (Yes only)
- Shows the filename after the user selects a file via the upload input

**Field 4b: JDF Document Upload (file input)**
- OLD: `appointment-change-request-add.component.html:77-80`
- Control name: none (not part of reactive form; handled via `(change)="onFileChange($event)"`)
- Type: `input[type="file"]`, hidden; triggered via `<label for="addDocument">`
  styled as `btn btn-info`
- Button label: "Upload Document"
- Button class: `btn btn-info px-3 bold pull-right`
- Visibility: same as Field 4a (`isBeyodLimit=1` only)
- Accepted types: `.doc,.docx,.pdf` (OLD: `DEFAULT_IMAGE_FILE_EXTENSTION`)
  - OLD source: `default-file-extension.ts:1`
- Max size: 10 MB (see Exception 6)
- `onFileChange` reads file as binary string, base64-encodes it, patches into
  `appointmentChangeRequestDocuments[0].fileData`
- Error toasts:
  - Over size: "You can`t upload file larger than 10 MB." (backtick typo -- see Exception 7)
  - Wrong type: "Please select word document only." (misleading -- see Exception 8)
- OLD source: `appointment-change-request-add.component.ts:336-371`

**Field 5: Reschedule Reason (`reScheduleReason`)**
- OLD: `appointment-change-request-add.component.html:82-87`
- Control name: `reScheduleReason`
- Type: `textarea`, 3 rows
- Label: "Reason for Re-schedule"
- Required: Yes (`requiredValidator()`)
- No `maxlength` in OLD -- apply a reasonable cap (e.g. 500) in NEW

### 4b. Re-Upload State Fields

The re-upload state (`applyRescheduleRequest=false`) has no reactive form inputs
except the new file upload. All other content is read-only display.

| Display label | Source field |
|---|---|
| Existing Appointment Date & Time | `appointmentRescheuldeData.OldAppointmentDateTime` |
| Requested Appointment Date & Time | `appointmentRescheuldeData.NewAppointmentDateTime` |
| Reason for Re-schedule | `appointmentRescheuldeData.ReScheduleReason` |
| Document Name | `appointmentChangeRequestDocumentsLists[0].documentName` |
| Document Status | `appointmentChangeRequestDocumentsLists[0].documentStatusName` |
| Download Document | link to `documentFilePath` (see Exception 9) |
| Rejection Reason | `rejectionNotes`, attributed to `rejectedByUserName` |

File upload fields are identical to Fields 4a/4b above.

OLD source: `appointment-change-request-add.component.html:97-155`

## 5. Tables / Grids

None. Available time slots are a `<select>` dropdown, not a table.

## 6. Modals

This component IS the modal. No nested modals.

The OLD app uses `RxPopup` (in-house injection). In NEW app, use `MatDialog`:

```typescript
// Launcher (view-appointment component):
this.dialog.open(RescheduleRequestModalComponent, {
  maxWidth: '800px',
  data: {
    appointmentId: this.appointment.id,
    appointmentTypeId: this.appointment.appointmentTypeId,
    locationId: this.appointment.locationId,
    mode: 'submit'
  }
});
```

For re-upload mode, pass `mode: 'reupload'` and `changeRequestId`.

## 7. Buttons

| Button | OLD class | Disabled condition | Action |
|---|---|---|---|
| Save (submit) | `btn btn-primary` | `!appointmentChangeRequestFormGroup.valid` | `addAppointmentChangeRequest()` |
| Save (re-upload) | `btn btn-primary` | `!isDocumentUploaded` | `addAppointmentChangeRequest()` |
| Close | `btn btn-secondary` | never | `closePopup()` / `dialogRef.close()` |
| Upload Document | `btn btn-info px-3` | never | triggers hidden file input |
| X (header close) | `.bootbox-close-button.close.text-white` | never | `closePopup()` |
| Click here (JAL download) | `sidenav-link` | never | `downloadJointAgreementLetter()` (see Exception 10) |

OLD source: `appointment-change-request-add.component.html:92-94, 106, 151-153`

## 8. Role Visibility Matrix

| Role | Can open reschedule modal | Notes |
|---|---|---|
| Patient | Yes, if appointment owner | Must be appointment owner (created by this user) |
| Defense Attorney | Yes, if Edit accessor | `AppointmentAccessors` with `Edit` access level |
| Applicant Attorney | Yes, if Edit accessor | Same as Defense Attorney |
| Claims Examiner | Yes, if Edit accessor | Same pattern |
| QME Doctor | No | Doctor is a domain entity, not a portal user |
| Staff / IT Admin / Supervisor | Via internal shell only | Not this external-user flow |

**Gate conditions (both must pass):**
1. Appointment `StatusId == Approved (4)` -- no other status allows reschedule
2. No existing `AppointmentChangeRequest` with `RequestStatusId == Pending` for this appointment
3. User is owner (`CreatedById == currentUserId`) OR has `Edit` accessor role

OLD source: `appointment-status-type.ts`, `AppointmentChangeRequestDomain`,
parity audit `docs/parity/external-user-appointment-rescheduling.md`

Internal supervisor approval flow (approve / reject / modify reschedule request) is
a SEPARATE design doc: `staff-supervisor-change-request-approval-design.md`.

## 9. Branding Tokens

| Element | Token | Value |
|---|---|---|
| Modal header background | `--brand-primary` | `#4e73df` |
| Modal header text | `--brand-primary-text` | `#ffffff` |
| Save button background | `--brand-primary` | via `.btn-primary` override |
| Upload button | static `btn-info` | `#17a2b8` -- no brand token override |
| Close button | static `btn-secondary` | `#6c757d` -- no brand token override |

The header uses `bg-color: var(--brand-primary)` with `color: white` (same as
cancellation request modal -- see `external-user-appointment-cancellation-design.md`).

## 10. NEW Stack Delta

Changes required to replicate OLD behavior on the NEW stack:

1. **Separate components:** Split the OLD shared `AppointmentChangeRequestAddComponent`
   (which branches on `statusId == cancel | reschedule`) into two distinct Angular
   standalone components:
   - `<app-cancel-request-modal>` (already captured in cancellation design doc)
   - `<app-reschedule-request-modal>` (this feature)
   Both share the same `MatDialog` host pattern and re-upload sub-mode logic can
   live inside `app-reschedule-request-modal` behind a `mode` input.

2. **Date picker:** Replace OLD `<rx-date [showMonths]="3">` with Angular Material
   `mat-datepicker`. Pass available dates as a `dateFilter: DateFilterFn<Date>` that
   returns `true` only for server-whitelisted dates. Single-month view is acceptable
   parity -- 3-month view is not a business rule, just a UX convenience in OLD.

3. **Time select:** Replace OLD `<select>` with `ng-select` bound to
   `DoctorAvailabilityLookupDto[]`. Use `bindValue="doctorAvailabilityId"`,
   `bindLabel="appointmentTime"` (corrected spelling -- see Exception 5).

4. **JDF upload:** Keep as `input[type="file"]` with `accept=".doc,.docx,.pdf"`.
   Replace OLD binary-string FileReader with `FormData` + `multipart/form-data`
   POST to `api/app/appointment-change-request-documents/upload`.

5. **JAL blank template download:** OLD calls `downloadJointAgreementLetter(filePath)`
   which pulls from `api/DocumentDownload/DownloadFile`. In NEW, expose a named
   blank-JAL endpoint (e.g. `GET api/app/system-documents/blank-jal`) so the file
   path is not exposed client-side.

6. **Backend entity gap -- `OriginalAppointmentId`:** The `AppointmentChangeRequest`
   table needs an `OriginalAppointmentId` FK to the source appointment so the
   cloner (`AppointmentRescheduleCloner.BuildScalarClone`, Phase 11c) knows which
   appointment to clone. This field is missing from the current NEW entity. Add it
   before implementing the submit path.

7. **Backend Phase 17 orchestration (not yet built):** The reschedule approval flow
   (supervisor approve -> clone appointment -> update slot statuses -> notify parties)
   is pending Phase 17. The frontend modal can be built against a stub that returns
   `202 Accepted` until Phase 17 ships. Do NOT mock the backend state transitions --
   leave them as no-ops and mark with `// PARITY-FLAG` (see Exception 11).

## 11. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Form control name `isBeyodLimit` | Typo in HTML, TS, DB column | DB column stays `IsBeyodLimit` (parity); Angular `formControlName` renamed to `isBeyondLimit` (fix at UI layer only) | DB rename requires migration touching existing data; UI layer is safe to fix without breaking contract |
| 2 | Submit toast text | "Reschedule request submmited" (double-m typo) | "Reschedule request submitted" | Clear typo fix; does not affect any downstream system |
| 3 | JAL download in header calls `downloadJointAgreementLetter()` with no argument | `filePath` undefined -> `encodeURIComponent(undefined)` = "undefined" -> server returns 404 | NEW download link calls the new blank-JAL endpoint directly | OLD is a clear bug; the feature (download blank template) is intentional |
| 4 | Lead time error: double space "with in  {N}  days" | Two extra spaces in toast message string | Normalize to single space | Cosmetic fix; no business logic impact |
| 5 | Time option `item.appointmenTime` | Missing `t` in property name (from OLD DB lookup model) | Use correctly-spelled `appointmentTime` in NEW DTO | Spelling fix at DTO/lookup level; DB column name does not have this typo |
| 6 | File size guard `file.size >= (1000 * 1024)` (~1MB) vs toast "10 MB" | Guard enforces ~1MB but tells user 10MB | NEW enforces 10MB consistently (guard: `file.size > 10 * 1024 * 1024`) | OLD is a clear mismatch bug; 10MB is the stated limit |
| 7 | File size toast "You can`t upload file larger than 10 MB." | Backtick used instead of apostrophe | "You can't upload files larger than 10 MB." | ASCII apostrophe fix; backtick is unreadable |
| 8 | Upload error "Please select word document only." | Misleading -- PDFs are actually accepted | "Please select a .doc, .docx, or .pdf file." | Error message did not match accepted types constant |
| 9 | Download link in re-upload state: `[href]="...documentFilePath"` | Direct S3/storage path exposed in API response | NEW returns a signed URL or uses `/api/app/appointment-change-request-documents/download/{id}` | Security: direct storage URLs must not be exposed to clients |
| 10 | `requestedDoctorAvailabilityId = 0` hardcoded on submit | Clears any requested availability before sending | Investigate in Phase 17 -- this may be intentional (not yet used) or a placeholder; mark `// PARITY-FLAG` until confirmed | Ambiguous; preserve with flag |
| 11 | Phase 17 orchestration pending | Full supervisor approve/clone/notify flow | Frontend can POST the change request; backend returns 202; full state transitions await Phase 17 | Phase dependency -- not a parity deviation |

## 12. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `appointment-change-request-add.component.html` | 9-13 | Reschedule modal header + subtitle |
| `appointment-change-request-add.component.html` | 28-43 | `isBeyodLimit` radio group |
| `appointment-change-request-add.component.html` | 44-88 | `isShowSection` guarded body (date picker, time, JAL, reason) |
| `appointment-change-request-add.component.html` | 45-51 | JAL upload instruction + download link |
| `appointment-change-request-add.component.html` | 54-60 | Date picker field |
| `appointment-change-request-add.component.html` | 63-71 | Time select field |
| `appointment-change-request-add.component.html` | 72-81 | JDF file name + upload button |
| `appointment-change-request-add.component.html` | 82-87 | Reschedule reason textarea |
| `appointment-change-request-add.component.html` | 91-95 | Modal footer (Save / Close) |
| `appointment-change-request-add.component.html` | 97-155 | Re-upload state (review + re-upload) |
| `appointment-change-request-add.component.ts` | 82-130 | `ngOnInit()` -- form setup, system param load, validator assignment |
| `appointment-change-request-add.component.ts` | 154-178 | `addAppointmentChangeRequest()` -- submit + re-upload paths |
| `appointment-change-request-add.component.ts` | 190-200 | `doctorsAvailabilitiesDates()` -- initial available dates load |
| `appointment-change-request-add.component.ts` | 202-309 | `getDoctorsAvailabilities($event)` -- date validation + slot fetch |
| `appointment-change-request-add.component.ts` | 311-334 | `setValidators()` -- radio change handler |
| `appointment-change-request-add.component.ts` | 336-371 | `onFileChange()` -- file upload handling |
| `appointment-change-request-add.component.ts` | 373-389 | `downloadJointAgreementLetter(filePath)` -- JAL download |
| `default-file-extension.ts` | 1 | `".doc,.docx,.pdf"` accepted upload types |
| `docs/parity/external-user-appointment-rescheduling.md` | all | Full parity audit (gap table, role matrix, phase map) |

## 13. Verification Checklist

- [ ] Modal launches from view-appointment page when appointment is `Approved` and
      no pending change request exists
- [ ] Modal does NOT appear when appointment is any status other than `Approved`
- [ ] isBeyodLimit radio defaults to unselected; form Save button is disabled
- [ ] Selecting No (isBeyodLimit=0) reveals date picker, time select, reason
- [ ] Selecting Yes (isBeyodLimit=1) reveals date picker, time select, JAL upload,
      reason; also shows the JAL instruction text with download link
- [ ] Date picker shows only server-whitelisted available dates; past dates disabled
- [ ] Selecting a date within lead time shows error toast (external users only)
- [ ] Selecting a date beyond max time (No branch) shows error toast
- [ ] Selecting a date within max time (Yes branch) shows error toast (inverted)
- [ ] Selecting a valid date with available slots shows time select
- [ ] Selecting a valid date with no slots shows "All slots booked" toast; time
      select remains hidden
- [ ] Time select shows available time slots for chosen date
- [ ] Save button is disabled until all required fields are filled (date, time, reason;
      plus file if isBeyodLimit=1)
- [ ] Submitting (No branch) POSTs change request with correct payload; success toast
      "Reschedule request submitted"; modal closes
- [ ] Submitting (Yes branch) requires a file to be uploaded; file must be doc/docx/pdf
      and <= 10MB
- [ ] Wrong file type shows "Please select a .doc, .docx, or .pdf file." toast
- [ ] File > 10MB shows "You can't upload files larger than 10 MB." toast
- [ ] Re-upload state shows Existing/Requested dates, reason, doc name, status,
      download link, rejection reason
- [ ] Re-upload Save is disabled until a new file is selected (not just form valid)
- [ ] Re-upload submits PATCH with new file data; toast "Joint Agreement Letter Uploaded"
- [ ] Header background uses `--brand-primary` color; text is white
- [ ] `appointmentMaxTime` in label reflects the correct system parameter for the
      appointment type (PQME value shown for PQME appointment, AME for AME, etc.)
- [ ] DB column `IsBeyodLimit` preserved; Angular form control uses `isBeyondLimit`
      (fixed spelling at UI layer)
