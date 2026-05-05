# Stage 6 -- Change Requests + Eto Handlers (C1-C5)

Research-only. Drives the build of:

- **C1** -- External cancel modal (view-detail page)
- **C2** -- External reschedule modal (slot picker + reason)
- **C3** -- Supervisor change-request approval UI (list + per-row outcome)
- **C4** -- Wire `AppointmentChangeRequestSubmittedEto` handler
- **C5** -- Wire `AppointmentAccessorInvitedEto` handler

Backend Phases 15, 16, 17 are already shipped (cancel-submit, reschedule-submit, supervisor-approve cascade-clone). C1-C3 are pure frontend. C4/C5 are backend handlers.

---

## 0. Cross-cutting OLD references

### Cancel-time gate (system parameter)

OLD column: `vSystemParameter.AppointmentCancelTime` -- `int` days.
- Citation: `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\vSystemParameter.cs:19-20` and `Models\SystemParameter.cs:19-20`.
- Used at `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs:83`:
  ```
  int systemAppointmentCancelTime = AppointmentRequestUow.Repository<vSystemParameter>().All()
      .Select(x => x.AppointmentCancelTime).FirstOrDefault();
  ```
- Comparison at line 87 (verbatim):
  ```
  bool IsAppointmentCancelTimePassed =
      (appointmentDateTime - DateTime.Today).TotalDays < systemAppointmentCancelTime ? true : false;
  ```
  Strict less-than: a slot exactly N days out is still cancellable. NEW pinned this in `CancellationRequestValidators.IsWithinNoCancelWindow(slotDate, today, cancelTimeDays)`.
- OLD default: not visible in source (sourced at runtime per tenant). Audit doc cites tenant-config; design pins display copy "within {N} days".

### OLD AppointmentStatusType enum (verbatim, `Models\Enums\AppointmentStatusType.cs:9-25`)

| ID | Name | Notes |
|---|---|---|
| 1 | Pending | initial |
| 2 | Approved | the only state cancel/reschedule can act on |
| 3 | Rejected | -- |
| 4 | NoShow | -- |
| 5 | CancelledNoBill | terminal cancel outcome (within window) |
| 6 | CancelledLate | terminal cancel outcome (within billable window) |
| 7 | RescheduledNoBill | terminal source-side state after reschedule approved |
| 8 | RescheduledLate | terminal source-side state after reschedule approved (billed) |
| 9 | CheckedIn | -- |
| 10 | CheckedOut | -- |
| 11 | Billed | -- |
| 12 | RescheduleRequested | interim during reschedule submit |
| 13 | CancellationRequested | enum exists but unused in OLD's Cancel-Add path |

### OLD TemplateCode enum (`Models\Enums\TemplateCode.cs`)

| Code | Name |
|---|---|
| 4 | AppointmentCancelledRequest |
| 5 | AppointmentCancelledRequestApproved |
| 6 | AppointmentCancelledRequestRejected |
| 7 | AppointmentRescheduleRequest |
| 8 | AppointmentRescheduleRequestApproved |
| 9 | AppointmentRescheduleRequestRejected |
| 18 | AppointmentCancelledByAdmin |

### OLD email-template HTML files (`DbEntities\Constants\ApplicationConstants.cs:26-71`)

| Const | File |
|---|---|
| `PatientAppointmentRescheduleReq` | `Patient-Appointment-Reschedule-Request.html` |
| `PatientAppointmentRescheduleReqApproved` | `Patient-Appointment-Reschedule-Request-Approved.html` |
| `PatientAppointmentRescheduleReqRejected` | `Patient-Appointment-Reschedule-Request-Rejected.html` |
| `PatientAppointmentRescheduleReqAdmin` | `Patient-Appointment-Reschedule-Request-Admin.html` |
| `PatientAppointmentCancellationApprvd` | `Patient-Appointment-Cancellation-Apporved.html` (OLD typo preserved) |
| `ClinicalStaffCancellation` | `Clinical-Staff-Cancellation.html` |
| `AccessorAppointmentBooked` | `Accessor-Appointment-Booked.html` |

### NEW NotificationTemplateConsts.Codes (existing today, seeded)

`CancellationRequestSubmitted`, `CancellationRequestAccepted`, `CancellationRequestRejected`, `RescheduleRequested`, `RescheduleApproved`, `RescheduleRejected`, `AccessorInvited` -- all already in
`src\HealthcareSupport.CaseEvaluation.Domain\NotificationTemplates\NotificationTemplateDataSeedContributor.cs:97-103`.

---

## 1. C1 -- External Cancel Modal

### OLD source

- HTML: `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-change-requests\add\appointment-change-request-add.component.html:1-25, 91-94` (cancel branch only -- shared file with reschedule).
- TS (modal launch + add submit): `add\appointment-change-request-add.component.ts`.
- Controller: `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentChangeRequestsController.cs:25-36` (`POST` calls `AddValidation` + `Add`).
- Domain: `AppointmentChangeRequestDomain.cs:65-114` (`AddValidation`), `:197-223` (`Add`).

### OLD UX (cancel branch)

| Element | OLD spec | Citation |
|---|---|---|
| Modal title | "Cancellation Request" | `add.html:5` |
| Subtitle | "Please provide a reason for the cancellation request." | `add.html:7` |
| Body field | textarea, 3 rows, control name `cancellationReason` | `add.html:23` |
| Save button | `btn-primary`, disabled until form valid; binds `addAppointmentChangeRequest()` | `add.html:92` |
| Close | X icon top-right + Close (`btn-secondary`) | `add.html:15, 93` |
| Header bar | brand-primary bg + white text (`text-white`) | `add.html:3-15` |

### OLD validation gates (cancel)

`AppointmentChangeRequestDomain.AddValidation` lines 70-95:
1. `appointment.AppointmentStatusId != Approved` -> "NoChangeAllowedinAppointment".
2. Cancel-time gate: `(slot.AvailableDate - DateTime.Today).TotalDays < SystemParameter.AppointmentCancelTime` -> "CannotCancelOrRescheduleAppointment" (line 87).
3. `cancellationReason` empty -> "ProvideCancelReason" (line 92-95).

### OLD state transitions (cancel)

| Step | Appointment.Status | DoctorsAvailability | ChangeRequest.RequestStatusId |
|---|---|---|---|
| Pre-submit | Approved (2) | Booked | -- |
| Submit (`Add`) | **stays Approved** (cite line 217 sets only `RescheduleRequested`; cancel path leaves Appointment alone -- `CancellationRequested` unused) | Booked | Pending (25) |
| Approve (Update CancelledLate/NoBill) | CancelledLate / CancelledNoBill | **Available** (line 274-276) | Accepted (26) |
| Reject (Update with `CancellationRejectionReason`) | reverts to Approved (line 296) | Booked (unchanged) | Rejected (27) |

