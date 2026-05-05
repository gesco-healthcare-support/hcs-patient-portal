---
stage: 5
task: A1 -- clinic staff approval UI + send-back modal
date: 2026-05-04
old-source: P:\PatientPortalOld
new-source: W:\patient-portal\replicate-old-app
status: research-only (no edits)
---

# Stage 5 -- Clinic-staff approval UI + send-back modal (research)

## 1. Headline findings (must read before implementing)

1. **OLD has NO send-back action.** OLD's pending-appointment surface offers
   only Approve and Reject (plus internal-user-initiated Cancel). The
   `AppointmentStatusType` enum (`P:\PatientPortalOld\PatientAppointment.Models\Enums\AppointmentStatusType.cs`:9-24)
   has 13 values and no `SentBack` / `AwaitingMoreInfo`. The send-back
   workflow in NEW is a NEW-only addition, not a parity port. **Strict-parity
   exception #5** in `docs/design/clinic-staff-appointment-approval-design.md`:357
   already documents this. There is no OLD verbatim source for the send-back
   modal -- the spec for it lives entirely in the NEW design doc.
2. **The Domain.Shared `AppointmentStatusType.cs` enum file is stale.** That
   file (`src/.../Domain.Shared/Enums/AppointmentStatusType.cs`:6-23) says
   "AwaitingMoreInfo=14 was removed when SendBack was deleted in Phase 0.2"
   but the Angular proxy enum
   (`angular/src/app/proxy/enums/appointment-status-type.enum.ts`:14)
   has `AwaitingMoreInfo = 14` AND the entire send-back DTO surface
   (`SendBackAppointmentInput`, `AppointmentSendBackInfoDto`, `sendBack`,
   `saveAndResubmit`, `getLatestUnresolvedSendBackInfo`) is wired in the
   proxy, the appointment-view component, and the appointment service.
   The send-back flow IS the current target -- the Domain.Shared enum file
   needs to be re-checked (likely the comment is the stale artefact, not the
   feature). **Verify with Adrian before implementation.**
3. **OLD validation: only the responsible-user dropdown is required for
   Approve.** Comments textarea is OPTIONAL. Rejection notes textarea is
   REQUIRED. (`patientappointment-portal/.../appointments/domain/appointment.domain.ts`:894-910).
4. **NEW Phase 12 backend already ships a richer Approve / Reject surface**
   at `api/app/appointment-approvals/{id}/{approve|reject}` that takes
   `PrimaryResponsibleUserId` + `OverridePatientMatch` for approve and a
   required `Reason` for reject. The legacy thin `AppointmentsAppService.ApproveAsync(id)`
   on `api/app/appointments/{id}/approve` (no body) is what the existing
   approve modal currently calls. **Two parallel surfaces exist; the Angular
   approve modal is wired to the wrong one.**
5. **`SendBackAppointmentModalComponent` is imported by the view component
   (`appointment-view.component.ts`:31) but the file does NOT exist** under
   `angular/src/app/appointments/appointment/components/`. This is the
   Phase 19b deferred task A1 closes.

---

## 2. OLD source (verbatim citations)

### 2.1 OLD status enum

`P:\PatientPortalOld\PatientAppointment.Models\Enums\AppointmentStatusType.cs`:9-24

```
Pending = 1, Approved = 2, Rejected = 3, NoShow = 4,
CancelledNoBill = 5, CancelledLate = 6, RescheduledNoBill = 7,
RescheduledLate = 8, CheckedIn = 9, CheckedOut = 10, Billed = 11,
RescheduleRequested = 12, CancellationRequested = 13
```

No SentBack, no AwaitingMoreInfo.

### 2.2 OLD Approve / Reject status mutation (slot transitions)

`P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs`:586-611

