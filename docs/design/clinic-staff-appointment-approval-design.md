---
feature: clinic-staff-appointment-approval
date: 2026-05-04
phase: 2-frontend (backend approve/reject endpoints done; send-back modal NOT yet created in Angular)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointments/
old-components:
  - detail/appointment-detail.component.ts + .html (pending appointments list + approve/reject dispatch, 243/153 lines)
  - view/appointment-view.component.ts + .html (shared approve/reject/cancel modal, 163/155 lines)
new-feature-path: angular/src/app/appointments/appointment/components/ (appointment-view + 3 action modals)
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: pending
---

# Design: Clinic Staff -- Appointment Approval

## Overview

The approval surface lets Clinic Staff (and Staff Supervisor / IT Admin) act on
appointment requests submitted by external users:

- **Approve** -- assign a responsible user + optional comment; moves status to Approved.
- **Reject** -- provide a rejection reason; moves status to Rejected.
- **Send Back** (NEW only) -- flag specific fields for the external user to correct; moves
  status to AwaitingMoreInfo, unlocks flagged fields for external user.
- **Cancel** (internal-initiated) -- capture a cancellation reason; moves to CancelledNoBill.

In OLD, all three actions (approve/reject/cancel) share a single `AppointmentViewComponent`
modal popup launched from the Pending Appointments list. In NEW, each action has its own
dedicated modal component, launched from an action dropdown on the full appointment-view page.

**Implementation gap:** The `SendBackAppointmentModalComponent` is referenced in the
view template but the file does not yet exist. This is a Phase 19b UI task.

---

## 1. Routes

| Surface | OLD | NEW |
|---|---|---|
| Pending appointments list | `/appointments/detail` (AppointmentDetailModule) | `/appointments` (appointment list, filtered/sorted by Pending) |
| Appointment view | `/appointments/:id` (AppointmentEditModule, opens from list) | `/appointments/view/:id` (dedicated view page) |
| Approve modal | `RxPopup` overlay over list | `<app-approve-confirmation-modal>` on view page |
| Reject modal | `RxPopup` overlay (same component, different `appointmentStatusId`) | `<app-reject-appointment-modal>` on view page |
| Send-back modal | n/a (not in OLD) | `<app-send-back-appointment-modal>` on view page (TODO) |

Guards:
- OLD: `canActivate: [PageAccess]` `applicationModuleId: 6`, `accessItem: 'add'` on detail route.
- NEW: `canActivate: [authGuard, permissionGuard]`; `CaseEvaluation.Appointments.Approve` permission
  gates the action dropdown visibility (`canTakeOfficeAction`).

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar).
Clinic Staff, Staff Supervisor, and IT Admin all use this shell.

---

## 3. Pending Appointments List (OLD)

```
+-------------------------------------------------------+
| [H2] Pending Appointments     [Search input] [Sync]  |
| [Advanced Search accordion]                          |
|   Appointment Type [select]  Confirmation # [text]   |
|   Location [select]  Claim # [text]                  |
|   Date Of Injury [date]  DOB [date]  SSN [masked]    |
|   [Search]  [Reset]                                  |
+-------------------------------------------------------+
| [Table rx-table]                                     |
| Patient Name | Gender | Type | Conf # |              |
| Appt Date | DOB | SSN | Claim # | DOI |              |
| Location | Created Date | Action                     |
+-------------------------------------------------------+
```

**Action column per row:**
- Approve icon (fa-check, green): `approveRequest(appointmentId, patientDetail)` -- opens modal
- Reject icon (fa-times, red): `rejectRequest(appointmentId, patientDetail)` -- opens modal

The modal receives the target `appointmentStatusId` (Approved or Rejected) and the
full patient detail ViewModel pre-loaded in the list TS.

OLD source: `detail/appointment-detail.component.html:1-153`,
`detail/appointment-detail.component.ts:1-243`

---

## 4. Approval/Rejection/Cancellation Modal (OLD)

One `AppointmentViewComponent` handles all three actions:

```
+---------------------------------------------+
| [Modal header: Approve/Reject Appointment]  |
+---------------------------------------------+
| [Patient info card]                         |
| Name: {name}  Phone: {phone}                |
| Email: {email}  DOB: {dob}                  |
| SSN: {ssn}  Language: {lang}                |
| Address: {address}                          |
+---------------------------------------------+
| [APPROVE fields -- *ngIf="isApprove"]       |
| Responsible User  [select, required]        |
| Any comments?     [textarea, optional]      |
+---------------------------------------------+
| [REJECT fields -- *ngIf="isReject"]         |
| Write Rejection Reason  [textarea, required]|
+---------------------------------------------+
| [CANCEL fields -- *ngIf="isCancelledNoBill"]|
| Write Cancellation Reason [textarea, required] |
+---------------------------------------------+
| [Approve/Reject/Submit]    [Close]          |
+---------------------------------------------+
```

**Responsible User** (approve only): Dropdown of internal users (`GetUsersLookup()`).
Required for approval -- cannot submit without selecting a responsible user.

**Rejection Reason** (reject only): Free-text textarea, required. No max-length enforced in OLD.

**Cancellation Reason** (cancel only): Free-text textarea, required.

**Comments** (approve only): Optional textarea. Stored as `internalUserComments`.

**Submit flow:**
1. `ngOnInit()` loads appointment via `appointmentsService.group([appointmentId], [lookups])`.
2. Binds patient detail to display card.
3. On submit: PATCHes `AppointmentRequestUpdateViewModel` with:
   - `appointmentStatusId` (target status)
   - `primaryResponsibleUserId` (approve only)
   - `internalUserComments` (approve only, optional)
   - `rejectionNotes` (reject only)
   - `cancellationReason` (cancel only)
   - `isInternalUserUpdateStatus: true`, `isStatusUpdate: true`, `isDataUpdate: false`
4. Shows toast: "approved" / "rejected" / "cancelled".
5. `popup.hide()` closes modal; parent list refreshes.

OLD source: `view/appointment-view.component.html:1-155`,
`view/appointment-view.component.ts:1-163`

---

## 5. NEW Approval Flow

In NEW, staff navigate to the full appointment-view page and use the action dropdown.

### 5a. Action Dropdown (top of appointment-view page)

Visible only when `canTakeOfficeAction` is true (Clinic Staff / Staff Supervisor / IT Admin
on Pending or AwaitingMoreInfo appointments):

```
+---------------------------------------------+
| Choose Action  [dropdown v]   [Submit]      |
+---------------------------------------------+
|   Approve                                   |
|   Reject                                    |
|   Send Back                                 |
+---------------------------------------------+
```

On **Submit**, the selected action opens its respective modal:
- `approve` → `approveModalVisible = true`
- `reject` → `rejectModalVisible = true`
- `sendBack` → `sendBackModalVisible = true`

NEW source: `appointment-view.component.html` (action dropdown section),
`appointment-view.component.ts` (`dispatchAction()`, `canTakeOfficeAction`)

---

### 5b. Approve Confirmation Modal (IMPLEMENTED)

```
+---------------------------------------------+
| Approve Appointment                   [X]   |
+---------------------------------------------+
| Are you sure you want to approve this       |
| appointment?                                |
+---------------------------------------------+
| [Cancel]              [Approve]             |
+---------------------------------------------+
```

Simple confirmation dialog -- no fields. Clicking "Approve":
1. `isBusy = true` (disables button to prevent double-submit).
2. `appointmentService.approve(appointmentId)` → `POST /api/app/appointments/{id}/approve`.
3. Success toast: `'::Appointment:Toast:Approved'`.
4. Emits `succeeded` event → parent page refreshes appointment.
5. Closes modal.

**Parity gap:** OLD required "Responsible User" selection before approving. NEW approval
modal has no Responsible User field. See Exception 1.

NEW source: `approve-confirmation-modal.component.html:1-20`,
`approve-confirmation-modal.component.ts:1-68`

---

### 5c. Reject Appointment Modal (IMPLEMENTED)

```
+---------------------------------------------+
| Reject Appointment                    [X]   |
+---------------------------------------------+
| Rejection Reason *                          |
| [textarea -- 4 rows, maxlength 500]         |
|   {placeholder text}            X / 500     |
+---------------------------------------------+
| [Cancel]              [Reject]              |
+---------------------------------------------+
```

**Reject button enabled** when: `reason.trim().length > 0 && reason.length <= 500`.

Submit:
1. `appointmentService.reject(appointmentId, { reason })` → `POST /api/app/appointments/{id}/reject`.
2. Success toast: `'::Appointment:Toast:Rejected'`.
3. Emits `succeeded`; closes modal.

**Parity note:** OLD had no max-length enforcement; NEW adds 500 char limit. This is an
intentional quality improvement (Exception 3).