### OLD email cascade (cancel)

| Transition | OLD branch | Template (HTML file) | Subject suffix | Recipients |
|---|---|---|---|---|
| Cancel-submit | code path implicit -- OLD does NOT call `SendEmailData` from the `Add` cancel branch (line 215 only fires for reschedule) | `ClinicalStaffCancellation` (per `SendEmailToClinicStaffForCancellation` at line 623, called from elsewhere) | "Appointment request has been cancelled" | clinic supervisors -- TO VERIFY (audit doc flagged) |
| Cancel-approve | `isCancellationRequestApprved=true` (line 282) | `PatientAppointmentCancellationApprvd` | "cancellation request has been accepted" (line 744) | `appointmentStackHoldersEmailPhone.EmailList` (all parties) |
| Cancel-reject | `isCancellationRequestRejected=true` (line 302); but `SendEmailData` cancel-reject branch is commented out at lines 749-755 -- NEW must implement |
| Stakeholder fan-out | TO VERIFY: OLD does not have a separate stakeholder branch for cancel; Phase-18 NEW handler `ChangeRequestApprovedEmailHandler` already does the fan-out (`Application\Notifications\Handlers\ChangeRequestApprovedEmailHandler.cs:108`) |

### NEW current state (backend)

- AppService: `IAppointmentChangeRequestsAppService.RequestCancellationAsync(Guid, RequestCancellationDto)` at
  `src\HealthcareSupport.CaseEvaluation.Application\AppointmentChangeRequests\AppointmentChangeRequestsAppService.cs:51-77`.
- DTO: `RequestCancellationDto.Reason` (Required, StringLength).
- Domain manager: `AppointmentChangeRequestManager.SubmitCancellationAsync` at
  `src\...\Domain\AppointmentChangeRequests\AppointmentChangeRequestManager.cs:99-161`.
  - Validates `Approved` status (`CancellationRequestValidators.CanRequestCancellation`).
  - Cancel-time gate via `IsWithinNoCancelWindow` (strict less-than match -- pinned by unit test).
  - Inserts `AppointmentChangeRequest` with `ChangeRequestType.Cancel`, `RequestStatus = Pending`.
  - Publishes `AppointmentChangeRequestSubmittedEto` (ChangeRequestType=Cancel).
- Per-row edit-access policy via `AppointmentAccessRules.CanEdit` (creator OR Edit-accessor; View-only rejected).
- HTTP: `POST api/app/appointment-change-requests/cancel/{appointmentId}` (controller `AppointmentChangeRequestController.cs`).
- Permission: `[Authorize]` only (not gated on a specific permission key) -- per parity audit.

### Implementation plan (C1)

Frontend (Angular 20):

1. New folder: `angular/src/app/appointments/cancel-request/`
   - `cancel-request-modal.component.ts` (standalone, `MatDialog` host)
   - `cancel-request-modal.component.html`
   - `cancel-request-modal.component.scss`
2. Trigger: launched from `appointments/appointment/appointment-view-detail.component` "Cancel Appointment" button (only visible when `appointment.appointmentStatus === Approved` AND `canEdit`).
3. Bind to auto-generated proxy `AppointmentChangeRequestService.requestCancellationByAppointmentIdAndInput()` after `abp generate-proxy` regenerates `angular/src/app/proxy/appointment-change-requests/`.
4. Form: single `cancellationReason` `FormControl<string>` with `Validators.required` + `Validators.maxLength(2000)`.
5. Error popups: shared `<app-validation-popup>` rendering localized `ChangeRequestCancelTimeWindow` / `ChangeRequestAppointmentNotApproved` / `ChangeRequestEditAccessRequired` / `Validation:CancellationReasonRequired` from BusinessException error codes.
6. Success: toast "Your cancellation request has been submitted." + close modal + emit refresh on parent.

Tests:
- Component test (Karma/Jasmine): valid form enables Save; empty form disables; clicking Save invokes service with `{ reason }`.
- Backend already covered by Phase 15 unit tests in `AppointmentChangeRequestManagerTests` -- no new backend tests for C1.

### Acceptance criteria (C1)

- [ ] Owner clicks Cancel on Approved appointment -- modal opens with header "Cancellation Request" + subtitle.
- [ ] Reason textarea required; Save disabled until non-empty trimmed value.
- [ ] Save POSTs to NEW endpoint; on 200 toast renders + modal closes + parent refreshes (banner shows "Pending Cancellation").
- [ ] BusinessException(`ChangeRequestCancelTimeWindow`) renders popup with `cancelTimeDays` interpolated.
- [ ] View-only accessor cannot see the Cancel button (gated by `canEdit` from Phase 13a).
- [ ] Edit-accessor can submit successfully.
- [ ] Tenant-theme swap -- modal header bar + Save reflect new brand-primary.

### Gotchas (C1)

1. The OLD modal is shared with reschedule (one component, two `*ngIf` branches). Design.md recommends splitting into two NEW components -- DO NOT replicate the shared modal.
2. OLD's `addAppointmentChangeRequest()` posts the appointmentStatus enum on the body (CancelledNoBill/CancelledLate). NEW does NOT -- the supervisor picks the outcome at approval time. The submit DTO is just `{ reason }`.
3. OLD typo preserved in design ADR: `IsBeyodLimit` is fixed to `IsBeyondLimit` in NEW (Exception 3 in design doc).
4. Cancel-time error message must show the resolved day count -- thread `cancelTimeDays` from `BusinessException.Data["cancelTimeDays"]` into the popup.
5. The OLD Add path does NOT update `Appointment.AppointmentStatus` on cancel-submit (parent stays Approved; `CancellationRequested = 13` enum is unused). NEW preserves this; the design doc Section 9 calls this out as an Exception 1 (parity-verified).

---

## 2. C2 -- External Reschedule Modal

### OLD source