```csharp
private void UpdateDoctorAvailbilty(Appointment appointment) {
    DoctorsAvailability doctorsAvailability = ... ;
    if (doctorsAvailability != null) {
        switch (appointment.AppointmentStatusId) {
            case (int)AppointmentStatusType.Pending:
                doctorsAvailability.BookingStatusId = BookingStatus.Reserved;
                break;
            case (int)AppointmentStatusType.Approved:
                doctorsAvailability.BookingStatusId = BookingStatus.Booked;
                break;
            case (int)AppointmentStatusType.Rejected:
                doctorsAvailability.BookingStatusId = BookingStatus.Available;
                break;
            case (int)AppointmentStatusType.CancelledNoBill:
                doctorsAvailability.BookingStatusId = BookingStatus.Available;
                break;
        }
        ...
    }
}
```

### 2.3 OLD ApproveDate stamp + package-doc trigger

`AppointmentDomain.cs`:453-456 -- AppointmentApproveDate stamped only on
the Approve transition path:

```csharp
if (appointment.AppointmentStatusId == (int)AppointmentStatusType.Approved
    && appointment.IsStatusUpdate && appointment.IsInternalUserUpdateStatus) {
    appointment.AppointmentApproveDate = DateTime.Now;
}
```

`AppointmentDomain.cs`:560-566 -- package documents auto-queued + responsible-user
email sent only on Approve:

```csharp
if (appointment.AppointmentStatusId == (int)AppointmentStatusType.Approved
    && appointment.IsInternalUserUpdateStatus) {
    User user = ... .Where(x => x.UserId == appointment.PrimaryResponsibleUserId).FirstOrDefault();
    SendEmail(appointment.AppointmentStatusId, appointment, user.EmailId, appointment.IsInternalUserUpdateStatus);
    AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail(appointment);
}
```

### 2.4 OLD idempotency check

`AppointmentDomain.cs`:312-344 -- verbatim error strings:
"Appointment Already Approved", "Appointment Already Rejected",
"Appointment Already checked in", "Appointment Already checked out",
"Appointment Already billed".

### 2.5 OLD email cascade (the load-bearing block)

`AppointmentDomain.cs`:923-1035 -- per status, in this order:

- **Pending (booking time, external user):**
  1. Email to patient (template `PatientAppointmentPending`) CC: `clinicStaffEmail`.
  2. Then second send: email to all internal users with `RoleId = StaffSupervisor | ClinicStaff`
     using template `PatientAppointmentApproveReject`. (Lines 935-951.)
- **Approved by internal user (`internalUserUpdateStatus == true`):**
  1. Email to all stakeholders (patient, attorneys, claim examiner, accessors,
     employer if email present) using template `PatientAppointmentApprovedInternal`,
     CC: `clinicStaffEmail`. (Lines 953-967.)
  2. Then `AppointmentDomain.Update` line 562-564 separately calls `SendEmail`
     with `internalUserUpdateStatus = true` against the responsible user's
     personal email (a SECOND send to the responsible user only).
  3. Then `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail`
     fires (line 564). This does TWO things: (a) inserts AppointmentDocument
     rows for each Document the AppointmentType's package contains, with
     `Pending` status + `VerificationCode`; (b) emails the patient with
     per-document upload links.
- **Approved by external user (re-eval flow, `internalUserUpdateStatus == false`):**
  1. Email to patient/creator using template `PatientAppointmentApprovedExt`
     CC: `clinicStaffEmail`. (Lines 968-981.)
- **Rejected:**
  1. Email to patient/creator only using template `PatientAppointmentRejected`,
     no CC. (Lines 984-991.) Body interpolates `RejectionNotes`.

### 2.6 OLD Angular approval modal (the shared one)

OLD does NOT have an Approve button on the check-in list -- approval lives
on the appointment edit page (`appointment-edit.component.ts`:814-877):

```typescript
approveRequest(): void {
  let updatePropertys = {
    appointmentStatusId: AppointmentStatusTypeEnums.Approved,
    isInternalUserUpdateStatus: true, isDataUpdate: false, isStatusUpdate: true
  };
  this.popup.show(AppointmentViewComponent, { appointmentDetail: updatePropertys, ... });
}

rejectRequest(): void {
  let updatePropertys = {
    appointmentStatusId: AppointmentStatusTypeEnums.Rejected, ...
  };
  this.popup.show(AppointmentViewComponent, { ... });
}
```