NEW source: `reject-appointment-modal.component.html:1-39`,
`reject-appointment-modal.component.ts:1-95`

---

### 5d. Send-Back Modal (NOT YET CREATED -- Phase 19b UI task)

Referenced in view template but file does not exist:
```typescript
// appointment-view.component.ts line 31
import { SendBackAppointmentModalComponent } from './send-back-appointment-modal.component';
```

**Expected behavior** (from view template + component logic):

```
+---------------------------------------------+
| Send Back Appointment                 [X]   |
+---------------------------------------------+
| Note to applicant  [textarea, required]     |
|                                             |
| Flag sections needing correction:           |
| [Appointment Details]  [ ]                  |
| [Patient Demographics]  [ ]                 |
| [Employer Details]  [ ]                     |
| [Claim Information]  [ ]                    |
| [Attorney Details]  [ ]                     |
|                                             |
| Flag specific fields:                       |
| [multi-select or checkbox list of fields]   |
+---------------------------------------------+
| [Cancel]              [Send Back]           |
+---------------------------------------------+
```

- Submit → `POST /api/app/appointments/{id}/send-back` (endpoint TBD).
- Saves `AppointmentSendBackInfoDto` with: `note`, flagged section names, flagged field names.
- Appointment moves to `AwaitingMoreInfo` status.
- External user sees flagged-fields banner on their view page; flagged fields become editable.
- External user saves and resubmits → appointment returns to staff queue.

**This modal must be created before Phase 19b send-back flow can be tested.**

---

### 5e. Cancellation (Internal-Initiated)

In OLD, internal users could cancel an appointment from both the pending list AND the
full view page (button: "Cancel Appointment" with `pendingState` condition). In NEW:

- The `appointment-view.component` has no explicit Cancel button for internal users
  separate from the action dropdown.
- Cancellation by staff is likely mapped to the **Reject** action with a specific
  cancellation-type reason, or is a separate endpoint `POST /api/app/appointments/{id}/cancel`.
- **Surface to Adrian:** Verify whether internal-user cancellation is in the action
  dropdown or is a separate UI element. See Exception 4.

---

## 6. State Transitions

| From status | Action | To status | Who |
|---|---|---|---|
| Pending | Approve | Approved | Clinic Staff / Supervisor / IT Admin |
| Pending | Reject | Rejected | Clinic Staff / Supervisor / IT Admin |
| Pending | Send Back | AwaitingMoreInfo | Clinic Staff / Supervisor / IT Admin |
| Pending | Cancel | CancelledNoBill | Clinic Staff / Supervisor / IT Admin (OLD) |
| AwaitingMoreInfo | Approve | Approved | Clinic Staff / Supervisor / IT Admin |
| AwaitingMoreInfo | Reject | Rejected | Clinic Staff / Supervisor / IT Admin |
| Approved | Cancel | CancelledNoBill / CancelledLate | Clinic Staff / IT Admin |

State transitions also determine external-user button visibility (see
`external-user-view-appointment-design.md` Section 5 for the external-user side).

---

## 7. Role Visibility Matrix

| Role | Pending list | Approve | Reject | Send Back | Cancel (internal) |
|---|---|---|---|---|---|
| Clinic Staff | Yes (all pending) | Yes | Yes | Yes (Phase 19b) | Yes |
| Staff Supervisor | Yes | Yes | Yes | Yes | Yes |
| IT Admin | Yes | Yes | Yes | Yes | Yes |
| Patient / external | No | No | No | No | No |
| Doctor | No | No | No | No | No |

`canTakeOfficeAction` computed property gates the action dropdown:
- Must be internal role (Clinic Staff / Supervisor / IT Admin)
- Appointment must be in Pending or AwaitingMoreInfo status

---

## 8. Branding Tokens

| Element | Token |
|---|---|
| Action dropdown | `btn-secondary` via `--brand-secondary` |
| Submit / Approve button | `btn-primary` via `--brand-primary` |
| Reject button | `btn-danger` |
| Send Back button | `btn-warning` via `--status-pending` |
| Approval success toast | `--status-approved` (green) |
| Rejection reason counter (over limit) | `--status-rejected` (red) |
| Pending status badge | `--status-pending` (yellow) |

Token definitions: `_design-tokens.md`.

---