- HTML: `add\appointment-change-request-add.component.html:1-95` (reschedule branch is `*ngIf="statusId == appointmentStatusReschedule"`, lines 9-89; submit-state at lines 26-89; reupload-state lines 97-155).
- TS: same `add\appointment-change-request-add.component.ts`.
- Domain: `AppointmentChangeRequestDomain.cs:96-194` (RescheduleRequested validation), `:197-223` (Add path lines 199-216 covers reschedule transition).
- Slot lookup: OLD frontend service `appointment-change-requests.service.ts` (calls `getDoctorsAvailabilities(date)` -- per same doctor's availability list at the SAME location and AppointmentType).

### OLD UX (reschedule submit branch)

| Element | OLD spec | Citation |
|---|---|---|
| Modal title | "Re-Schedule Request" | `add.html:10` |
| Subtitle | "Do you want to Re-Schedule an appointment? Please fill Out the details below." | `add.html:13` |
| Field 1 -- Beyond-limit radio | `isBeyodLimit` (OLD typo) Yes=1 / No=0 | `add.html:32-41` |
| Field 2 -- Date picker | `<rx-date>` 3 months, available dates whitelisted, no past dates; control `availableDate` | `add.html:57-59` |
| Field 3 -- Time select | shown only after date picked + slots exist; control `doctorAvailabilityId` | `add.html:63-71` |
| Field 4 -- JDF upload | only when `isBeyodLimit=Yes`; click-link downloads blank JAL template | `add.html:44-81` |
| Field 5 -- Reason | textarea 3 rows; control `reScheduleReason` | `add.html:83-87` |
| Save | `btn-primary`, disabled until valid | `add.html:92` |

### OLD validation gates (reschedule submit)

`AppointmentChangeRequestDomain.AddValidation` (lines 96-193):
1. `appointment.AppointmentStatusId == Approved` (gate same as cancel).
2. `string.IsNullOrEmpty(reScheduleReason)` -> "ProvideRescheduleReason" (line 99-101).
3. `appointmentChangeRequest.DoctorAvailabilityId == 0` -> "ProvideNewAppointmentDateTime" (line 104-107).
4. `vDoctorsAvailability.BookingStatusId != Available` -> "AppointmentBookingDateNotAvailable" (line 108-111).
5. **Lead-time gate** (`!IsBeyodLimit`): `DateTime.Now.AddDays(systemParameters.AppointmentLeadTime) >= AvailableDate` -> "You are not allowed to book within {leadTime} days" (line 125-129).
6. **Per-AppointmentType max-time gate**:
   - PQME / PQMEREEVAL: `AvailableDate > DateTime.Now.AddDays(AppointmentMaxTimePQME)` (line 131-141).
   - AME / AMEREEVAL: `AppointmentMaxTimeAME` (line 142-151).
   - OTHER: `AppointmentMaxTimeOTHER` (line 152-161).
7. If `IsBeyodLimit=true`: `AvailableDate < AppointmentMaxTimePQME-days-from-today` -> "beyond {N} days" (line 165-191).

### OLD state transitions (reschedule)

| Step | Appointment.Status | Old Slot | New Slot | ChangeRequest |
|---|---|---|---|---|
| Pre-submit | Approved | Booked | Available | -- |
| Submit (`Add` reschedule, line 202-216) | RescheduleRequested (12) -- line 217 sets via `appointmentChangeRequest.AppointmentStatusId` mapped onto appointment | Booked | **Reserved** (line 206) | Pending |
| Approve (Update RescheduledLate/NoBill, line 309-587) | source -> RescheduledLate / RescheduledNoBill (line 313); NEW appointment row inserted at Approved (line 322-348) | -> **Available** (line 552-556) | -> **Booked** (line 557-561) | Accepted |
| Approve with admin-override (line 562-580) | source -> RescheduledLate / RescheduledNoBill | -> Available | admin-picked slot -> Booked; user's `RequestedDoctorAvailabilityId` -> Available (line 564-567) | Accepted, `RequestedDoctorAvailabilityId` retained for audit |
| Reject (Update with `ReScheduleRejectionReason`, line 591-617) | reverts to Approved (line 606) | Booked (line 595) | -> Available (line 600-602) | Rejected |

### OLD email cascade (reschedule)

| Transition | Branch flag | Template HTML | Subject | Recipients |
|---|---|---|---|---|
| Reschedule-submit | `rescheduleRequested=true` (Add, line 215 -> SendEmailData line 756-768) | `PatientAppointmentRescheduleReq` | "Your have successfully requested for reschedule." | `appointmentStackHoldersEmailPhone.EmailList` |
| Reschedule-approve (no override) | `isRescheduleRequestApproved=true` (line 583, line 719-731) | `PatientAppointmentRescheduleReqApproved` | "Your reschedule request has been approved" | stackholders email list |
| Reschedule-approve (admin override) | `isAdminReschedule=true` (line 576, line 705-718) | `PatientAppointmentRescheduleReqAdmin` | "Reschedule request has been changed by our team." | stackholders email list |
| Reschedule-reject | `isRescheduleRequestRejected=true` (line 612, line 732-741) | `PatientAppointmentRescheduleReqRejected` | "Your reschedule request has been rejected" | stackholders email list |

### Cascade-clone on reschedule approval (OLD)

OLD `Update` reschedule-approve creates a NEW `Appointment` row (lines 322-348) and clones these child groups:

1. **AppointmentPatientAttorneys** (line 350-374) -- one row only (`.FirstOrDefault()`).
2. **AppointmentDefenseAttorneys** (line 376-399) -- one row only.
3. **AppointmentAccessors** (line 401-413) -- ALL rows.
4. **AppointmentEmployerDetails** (line 414-434) -- ALL rows.
5. **CustomFieldsValues** (line 435-450) -- ALL rows.
6. **AppointmentInjuryDetails** (line 451-467) including:
   - **AppointmentClaimExaminers** (line 468-490)
   - **AppointmentPrimaryInsurance** (line 491-511)
   - **AppointmentInjuryBodyPartDetails** (line 512-521)
7. **AppointmentNewDocument** (line 523-549).

NEW Phase 17 (`AppointmentChangeRequestsAppService.Approval.cs:CloneChildEntitiesAsync` lines 470-552) clones:
- AppointmentInjuryDetail + body parts + claim examiners + primary insurances (correctly cascading 4 layers)
- AppointmentEmployerDetail
- AppointmentApplicantAttorney link rows
- AppointmentDefenseAttorney link rows
- AppointmentAccessor

**Gap:** NEW does NOT clone `CustomFieldsValues` or `AppointmentNewDocument` (or its NEW equivalent `AppointmentDocument`). Flag for parity verification: this may be Phase-17-Session-A-incomplete or intentionally deferred. The CLAUDE.md notes Phase 17 is "complete" but the code shows 5 child groups vs. OLD's 7+. **Recommend a parity flag in `docs/parity/_parity-flags.md`**: "Reschedule-approve cascade-clone missing CustomFieldsValues + AppointmentDocuments per OLD lines 435-450, 523-549".

### NEW current state (backend)

- AppService: `IAppointmentChangeRequestsAppService.RequestRescheduleAsync(Guid, RequestRescheduleDto)` at
  `AppointmentChangeRequestsAppService.cs:79-127`.
- DTO: `RequestRescheduleDto { Guid NewDoctorAvailabilityId, string ReScheduleReason, bool IsBeyondLimit }`.
- Manager: `AppointmentChangeRequestManager.SubmitRescheduleAsync` at
  `Manager.cs:188-277`. Inserts ChangeRequest, transitions new slot to Reserved, transitions parent to RescheduleRequested via `_appointmentManager.RequestRescheduleAsync`, publishes `AppointmentChangeRequestSubmittedEto` (ChangeRequestType=Reschedule).
- Lead-time + per-AppointmentType max-time gates run upstream in AppService via `BookingPolicyValidator.ValidateAsync(slot.AvailableDate, appointment.AppointmentTypeId)` (line 117).
- Slot-availability gate: `RescheduleRequestValidators.IsSlotAvailable(BookingStatusId)` (Manager line 233).
- HTTP: `POST api/app/appointment-change-requests/reschedule/{appointmentId}`.
- Slot lookup endpoints (existing): `GET api/app/doctor-availabilities/lookup?type=1&date=...&locationId=...` (dates) and `type=0` (time slots).

### Implementation plan (C2)

Frontend (Angular 20):

1. New folder: `angular/src/app/appointments/reschedule-request/`
   - `reschedule-request-modal.component.ts` (standalone, MatDialog host, `mode = 'submit' | 'reupload'`).
   - HTML/SCSS partner files.
2. Trigger: Reschedule button on view-detail page (same gating as Cancel).
3. Form (`mode='submit'`):
   - `isBeyondLimit: FormControl<boolean>` (radio Yes=true / No=false).
   - `availableDate: FormControl<Date>` (Material datepicker; load valid dates via `AppointmentChangeRequestService` -- TBD endpoint or reuse `DoctorAvailabilityService`).
   - `doctorAvailabilityId: FormControl<string>` (mat-select bound to slot lookup result for the picked date).
   - `reScheduleReason: FormControl<string>` (required, max 2000).
   - JDF upload + JDF blank-template download -- defer to a later phase (Section 10 Item 5 of design doc); NEW MVP can ship without `IsBeyondLimit=true` JDF flow if the backend rejects it cleanly. Confirm with design doc.
4. The two date-picker / slot-picker calls reuse Phase-11 booking flow's services. SAME `Doctor + Location + AppointmentType` as the existing appointment.
5. Save: POST to reschedule endpoint with `{ newDoctorAvailabilityId, reScheduleReason, isBeyondLimit }`.
6. Reupload mode (`applyRescheduleRequest=false` in OLD): rejected JAL re-upload path -- defer; not in C2 scope.
7. Error popups: `ChangeRequestNewSlotNotAvailable`, `ChangeRequestRescheduleReasonRequired`, `BookingPolicyLeadTimeViolation`, `BookingPolicyMaxHorizonViolation`, `ChangeRequestEditAccessRequired`.

Tests:
- Component test: form invalid until all required fields filled; date pick triggers slot-fetch; valid form enables Save.
- Service binding test: submit body matches DTO shape.
- Backend Phase 16 unit tests already cover manager paths; no new backend tests.

### Acceptance criteria (C2)

- [ ] Reschedule button visible on Approved appointment for owner/Edit-accessor.
- [ ] Modal opens, Yes/No radio for IsBeyondLimit.
- [ ] After radio choice, calendar appears -- only valid dates pickable.
- [ ] After date pick, time-slot select populates from slot lookup.
- [ ] Reason required; Save disabled until all required filled.
- [ ] On Save: source slot stays Booked; new slot transitions to Reserved; parent appointment status becomes RescheduleRequested; ChangeRequest is Pending.
- [ ] Lead-time / max-horizon violations render BookingPolicy* popup.
- [ ] Slot-not-available (lost a race) renders ChangeRequestNewSlotNotAvailable popup.

### Gotchas (C2)

1. NEW transitions parent appointment to `RescheduleRequested` (Manager line 264) -- OLD does the same via `appointment.AppointmentStatusId = appointmentChangeRequest.AppointmentStatusId` at line 217 with `RescheduleRequested=12` set on line 204. PARITY-MATCH.
2. `IsBeyondLimit=true` opens a separate downstream JAL document workflow (per design doc 9b). C2 SCOPE EXCLUDES the JAL upload step. Adrian must approve before relaxing the IsBeyondLimit=true path.
3. NEW slot-picker MUST filter by same `Doctor + Location` (OLD behavior). The existing `DoctorAvailabilityService.lookup(type, date, locationId)` already filters; pass the appointment's existing `LocationId`. Doctor filter: TBD -- OLD's `vDoctorsAvailability` filters by `DoctorsAvailabilityId` on a single doctor; NEW must replicate via `doctorId` query param (verify parity).
4. New slot must be `Available` (BookingStatus). The booking-status check is in the Manager; popup must thread the rejected status code.
5. `ReScheduleReason` is required even though NEW `RequestRescheduleDto` may not have a `[Required]` attribute -- the Manager throws `ChangeRequestRescheduleReasonRequired` (line 211-212).

---

## 3. C3 -- Supervisor Change-Request Approval UI

### OLD source

- Reschedule list: `patientappointment-portal\src\app\components\appointment-request\appointment-change-requests\list\appointment-change-request-list.component.html:1-159` and `.ts`.
- Cancel list: `detail\appointment-change-request-detail.component.html:1-96` and `.ts`.
- Approve/Reject modal (shared): `edit\appointment-change-request-edit.component.html:1-151` and `.ts:110-199`.
- Domain (approve/reject orchestration): `AppointmentChangeRequestDomain.cs:231-622` (`Update` method).

### OLD UX (supervisor approval)

Two pages, same shell (internal-user authenticated):

| Page | URL | Component |
|---|---|---|
| Reschedule Requests | `/appointment-rescheduled-requests` | `AppointmentChangeRequestListComponent` |
| Cancel Requests | `/appointment-cancel-requests` | `AppointmentChangeRequestDetailComponent` |

Reschedule list columns (`list.html:71-150`): Patient, Gender, Type, RequestOn, Confirmation #, OldAppointmentDateTime, NewAppointmentDateTime, Reason (ellipsis), RequestStatus, RequestedBy, IsBeyondLimit (color-coded "Yes"=green / "No"=red), DOB, SSN, ClaimNumberList (multi-line), DOIList (multi-line), Action (approve+reject icons).

Cancel list columns (`detail.html:34-84`): Patient, Gender, Type, DOB, SSN, ClaimNumber, DOI, RequestOn, Confirmation #, OldAppointmentDateTime, CancellationReason (ellipsis), RequestStatus, RequestedBy, Action (**approve icon ONLY**; reject icon is commented out at line 81 of detail.html).

Approve/Reject modal (4 distinct bodies):

| op | Body fields | Submit payload |
|---|---|---|
| Cancel-Approve (`operationType=1`) | Read-only `OldAppointmentDateTime`, `cancellationReason`. Required radio `appointmentStatusId` ∈ {`CancelledNoBill`, `CancelledLate`}. | `{ requestStatusId: Accepted, appointmentStatusId, appointmentId }` |
| Cancel-Reject (`operationType=2`) | Read-only fields. Required textarea `cancellationRejectionReason`. | `{ requestStatusId: Rejected, appointmentStatusId: Approved, cancellationRejectionReason, appointmentId }` |
| Reschedule-Approve (`operationType=1`) | Read-only existing/requested datetimes + reason. Required radio `appointmentStatusId` ∈ {`RescheduledNoBill`, `RescheduledLate`}. Optional checkbox `isChangeInReschedule` -- when checked reveals required date picker + time select + textarea `adminReScheduleReason` (admin-override-with-reason). | Two payload variants per design doc 5f. |
| Reschedule-Reject (`operationType=2`) | Required textarea `reScheduleRejectionReason`. | `{ requestStatusId: Rejected, appointmentStatusId: Approved, reScheduleRejectionReason, appointmentId, doctorAvailabilityId, oldDoctorAvailabilityId }` |

### OLD admin-override semantics

The "admin-override-with-reason" flag (`isChangeInReschedule` checkbox + `adminReScheduleReason` textarea) lets the supervisor override the user's chosen new slot. It does NOT override the cancel-time gate (the cancel-time gate fires ONLY at submit; supervisors can approve a cancel that is past the gate because the request was made before).

OLD Update lines 562-580 detect override via `appointmentChangeRequest.RequestedDoctorAvailabilityId > 0` and free that user-picked slot back to Available (line 564-567) while booking the supervisor-picked slot.

### OLD state transitions (supervisor)

Already enumerated in C1/C2 sections above (collapsed there for clarity). The `Update` method's branching:

- `RequestStatusId == Accepted` AND `AppointmentStatusId in (CancelledLate, CancelledNoBill)` -> cancel-approve (line 263-292).
- `CancellationRejectionReason != null` -> cancel-reject (line 294-306).
- `AppointmentStatusId in (RescheduledLate, RescheduledNoBill)` AND `RequestStatusId == Accepted` -> reschedule-approve + cascade-clone (line 309-588).
- `ReScheduleRejectionReason != null` -> reschedule-reject (line 591-617).

### NEW current state (backend Phase 17)

`AppointmentChangeRequestsApprovalAppService` (`Application\AppointmentChangeRequests\AppointmentChangeRequestsAppService.Approval.cs`) -- 5 endpoints:

| Method | Permission | Citation |
|---|---|---|
| `ApproveCancellationAsync(changeRequestId, ApproveCancellationInput)` | `AppointmentChangeRequests.Approve` | line 99-165 |
| `RejectCancellationAsync(changeRequestId, RejectChangeRequestInput)` | `AppointmentChangeRequests.Reject` | line 167-207 |
| `ApproveRescheduleAsync(changeRequestId, ApproveRescheduleInput)` | `AppointmentChangeRequests.Approve` | line 209-328 |
| `RejectRescheduleAsync(changeRequestId, RejectChangeRequestInput)` | `AppointmentChangeRequests.Reject` | line 330-393 |
| `GetPendingChangeRequestsAsync(GetChangeRequestsInput)` | `AppointmentChangeRequests.Default` | line 395-420 |

DTOs: `ApproveCancellationInput { CancellationOutcome, ConcurrencyStamp }`, `ApproveRescheduleInput { RescheduleOutcome, OverrideSlotId?, AdminReScheduleReason?, ConcurrencyStamp }`, `RejectChangeRequestInput { Reason, ConcurrencyStamp }`, `GetChangeRequestsInput { RequestStatus?, ChangeRequestType?, CreatedFromUtc?, CreatedToUtc?, SkipCount, MaxResultCount, Sorting }`.

Optimistic-concurrency: `LoadAndStampStampAsync` enforces `ConcurrencyStamp` match before accept/reject (line 422-440).

Cascade-clone: `CloneChildEntitiesAsync` covers 5 child groups (gap noted above at "Cascade-clone").

Slot transitions: admin-override case releases the user's Reserved slot (line 303-306 + `ReleaseSlotIfReservedAsync` line 447-459) and the new admin-picked slot transitions via `AppointmentStatusChangedEto` cascade.

Events published:
- `AppointmentChangeRequestApprovedEto` -- `ChangeRequestApprovedEmailHandler` (existing) maps to `CancellationRequestAccepted` / `RescheduleApproved` template codes.
- `AppointmentChangeRequestRejectedEto` -- `ChangeRequestRejectedEmailHandler` (existing) maps to `CancellationRequestRejected` / `RescheduleRejected`.
- `AppointmentStatusChangedEto` -- drives the slot cascade via `SlotCascadeHandler`.

### Implementation plan (C3)

Frontend (Angular 20, internal shell):

1. New folder: `angular/src/app/appointments/change-requests/`
   - `cancel-list.component.{ts,html,scss}` -- routed at `/appointments/change-requests/cancel`.
   - `reschedule-list.component.{ts,html,scss}` -- routed at `/appointments/change-requests/reschedule`.
   - `change-request-edit-modal.component.{ts,html,scss}` -- shared MatDialog for the 4 operations.
   - `change-request.routes.ts` -- 2 routes guarded by `StaffSupervisor` OR `ITAdmin` role.
2. Side-nav entry "Change Requests" with two sub-items: "Reschedule Requests" and "Cancel Requests".
3. List page implementation:
   - Server-side paged + sortable table (`mat-table` + `mat-paginator` + `mat-sort`).
   - "Toggle: Cancellation/Reschedule Requests till Date" boolean filter.
   - Search box (Enter triggers; clear icon).
   - Bind to `AppointmentChangeRequestApprovalService.getPendingChangeRequests()` (auto-generated proxy after `abp generate-proxy`).
   - Action column visibility: per design doc 4c, `showButtonBaseOnRole` = caller is StaffSupervisor / ITAdmin AND `event.RequestStatusId == Pending` AND for reschedule: (`isBeyondLimit=false` OR `documentStatus == Accepted`).
4. Approve/Reject modal -- 4 distinct content blocks:
   - **Cancel-Approve:** read-only fields + required radio `cancellationOutcome` ∈ `{CancelledNoBill, CancelledLate}` + Approve button. Posts to `ApproveCancellationAsync`.
   - **Cancel-Reject:** required textarea `reason` + Reject button (danger style). Posts to `RejectCancellationAsync`.
   - **Reschedule-Approve:** read-only fields + required radio `rescheduleOutcome` ∈ `{RescheduledNoBill, RescheduledLate}` + checkbox "Change Re-Schedule Date & Time" -> when checked: required date picker + time select + textarea `adminReScheduleReason`. Posts to `ApproveRescheduleAsync`. If override checked, body includes `overrideSlotId` + `adminReScheduleReason`.
   - **Reschedule-Reject:** required textarea `reason` + Reject button. Posts to `RejectRescheduleAsync`.
5. Concurrency stamp: thread `concurrencyStamp` from list-row through the modal payload.

Tests:
- Component test for each modal mode: validate body shape, primary-button enable/disable, danger styling on reject.
- E2E test (Playwright) for cancel-approve happy path: list -> open modal -> pick CancelledNoBill -> Approve -> list refreshes with row marked Accepted.

### Acceptance criteria (C3)

- [ ] Side-nav "Change Requests" visible only to StaffSupervisor / ITAdmin.
- [ ] Cancel list: action column shows approve icon ONLY (per OLD parity, reject is omitted in cancel list).
- [ ] Reschedule list: action column shows approve + reject icons (subject to JAL-acceptance gating for IsBeyondLimit=true).
- [ ] Cancel-approve modal -- pick outcome -> appointment status flips to chosen outcome; ChangeRequest goes Accepted; slot freed via cascade; Approved-event email fires (Phase 18 handler).
- [ ] Cancel-reject modal -- write reason -> ChangeRequest goes Rejected; appointment stays Approved (no revert needed since OLD/NEW kept it Approved during Pending); Rejected-event email fires.
- [ ] Reschedule-approve no-override -- new appointment row created with cloned children; source -> Rescheduled*; old slot -> Available; new slot -> Booked.
- [ ] Reschedule-approve with override -- supervisor picks different slot + reason -> user's Reserved slot released; admin-picked slot -> Booked; admin-reason recorded on ChangeRequest.
- [ ] Reschedule-reject -- source reverts to Approved; user's Reserved slot freed.
- [ ] Optimistic-concurrency: parallel approve/reject by another supervisor returns `ChangeRequestAlreadyHandled` -- popup renders.

### Gotchas (C3)

1. NEW class name is `AppointmentChangeRequestsApprovalAppService` (sibling to the submit AppService) -- NOT a partial class. Endpoints land at `api/app/appointment-change-request-approvals` (per `[RemoteService(IsEnabled=false)]` + manual controller `AppointmentChangeRequestApprovalController`). Verify the proxy path before binding.
2. **Cascade-clone gap** flagged above: NEW does NOT clone `CustomFieldsValues` or `AppointmentDocuments`. Add `// PARITY-FLAG` comment + row in `_parity-flags.md` before C3 ships, OR fix Phase 17 cascade-clone first.
3. The "admin-override-with-reason" overrides the SLOT, not the cancel-time gate. The cancel-time gate is enforced ONLY at submit; supervisors can always approve regardless of date proximity.
4. Per design doc 4c, OLD's reschedule-action-column hides BOTH icons when `isBeyondLimit=true` AND JDF document not yet `Accepted`. C3 must replicate this gate -- requires the JAL document workflow to be visible in the row. JAL upload/review is outside C3 scope but the gate must be implemented.
5. Cancel list reject icon is COMMENTED OUT in OLD HTML (`detail.html:81`) -- per design doc Exception 3, NEW preserves this (cancel list shows approve only). The `RejectCancellationAsync` endpoint is still wired -- perhaps used internally or by a future flow.
6. `RescheduledNoBill` vs `RescheduledLate` is a billing-side classification. OLD preserves both as terminal states; NEW does too. The supervisor MUST pick one at approval time.

---

## 4. C4 -- Wire `AppointmentChangeRequestSubmittedEto` Handler

### OLD source

OLD does NOT use a clean Eto-handler model. The OLD `Add` cancel-path does NOT send a "submit" email at all (cite C1 section above). The OLD `Add` reschedule-path DOES send a "submit" email via `SendEmailData` (line 215 -> line 756-768 with `rescheduleRequested=true`) targeting all `appointmentStackHoldersEmailPhone.EmailList`.

So OLD has 1.5 cascades on submit:

| Submit type | OLD email | Recipients | Template |
|---|---|---|---|
| Cancel-submit | None directly; `SendEmailToClinicStaffForCancellation` (line 623) is callable but called from `Update` only -- NEW must extend coverage | Clinic supervisors (in `Update`) | `ClinicalStaffCancellation` |
| Reschedule-submit | `SendEmailData` rescheduleRequested branch | `appointmentStackHoldersEmailPhone.EmailList` (party fan-out) | `PatientAppointmentRescheduleReq` |

### NEW current state

- Eto: `AppointmentChangeRequestSubmittedEto` (`Domain.Shared\Notifications\Events\AppointmentChangeRequestSubmittedEto.cs`).
- Published from:
  - `Manager.SubmitCancellationAsync` line 150-158 (`ChangeRequestType=Cancel`).
  - `Manager.SubmitRescheduleAsync` line 266-274 (`ChangeRequestType=Reschedule`).
- **No subscriber today.** Confirmed via `Grep`: no `ILocalEventHandler<AppointmentChangeRequestSubmittedEto>` exists.
- Sibling handlers exist (`ChangeRequestApprovedEmailHandler.cs` for Approved, `ChangeRequestRejectedEmailHandler.cs` for Rejected) -- both at `Application\Notifications\Handlers\`. C4 follows the same pattern verbatim.
- Template codes already seeded: `CancellationRequestSubmitted`, `RescheduleRequested` (`NotificationTemplateConsts.Codes`).

### Recipient resolution (NEW)

`IAppointmentRecipientResolver.ResolveAsync(appointmentId, NotificationKind)` -- existing service used by `ChangeRequestApprovedEmailHandler`. Returns the OLD-parity stakeholder fan-out list (patient, attorneys, accessors, internal users).

OLD's "staff group" recipients on submit are not a separate group -- the OLD reschedule-submit email goes to the FULL stakeholder list (`EmailList` -- already includes clinic staff via `vInternalUserEmail` in places). NEW will use `NotificationKind.Submitted` which the resolver already handles.

If the resolver does not have a Submitted kind, add it. Likely it does -- inspect `AppointmentRecipientResolver` or extend.

### Implementation plan (C4)

Backend handler at `src\HealthcareSupport.CaseEvaluation.Application\Notifications\Handlers\ChangeRequestSubmittedEmailHandler.cs`:

```
public class ChangeRequestSubmittedEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestSubmittedEto>,
    ITransientDependency
{
    // Constructor injects: INotificationDispatcher, DocumentEmailContextResolver,
    // IAppointmentRecipientResolver, ICurrentTenant, ILogger.

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentChangeRequestSubmittedEto eventData)
    {
        if (eventData == null) return;
        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, null);
            if (ctx == null) { _logger.LogWarning(...); return; }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId, NotificationKind.Submitted);
            var recipients = resolverOutput.Where(r => !string.IsNullOrWhiteSpace(r.To))
                .Select(r => new NotificationRecipient(r.To, r.Role, r.IsRegistered))
                .ToList();
            if (recipients.Count == 0) { _logger.LogInformation(...); return; }

            var templateCode = eventData.ChangeRequestType switch
            {
                ChangeRequestType.Cancel     => NotificationTemplateConsts.Codes.CancellationRequestSubmitted,
                ChangeRequestType.Reschedule => NotificationTemplateConsts.Codes.RescheduleRequested,
                _                            => NotificationTemplateConsts.Codes.CancellationRequestSubmitted,
            };

            var variables = DocumentNotificationContext.BuildVariables(...);
            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"ChangeRequestSubmitted/{eventData.ChangeRequestType}/{eventData.ChangeRequestId}");
        }
    }
}
```

Idempotency: ABP's `ILocalEventHandler` does not provide built-in dedupe. NEW already accepts at-least-once delivery in sibling handlers. C4 follows the same convention; the dispatcher logs `contextTag` for observability.

Tests (`Application.Tests\Notifications\Handlers\ChangeRequestSubmittedEmailHandlerTests.cs`):
- Cancel submit -> dispatches `CancellationRequestSubmitted` template to resolved recipients.
- Reschedule submit -> dispatches `RescheduleRequested` template.
- No recipients -> logs + returns silently.
- Null event -> returns silently.

### Acceptance criteria (C4)

- [ ] External user submits a cancel -- `CancellationRequestSubmitted` email goes out to all stakeholders (party fan-out per `AppointmentRecipientResolver`).
- [ ] External user submits a reschedule -- `RescheduleRequested` email goes out.
- [ ] Email subject + body reference the appointment (request confirmation #, patient name, claim #, ADJ, slot date/time).
- [ ] Handler is registered in DI (transient via `ITransientDependency`).
- [ ] Tests in `ChangeRequestSubmittedEmailHandlerTests.cs` pass.

### Gotchas (C4)

1. The `AppointmentRecipientResolver.NotificationKind` enum may not have a `Submitted` value. If not, ADD it -- the resolver should treat `Submitted` similarly to `Approved` (full stakeholder fan-out). Verify before assuming.
2. NEW's `DocumentEmailContextResolver` currently expects an `appointmentDocumentId` -- pass `null` per the sibling Approved handler.
3. Idempotency: if Manager retries (UoW rollback) the Eto re-publishes. ABP's Local Event Bus does NOT replay across process restarts (Local is in-memory). Acceptable for at-least-once.
4. OLD's cancel-submit had NO email (only the submit-reschedule did). NEW SHOULD send a cancel-submit email per `CancellationRequestSubmitted` seed -- this is a deliberate parity-improvement.
5. The OLD reschedule-submit fan-out used `appointmentStackHoldersEmailPhone.EmailList` -- a comma-joined string sent via `SendMail.SendSMTPMail`. NEW splits per-recipient (cleaner; preserves localization per role). Functional outcome equivalent.

---

## 5. C5 -- Wire `AppointmentAccessorInvitedEto` Handler

### OLD source

`AppointmentAccessorDomain.cs:69-89` (`Add` -> `CreateAccountOfAppointmentAccessors`) and lines 263-303 (auto-create-or-link logic):

- OLD looks up `User.EmailId == accessor.EmailId.ToLower() AND StatusId != Delete`.
- If user does NOT exist -> creates a `User` row with:
  - Random 8-char password split as `XXXX@XXXX` (line 267-268).
  - `RoleId = appointmentAccessor.RoleId` (the requested accessor role -- Patient / Adjuster / Applicant Atty / Defense Atty).
  - `StatusId = Active`, `IsActive = true`, `IsVerified = true` (auto-verified -- no email-verify gate), `IsAccessor = true`.
  - Encrypted password via `PasswordHash.Encrypt`.
  - Sends email via `SendEmailToAccessor(appointment, email, user, randomPassword)` -- includes the temp password.
- If user EXISTS -> `SendEmailToAccessor(appointment, email)` (no password in body).
- Email template: `AccessorAppointmentBooked` (`Accessor-Appointment-Booked.html`).
- Email subject: `"Patient Appointment Portal - " + patientDetailsEmailSubject + " - Please find these appointment details"` (line 258).

### NEW current state

- Eto: `AppointmentAccessorInvitedEto` (`Domain.Shared\Notifications\Events\AppointmentAccessorInvitedEto.cs`).
- Published from `AppointmentAccessorManager.CreateOrLinkAsync` line 150-159 ONLY in the `CreateUserAndLink` branch (i.e., when NEW user is created).
- **No subscriber today.** Confirmed via Grep -- no handler subscribes.
- The auto-create logic is ALREADY DONE in `AppointmentAccessorManager.CreateOrLinkAsync` (Phase 11i, 2026-05-04, lines 81-167):
  - Looks up by email via `IIdentityUserManager`.
  - On miss: creates `IdentityUser` with random temp password, adds to requested role, links accessor row, publishes `AppointmentAccessorInvitedEto`.
  - On hit + role-match: links accessor only (no event).
  - On hit + grant-role-and-link: adds the missing role, links, no event.
  - On role mismatch: throws `BusinessException(AppointmentAccessorRoleMismatch)`.

So C5 is ONLY the email handler -- the user-creation already happens in the Manager. The Eto carries: `AppointmentId, InvitedUserId, TenantId, Email, RoleName, AccessTypeId, OccurredAt`.

The Eto does NOT carry the temp password. The handler must use the standard ABP password-reset-token flow OR retrieve the password from a one-shot store. Inspect existing email content: NEW uses `AccessorInvited` template code; the body should include a portal link + reset-password instruction, NOT a plaintext password (modern security baseline).

### Implementation plan (C5)

Backend handler at `src\HealthcareSupport.CaseEvaluation.Application\Notifications\Handlers\AccessorInvitedEmailHandler.cs`:

```
public class AccessorInvitedEmailHandler :
    ILocalEventHandler<AppointmentAccessorInvitedEto>,
    ITransientDependency
{
    // Constructor injects: INotificationDispatcher, DocumentEmailContextResolver,
    // IIdentityUserManager (for password-reset token if needed),
    // ICurrentTenant, ILogger.

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentAccessorInvitedEto eventData)
    {
        if (eventData == null) return;
        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, null);
            if (ctx == null) { _logger.LogWarning(...); return; }

            // Generate password-reset token for the invited user so the
            // email includes a one-shot setup link instead of a plaintext
            // temp password (security-improvement vs OLD).
            var user = await _userManager.GetByIdAsync(eventData.InvitedUserId);
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var setupUrl = BuildAccountSetupUrl(ctx.PortalBaseUrl, eventData.InvitedUserId, resetToken);

            var recipients = new[] { new NotificationRecipient(
                email: eventData.Email,
                role: eventData.RoleName,
                isRegistered: false) };

            var variables = DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: null,
                rejectionNotes: null,
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);
            // Add ##URL## variable for the setup link.
            variables["##URL##"] = setupUrl;
            variables["##Email##"] = eventData.Email;

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AccessorInvited,
                recipients: recipients,
                variables: variables,
                contextTag: $"AccessorInvited/{eventData.InvitedUserId}");
        }
    }
}
```

Idempotency: ABP-Identity's `GeneratePasswordResetTokenAsync` is single-use and time-bound. Re-publishing the Eto (rare; UoW retries) regenerates a new token; OK.

User auto-creation: NOTHING TO DO in C5. The Manager already auto-creates. C5 is email-only.

Tests (`Application.Tests\Notifications\Handlers\AccessorInvitedEmailHandlerTests.cs`):
- Eto fired -> dispatches `AccessorInvited` template with `##URL##` populated.
- Null event -> returns silently.
- Missing user -> logs + returns silently.