The shared `AppointmentViewComponent` (`view/appointment-view.component.ts`:1-163;
`view/appointment-view.component.html`:1-155) renders different fields by
`appointmentStatusId`:

- **Approved:** Patient match card (when `IsPatientAlreadyExist`), required
  Responsible-User dropdown (`primaryResponsibleUserId`), optional comments
  textarea (`internalUserComments`).
- **Rejected:** required textarea (`rejectionNotes`).
- **CancelledNoBill:** required textarea (`cancellationReason`).

Validation wiring at `appointment.domain.ts`:894-910:

```typescript
bindFromGroupOfAppoinmentUpdateRequest(appointmentStatusId, ...): FormGroup {
  ...
  if (appointmentStatusId == AppointmentStatusTypeEnums.Approved) {
    appointmentRequestUpdateFormGroup.controls.primaryResponsibleUserId
      .setValidators([requiredValidator()]);
  }
  if (appointmentStatusId == AppointmentStatusTypeEnums.Rejected) {
    appointmentRequestUpdateFormGroup.controls.rejectionNotes
      .setValidators([requiredValidator()]);
  }
}
```

Submit path: `PATCH /api/Appointments/{id}` with the JsonPatch document
(controller `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsController.cs`:54-91)
which loads the full aggregate, applies the patch, and calls `Domain.Appointment.Update`
which dispatches into the slot/email/doc-queue cascade above.

### 2.7 OLD permissions (approval gate)

There is no explicit "Approve" permission. The OLD page is gated by
`PageAccess` `applicationModuleId: 6, accessItem: 'add'` at the route level.
Internal-user roles `ClinicStaff (3)`, `StaffSupervisor (5)`, `ItAdmin (7)`
are the only ones that surface the page. Roles enum at
`P:\PatientPortalOld\PatientAppointment.Models\Enums\Roles.cs` (per role
matrix in parity audit).

---

## 3. OLD UX (where buttons live, modal contents, validation)

### 3.1 Entry point

OLD: appointment edit page (`appointment-edit.component.html`). Approve / Reject
icon buttons appear when status is Pending, on a status-action row at the top
of the form. `pendingState` flag (line 791-810 of edit component) gates
visibility.

### 3.2 Approve modal contents

```
+---------------------------------------+
| Approve appointment request    [X]    |
| Please approve an appointment...      |
+---------------------------------------+
| (Patient match card -- only if        |
|  IsPatientAlreadyExist == true)       |
|  "Patient record has been merged      |
|   with existing patient."             |
|  Name / Cell / Phone / Email / DOB /  |
|  SSN / Language / Street / City /     |
|  State / Zip / Referred-by /          |
|  Interpreter Vendor                   |
+---------------------------------------+
| Responsible User * [select dropdown]  |
|   (internalUserNameLookUps)           |
| Any comments? [textarea, optional]    |
+---------------------------------------+
| [Approve]   [Close]                   |
+---------------------------------------+
```

- Responsible User: REQUIRED; submit button disabled until non-null.
- Comments: OPTIONAL textarea, no max-length validator.

### 3.3 Reject modal contents

```
+---------------------------------------+
| Reject appointment request     [X]    |
| Please reject an appointment...       |
+---------------------------------------+
| Write Rejection Reason                |
| [textarea -- 5 rows]                  |
+---------------------------------------+
| [Reject]    [Close]                   |
+---------------------------------------+
```

- Rejection Reason: REQUIRED, free-text, no max-length in OLD.

### 3.4 Send-back modal

Does not exist in OLD.

---

## 4. State-machine table