## 9. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Approval UI entry point | Approve/Reject icon buttons on list page row | Action dropdown on full appointment-view page |
| Modal architecture | Single shared `AppointmentViewComponent` popup (handles all 3 actions via `appointmentStatusId` parameter) | Three separate modal components (approve, reject, send-back) |
| Patient info display | Pre-loaded patient card in modal (name, phone, email, DOB, SSN) | Full patient demographics already visible on view page; modals focus on action only |
| Send-back / AwaitingMoreInfo | Does not exist | New workflow: flagged-fields modal + resubmit banner (modal not yet created) |
| Framework | `RxPopup.show(AppointmentViewComponent, ...)` (in-house overlay) | Angular CDK overlay / ABP modal component |
| API pattern | PATCH with `isInternalUserUpdateStatus: true` metadata flags | Dedicated endpoints per action (`/approve`, `/reject`, `/send-back`) |

---

## 10. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Responsible User on approve | Required dropdown; cannot approve without selecting a responsible user | Not present in NEW's simple confirmation modal | Surface to Adrian: Responsible User may need to be added to the approve modal or mapped to the current logged-in staff user automatically |
| 2 | Internal user comments on approve | Optional textarea stored as `internalUserComments` | Not present in NEW's confirmation modal | Surface to Adrian: omit, or add as an optional textarea in the approve modal |
| 3 | Rejection reason max length | No limit enforced in OLD (free text) | NEW enforces 500 char limit with counter display | Deliberate quality improvement; acceptable deviation |
| 4 | Internal cancellation | Explicit "Cancel Appointment" button in approval modal (CancelledNoBill) and on view page (pendingState) | Cancellation via internal user not visible in action dropdown; needs verification | Surface to Adrian: confirm cancellation is in the action dropdown or is a separate button on the view page |
| 5 | Send-back workflow | Does not exist; staff can only Approve or Reject | NEW adds Send Back → AwaitingMoreInfo → resubmit cycle; modal component not yet created | NEW-only feature; `SendBackAppointmentModalComponent` must be created in Phase 19b |
| 6 | Patient info in modal | Patient card shown inside the approval/rejection modal | Patient info visible throughout the full appointment-view page; modals are action-only | UX improvement: modal can be lean because context is already on screen |

---

## 11. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `detail/appointment-detail.component.html` | 1-153 | Pending appointments list with approve/reject action icons |
| `detail/appointment-detail.component.ts` | 1-243 | `approveRequest()`, `rejectRequest()` -- open modal with target status |
| `view/appointment-view.component.html` | 1-155 | Shared modal: patient card + conditional fields per action |
| `view/appointment-view.component.ts` | 1-163 | `ngOnInit()` loads appointment + lookups; `updateAppointmentRequest()` submits PATCH |

---

## 12. Verification Checklist

- [ ] Clinic Staff navigates to the appointments list and can filter by Pending status
- [ ] Clicking a pending appointment opens the full appointment-view page
- [ ] Action dropdown is visible to Clinic Staff / Supervisor / IT Admin on Pending appointments
- [ ] Action dropdown is hidden for external users and non-qualifying statuses
- [ ] Selecting "Approve" from dropdown + clicking Submit opens the approve confirmation modal
- [ ] Approve modal shows "Are you sure?" text and Cancel / Approve buttons
- [ ] Confirming approve calls `POST /appointments/{id}/approve` and shows success toast
- [ ] Appointment status changes to Approved; action dropdown disappears
- [ ] Selecting "Reject" opens the rejection reason modal with a required textarea
- [ ] Reject button is disabled when reason is empty or over 500 characters
- [ ] Character counter displays "X / 500" and turns red when over limit
- [ ] Confirming reject calls `POST /appointments/{id}/reject` with reason body
- [ ] Appointment status changes to Rejected; external user sees rejection note
- [ ] "Send Back" action is available (Phase 19b: after `SendBackAppointmentModalComponent` is created)
- [ ] Send-back modal captures a note and flagged fields/sections
- [ ] After send-back, appointment moves to AwaitingMoreInfo; external user sees flagged banner
- [ ] External user edits flagged fields and submits via "Save & Resubmit"
- [ ] Resubmitted appointment returns to staff queue (Pending or separate AwaitingReview status)
- [ ] Non-internal-user roles cannot access the action dropdown (403 or hidden)
- [ ] Responsible User field on approval is handled (Exception 1 -- verify with Adrian)
- [ ] Internal cancellation path is functional (Exception 4 -- verify with Adrian)