### Acceptance criteria (C5)

- [ ] When booking flow auto-creates an accessor user, an email is sent to the accessor's address with `AccessorInvited` template + setup link.
- [ ] Setup link uses `IIdentityUserManager.GeneratePasswordResetTokenAsync` -- no plaintext password.
- [ ] If user already existed (no auto-create), the Manager does NOT publish the Eto -- so no email is sent. (OLD sent a different email in this case via `SendEmailToAccessor(appointment, email)` without the password; if NEW wants to match OLD here, Manager must publish a different Eto for the link-existing path. **Recommend:** add `AppointmentAccessorLinkedEto` later if Adrian wants strict OLD parity for this case.)
- [ ] Test coverage for happy path, null-event, and missing-user.

### Gotchas (C5)

1. The Manager publishes the Eto ONLY on `CreateUserAndLink`, NOT on `LinkExisting` or `GrantRoleAndLink` (lines 130-160). OLD also sent an email when user existed but the body was different. **Parity question for Adrian:** should NEW send a "you've been added as an accessor" email when the user already existed? If yes, add a separate `AppointmentAccessorLinkedEto` and a sibling handler. This question is NOT blocking C5 but is a known parity gap.
2. The Eto omits `FirstName`/`LastName` -- if the email body wants to address by name, the handler must fetch the IdentityUser. Acceptable; existing handlers do the same.
3. The setup link URL builder needs the portal base URL -- either thread via `DocumentEmailContext.PortalBaseUrl` or read from `ICurrentTenant.GetCurrentTenantContext()` settings. Existing `INotificationDispatcher` consumers use the former.
4. **Security improvement vs OLD:** OLD's email contained the literal random password (`UserInfo = "EmailId : " + user.EmailId + " Password : " + password`, line 249). NEW MUST NOT echo a plaintext password. Use `GeneratePasswordResetTokenAsync` -> reset link. This is a deliberate security-driven deviation; document in `_parity-flags.md` as `resolved: secure-by-default`.
5. The AccessType enum values are `View=23, Edit=24` (`AccessTypeId` int field on Eto). The email template can render different copy based on AccessType -- e.g., "view your appointment" vs "manage your appointment". Optional; out of C5 minimum scope.
6. OLD set `IsVerified = true` on auto-create (auto-verified, no email-confirm step). NEW's `IIdentityUserManager.CreateAsync` sets `IsActive=true` but ABP defaults `EmailConfirmed=false`. If C5 wants OLD-parity auto-verify, the Manager (NOT the handler) must set `EmailConfirmed=true` after creation. Verify Manager behavior; if not set, surface as a `// PARITY-FLAG`.