| From | Trigger | To | Slot transition | Required input | OLD source |
|---|---|---|---|---|---|
| Pending | Approve | Approved | Reserved -> Booked | PrimaryResponsibleUserId (req); InternalUserComments (opt) | AppointmentDomain.cs:596-598, 453-456, 560-564 |
| Pending | Reject | Rejected | Reserved -> Available | RejectionNotes (req) | AppointmentDomain.cs:599-601 |
| Pending | Send back | (NEW only) AwaitingMoreInfo | Reserved -> Reserved (unchanged) | flaggedFields[]; note (opt) | NEW design doc |
| Approved | (no further approve) | -- | -- | "Appointment Already Approved" idempotency | AppointmentDomain.cs:319-322 |
| Rejected | (no further reject) | -- | -- | "Appointment Already Rejected" idempotency | AppointmentDomain.cs:323-326 |
| AwaitingMoreInfo | Approve / Reject | Approved / Rejected | per row above | per row above | NEW only |
| AwaitingMoreInfo | SaveAndResubmit (booker) | Pending | Reserved -> Reserved | -- | NEW only |

**Send-back keeps the slot Reserved.** This is a deliberate NEW design
choice (the slot is held while the booker fixes data), distinct from
Reject (which frees the slot).

---

## 5. Email cascade per action

### Approve (internal user)

| Order | Recipient | Template | Source |
|---|---|---|---|
| 1 | All stakeholders (patient, attorneys, accessors, claim examiner, employer-if-email) -- single SMTP send with semicolon-joined To list | `PatientAppointmentApprovedInternal` | AppointmentDomain.cs:957-966 |
| 2 | Responsible user only -- second SMTP send | `PatientAppointmentApprovedInternal` (same template, different recipient) | AppointmentDomain.cs:560-563 |
| 3 | Patient -- "your package documents are ready, upload links inside" | `PatientAppointmentApprovedExt`-style with per-document attachment / link | AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail (called at line 564) |
| -- | CC on (1) | `appsettings:clinicStaffEmail` config value | line 954 |
| -- | SMS to all stakeholder phones | (template commented out in OLD; SMS branch is dead code at lines 858-865) | AppointmentDomain.cs:858-866 |

### Reject

| Order | Recipient | Template | Source |
|---|---|---|---|
| 1 | Patient / creator only | `PatientAppointmentRejected` | AppointmentDomain.cs:984-990 |
| -- | No CC, no responsible-user notification, no package docs | -- | -- |
| -- | SMS branch dead code | `AppointmentRejected` (commented out) | lines 867-874 |

### Send back

NEW-only. No OLD source. Per NEW design doc (sections 5d) the expected
recipients are: patient + booker (creator) only -- they need to know what
to fix. Stakeholders (attorneys, etc.) do not receive a send-back notification
because the appointment is still alive. **Surface to Adrian: confirm
recipient list before wiring.**

### Key point on cascade ordering for handlers

The Phase 12 backend already publishes `AppointmentApprovedEto` /
`AppointmentRejectedEto` from `AppointmentApprovalAppService.cs`:108-117,
143-150. Subscribers fire in subscription order, so the implementation
must register them in this order to match OLD:
1. `StakeholderApprovalEmailHandler` (group SMTP send to all stakeholders)
2. `ResponsibleUserNotificationHandler` (separate send to responsible user)
3. `PackageDocumentQueueHandler` (queues docs + sends patient package email)

Phase 12 currently ships only (3) as a logging stub
(`PackageDocumentQueueHandler.cs`); (1) and (2) are deferred to Phase 14
per the parity audit (`docs/parity/clinic-staff-appointment-approval.md`:111).

---

## 6. OLD validation table

| Field | Approve | Reject | Send-back (NEW) | OLD source |
|---|---|---|---|---|
| PrimaryResponsibleUserId | REQUIRED | -- | -- | appointment.domain.ts:905-906 |
| internalUserComments | optional | -- | -- | view-component.html:141-144 |
| rejectionNotes | -- | REQUIRED | -- | appointment.domain.ts:909-910 |
| cancellationReason | -- | -- | -- (different action) | view-component.html:136-139 |
| flaggedFields[] | -- | -- | (NEW: at least 1?) | NEW only -- TBD |
| note | -- | -- | (NEW: optional? required?) | NEW only -- TBD |
| Idempotency | "Appointment Already Approved" | "Appointment Already Rejected" | (no OLD precedent) | AppointmentDomain.cs:319-326 |

OLD's responsible-user validation runs CLIENT-side via `requiredValidator()`;
the server (`UpdateValidation`) does NOT re-check non-null PrimaryResponsibleUserId.
NEW's `AppointmentApprovalValidator.EnsureApprovable` does re-check (rejects
`Guid.Empty`) -- this is a deliberate hardening, not a parity break.

---

## 7. NEW current state (Phase 12 backend)

### 7.1 AppService methods + permissions

Two parallel surfaces exist:

| AppService | Method | Endpoint | Permission attribute | DTO |
|---|---|---|---|---|
| `AppointmentsAppService` (legacy thin, line 1194-1199) | `ApproveAsync(Guid id)` | `POST api/app/appointments/{id}/approve` | `[Authorize(Appointments.Edit)]` | (no body) |
| `AppointmentsAppService` (legacy thin, line 1201-1206) | `RejectAsync(Guid id, RejectAppointmentInput)` | `POST api/app/appointments/{id}/reject` | `[Authorize(Appointments.Edit)]` | `RejectAppointmentInput { Reason? }` |
| `AppointmentApprovalAppService` (Phase 12 rich) | `ApproveAppointmentAsync(Guid id, ApproveAppointmentInput)` | `POST api/app/appointment-approvals/{id}/approve` | `[Authorize(Appointments.Approve)]` | `ApproveAppointmentInput { PrimaryResponsibleUserId (req), OverridePatientMatch }` |
| `AppointmentApprovalAppService` (Phase 12 rich) | `RejectAppointmentAsync(Guid id, RejectAppointmentInput)` | `POST api/app/appointment-approvals/{id}/reject` | `[Authorize(Appointments.Reject)]` | `RejectAppointmentInput { Reason }` -- whitespace fails `EnsureRejectable` |

There is also a `sendBack` endpoint at
`POST /api/app/appointments/{id}/send-back` exposed via the Angular proxy
(`angular/src/app/proxy/appointments/appointment.service.ts`:227-234) and
a `saveAndResubmit` at `/save-and-resubmit` (line 237-244), plus
`getLatestUnresolvedSendBackInfo` (line 246-253). The C# implementations
of these are NOT in `AppointmentsAppService.cs` (grep returned only Approve
and Reject) -- they are presumably elsewhere in the partial / separate
files. **TODO during build:** verify the C# send-back implementation exists
and which partial owns it; the task is "wire frontend" not "build backend"
per the prompt.

### 7.2 Permission constants

`src/.../Application.Contracts/Permissions/CaseEvaluationPermissions.cs`:103-104:

```csharp
public const string Approve = Default + ".Approve";
public const string Reject = Default + ".Reject";
```

No `SendBack` permission exists. Send-back is implicitly gated by
`Appointments.Edit` (on the legacy AppService) or by adding a new
permission. **Surface to Adrian: add `Appointments.SendBack` permission,
or piggyback on `.Edit`?**

### 7.3 Domain manager

`src/.../Domain/Appointments/AppointmentManager.cs`:141, 145:

```csharp
public virtual Task<Appointment> ApproveAsync(Guid id, Guid? actingUserId)
    => TransitionAsync(id, AppointmentTransitionTrigger.Approve, reason: null, actingUserId);
public virtual Task<Appointment> RejectAsync(Guid id, string? reason, Guid? actingUserId)
    => TransitionAsync(id, AppointmentTransitionTrigger.Reject, reason, actingUserId);
```

Stamps `AppointmentApproveDate`, validates state-machine, publishes
`AppointmentStatusChangedEto` for the slot cascade. Send-back transition
trigger is unconfirmed by this stage's reads -- expect a `SendBack`
trigger or a separate manager method.

### 7.4 Frontend current state