---

## 6. Consolidated parity flags to add

Append to `docs/parity/_parity-flags.md`:

| Feature | OLD source | Description | Status |
|---|---|---|---|
| Reschedule-approve cascade-clone | `AppointmentChangeRequestDomain.cs:435-450, 523-549` | NEW does not clone `CustomFieldsValues` or `AppointmentDocuments` to the new appointment row | needs-fix (Phase 17 cleanup) |
| Accessor email when user exists | `AppointmentAccessorDomain.cs:292-294, 337-339` | OLD sends a no-password "you've been added" email when accessor user already exists; NEW does not (Manager skips Eto on LinkExisting/GrantRoleAndLink) | needs-decision |
| Accessor email contains plaintext password | `AppointmentAccessorDomain.cs:249, 267-268` | OLD echoed the temp password in the email body; NEW will use a reset-token link instead | resolved: secure-by-default |
| Cancel-submit had no email in OLD `Add` | `AppointmentChangeRequestDomain.cs:197-223` | OLD did NOT send any email on cancel submit; NEW will (CancellationRequestSubmitted) | resolved: parity-improvement |
| Auto-verify on accessor account creation | `AppointmentAccessorDomain.cs:283` (`IsVerified = true`) | OLD auto-set verified=true on auto-create; verify NEW Manager does the same OR add `EmailConfirmed=true` | needs-test |