Located at `angular/src/app/appointments/appointment/components/`:

- `appointment-view.component.ts` (1604 lines): full appointment view page;
  imports `SendBackAppointmentModalComponent` from
  `'./send-back-appointment-modal.component'` (line 31) but **the file does
  not exist**. Build fails on first import resolution.
- `appointment-view.component.ts:31` -- the failing import.
- `appointment-view.component.ts:466-477` -- `availableActions` getter
  returns `['approve', 'reject', 'sendBack']` for Pending and
  `['approve', 'reject']` for AwaitingMoreInfo.
- `appointment-view.component.ts:527-542` -- `dispatchAction()` flips the
  appropriate `*ModalVisible` flag.
- `appointment-view.component.ts:556-571` -- `onActionSucceeded(dto)`
  handler used by all three modals.
- `approve-confirmation-modal.component.ts` (68 lines): "Are you sure?"
  confirmation. Calls `appointmentService.approve(id)` -- which hits the
  THIN endpoint `/api/app/appointments/{id}/approve`. **No PrimaryResponsibleUserId
  collected.** This is the gap closed by switching to the rich endpoint or
  adding the field to this modal.
- `reject-appointment-modal.component.ts` (95 lines): textarea, 500-char
  cap, calls `appointmentService.reject(id, { reason })` -- thin endpoint.
- `send-back-appointment-modal.component.ts`: **does not exist**. Build is
  currently broken until A1 ships.

Send-back support files that DO exist:
- `angular/src/app/appointments/send-back-fields.ts` -- the flagged-field
  vocabulary used by `buildFlaggedFieldLookup` (referenced at line 34 of
  `appointment-view.component.ts`).
- Proxy DTOs `SendBackAppointmentInput { flaggedFields, note? }` and
  `AppointmentSendBackInfoDto { ..., flaggedFields, note?, sentBackAt,
  sentBackByUserId, isResolved, resolvedAt? }` at
  `angular/src/app/proxy/appointments/models.ts`:91-106.

---

## 8. Implementation plan

### 8.1 Files to add (new)

| File | Purpose |
|---|---|
| `angular/src/app/appointments/appointment/components/send-back-appointment-modal.component.ts` | Modal class; emits succeeded(AppointmentDto) |
| `angular/src/app/appointments/appointment/components/send-back-appointment-modal.component.html` | Note textarea + flagged-field/section checkboxes |
| (test) `angular/src/app/appointments/appointment/components/send-back-appointment-modal.component.spec.ts` | Unit test: validates required flagged-fields, calls service |

### 8.2 Files to modify

| File | Change |
|---|---|
| `approve-confirmation-modal.component.ts` / `.html` | Add Responsible-User dropdown + optional Comments textarea; switch from `appointmentService.approve(id)` to a new RestService call against `/api/app/appointment-approvals/{id}/approve` with `{ primaryResponsibleUserId, overridePatientMatch: false }` body. Source the dropdown from `/api/app/users/internal-lookup` or equivalent. |
| `appointment-view.component.html` | Render the action dropdown's three options conditional on `availableActions`. (Already wired in TS at lines 466-477 -- verify the template uses the getter.) |
| (parity) `src/.../Domain.Shared/Enums/AppointmentStatusType.cs` | Re-add `AwaitingMoreInfo = 14`. Stale comment in this file says it was "removed when SendBack was deleted in Phase 0.2" but the proxy enum, send-back DTOs, and view-component all assume it exists. Confirm with Adrian. |

### 8.3 Send-back modal design (since OLD has no source)

Per `docs/design/clinic-staff-appointment-approval-design.md`:240-260, the
expected modal:

```
+---------------------------------------------+
| Send Back Appointment                 [X]   |
+---------------------------------------------+
| Note to applicant                           |
| [textarea, 4 rows, max 500 chars,           |
|  REQUIRED per design]                       |
|                                             |
| Flag sections needing correction:           |
|   [_] Appointment Details                   |
|   [_] Patient Demographics                  |
|   [_] Employer Details                      |
|   [_] Claim Information                     |
|   [_] Attorney Details                      |
|                                             |
| Flag specific fields:                       |
|   (multi-select from sectioned list,        |
|    keys per send-back-fields.ts vocab)      |
+---------------------------------------------+
| [Cancel]              [Send Back]           |
+---------------------------------------------+
```

Validation: `flaggedFields.length >= 1` (must flag at least one field for
the booker to know what to fix). Note is REQUIRED per design (textarea
non-empty after trim). Submit calls
`appointmentService.sendBack(appointmentId, { flaggedFields, note })`.

### 8.4 Permission attribute fix (one-line)

`AppointmentsAppService.cs`:1194 currently has
`[Authorize(CaseEvaluationPermissions.Appointments.Edit)]` on the THIN
`ApproveAsync(Guid id)`. This is hit by `approve-confirmation-modal.component.ts`
today. Two options:

- Switch the modal to call the RICH endpoint (preferred -- gathers
  PrimaryResponsibleUserId for parity) and leave the thin endpoint with
  `.Edit` (Session A territory; one-line cleanup later).
- Or change the attribute to `Approve` and keep calling the thin endpoint
  -- but this loses parity on Responsible User.

**Recommendation: switch the modal to the rich endpoint.** The thin
endpoint is dead code after that switch; cleanup goes on the ledger for
Sync 3.

### 8.5 Test plan (per task brief)

- Unit: send-back-modal validates required note + at least one flagged field.
- Unit: approve modal disables submit until responsible-user selected.
- Integration (Karma): full action-dropdown -> modal -> service round trip
  with mocked rest backend; assert correct URL + body shape per action.

---

## 9. Acceptance criteria

1. `npx ng build --configuration development` succeeds (the missing
   `send-back-appointment-modal.component` file no longer breaks the build).
2. On a Pending appointment, the action dropdown shows three options:
   Approve / Reject / Send Back.
3. Approve modal: Responsible-User select is REQUIRED; submit disabled
   until populated. Optional Comments textarea persists when supplied.
   Calls the RICH endpoint. Toast `::Appointment:Toast:Approved` on success.
4. Reject modal: existing 500-char-capped textarea behavior unchanged
   (NEW improvement over OLD's no-cap).
5. Send-back modal: required note (max 500), at least 1 flagged field,
   submit POSTs `{ flaggedFields[], note }` to `/api/app/appointments/{id}/send-back`.
   Toast on success. Status flips to AwaitingMoreInfo; appointment view
   refreshes via `onActionSucceeded`.
6. Idempotency: a second approve / reject on a non-Pending appointment
   surfaces the verbatim OLD error string ("Appointment Already Approved" /
   "Appointment Already Rejected") via the BusinessException toast.
7. Permission gating: external users do not see the action dropdown.
   Backend rejects with 403 when permission is missing (existing behavior).

---

## 10. Gotchas

### 10.1 Idempotency (already handled, but watch ordering)

`AppointmentApprovalValidator.EnsureApprovable` runs BEFORE
`_appointmentManager.ApproveAsync` (which has its own state-machine guard).
Two layers, but both fire from the same UoW. A double-click on the
approve button will fire two parallel HTTPs -- both hit
`AppointmentApprovalValidator.EnsureApprovable` against a NOT-YET-COMMITTED
state. The second request sees Pending, advances. To fix: rely on the
state machine's optimistic-concurrency guard (ABP's
`ConcurrencyStamp` on the entity). Or disable the modal button during
isBusy=true (already done in approve / reject components; replicate in
send-back modal).

### 10.2 Transaction boundary

`AppointmentApprovalAppService.ApproveAppointmentAsync` issues TWO
`UpdateAsync(autoSave: true)` calls (lines 86-98 and 106). If the manager's
`ApproveAsync` throws after the first save, `PrimaryResponsibleUserId`
persists but `AppointmentStatus` does not -- the appointment is in
Pending status with a responsible user assigned. Recoverable (a re-approve
finds the same responsible-user value pre-set), but worth noting in tests.