---

## 7. Verification strategy (post-implementation)

End-to-end (Playwright, internal-shell + external-shell):

1. External user submits cancel -> `CancellationRequestSubmitted` email sent to all stakeholders.
2. Supervisor views Cancel Requests page -> sees pending row -> clicks Approve -> picks CancelledNoBill -> on submit, ChangeRequest = Accepted, appointment = CancelledNoBill, slot = Available, `CancellationRequestAccepted` email sent.
3. External user submits reschedule -> `RescheduleRequested` email sent; new slot = Reserved; parent = RescheduleRequested.
4. Supervisor approves reschedule (no override) -> new appointment row created with cloned children; source = RescheduledNoBill; old slot = Available; new slot = Booked; `RescheduleApproved` email sent.
5. Supervisor approves reschedule WITH override -> user's Reserved slot freed; admin-picked slot = Booked; admin-reason recorded; `RescheduleApproved` email sent (admin-override branch).
6. Supervisor rejects reschedule -> source reverts to Approved; user's Reserved slot freed; `RescheduleRejected` email sent.
7. Booking with NEW accessor email -> `AccessorInvited` email sent with setup link; clicking link lands on password-set page.

Unit tests:

- `ChangeRequestSubmittedEmailHandlerTests` (C4): cancel + reschedule branches, null event, no recipients.
- `AccessorInvitedEmailHandlerTests` (C5): happy path, null event, missing user.
- Component tests for C1, C2, C3 modals: form validity, submit-payload shape, button state.

Existing unit tests to re-run (sanity):
- `AppointmentChangeRequestManagerTests` (Phase 15+16 manager).
- `AppointmentChangeRequestsApprovalAppServiceTests` (Phase 17, if present).
- `ChangeRequestApprovedEmailHandlerTests` / `ChangeRequestRejectedEmailHandlerTests` (Phase 18).