### 10.3 Event publication order

`AppointmentManager.ApproveAsync` publishes `AppointmentStatusChangedEto`
(slot cascade subscriber) BEFORE `AppointmentApprovalAppService` publishes
`AppointmentApprovedEto`. Subscribers to `AppointmentApprovedEto` (e.g.
`PackageDocumentQueueHandler`) cannot assume the slot has already
transitioned to Booked -- the slot cascade and the package-doc-queue both
fire from the same outbox-flush, but order between two different Etos is
not guaranteed. If a subscriber needs the slot status, it should re-query
the slot, not assume.

### 10.4 The Domain.Shared enum file

`src/.../Domain.Shared/Enums/AppointmentStatusType.cs` claims SendBack/AwaitingMoreInfo
was deleted, but every other file (proxy enum, view-component, send-back-fields,
proxy service) assumes `AwaitingMoreInfo = 14` exists. The enum file is
either stale (likely) or the proxy is out of sync with the backend. Re-run
`abp generate-proxy` and inspect the generated enum; the source of truth
is the backend `AppointmentStatusType` enum. **Do not start A1 until this
divergence is confirmed.**

### 10.5 Responsible-User dropdown source

NEW does not yet expose an `internal-user-lookup` endpoint by that name.
The closest is `/api/public/external-signup/external-user-lookup`
(used in `appointment-view.component.ts`:902-905) which returns EXTERNAL
users. The Responsible-User dropdown needs the INVERSE -- internal staff
only. Either add a backend lookup (preferred) or filter the existing
lookup client-side (fragile -- defaults to empty when the lookup excludes
roles per D-2 design). **Surface to Adrian: add a dedicated
`/api/app/users/internal-lookup` endpoint?**

### 10.6 Send-back vs reject (semantic difference for slot + data ownership)

OLD does not have send-back, so this is a NEW design choice (already
documented in design doc but worth restating):

| Aspect | Reject | Send back |
|---|---|---|
| Slot | Reserved -> Available (freed) | Reserved -> Reserved (held) |
| Data ownership | Booker may NOT edit; must re-create via Re-Request flow (which under OLD creates a new Appointment row with `IsReRequestForm=true`) | Booker re-edits SAME appointment row, then SaveAndResubmit transitions Pending |
| Status | Rejected (terminal) | AwaitingMoreInfo (transient) |
| Re-eligibility | New appointment row needed | Same row, no new RequestConfirmationNumber |

A1 must NOT create a fresh appointment row on send-back -- it edits the
same row in place. The `saveAndResubmit` proxy method confirms this is
already wired this way.

### 10.7 The legacy thin endpoint (Adrian's specific clarifying question)

`AppointmentsAppService.cs`:1194 has `[Authorize(Appointments.Edit)]` on
`ApproveAsync(id)`. This IS hit by the current `approve-confirmation-modal`
component (`appointment.service.ts`:208 -> `/api/app/appointments/{id}/approve`).
**The fix is therefore not "one line" if we want OLD parity** -- the modal
must be re-targeted to the rich endpoint (which captures PrimaryResponsibleUserId).
If we accept the parity exception (no Responsible User on approve), then
yes, changing the attribute from `.Edit` to `.Approve` is one line and
covers the gating concern. The recommended path (gather Responsible User)
makes it a 30-line modal change. **Adrian to choose.**

---

## 11. Open questions to surface to Adrian

1. Domain.Shared enum file vs proxy enum -- which is source of truth?
2. Add `Appointments.SendBack` permission, or piggyback on `.Edit`?
3. Send-back recipients: patient + booker only, or include all stakeholders?
4. Internal-user lookup endpoint: add new, or extend existing?
5. Approve modal: keep simple confirmation (NEW exception #1, descope
   Responsible User) or restore OLD parity (require Responsible User)?
6. Send-back note: required or optional?
7. Min-flagged-fields: 1, 0 (allow note-only send-back), or section-level
   only?
