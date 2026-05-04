---
feature: staff-supervisor-change-request-approval
date: 2026-05-04
phase: 17-frontend (backend endpoints pending)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointment-change-requests/
old-components:
  - list/ (reschedule requests page)
  - detail/ (cancel requests page)
  - edit/ (approve/reject modal -- shared)
  - view/ (JAL document rejection modal)
  - domain/appointment-change-request.domain.ts (shared base class)
new-feature-path: angular/src/app/appointments/change-requests/
shell: internal-user-authenticated (top-bar + left side-nav)
screenshots: pending (OLD server on port 4201; capture deferred to batch pass)
---

# Design: Staff Supervisor -- Change Request Approval (Cancel + Reschedule)

## 1. Routes

Two dedicated full-page routes. Both are internal-shell pages (side-nav visible).

| Page | OLD URL | NEW URL (proposed) | OLD component |
|---|---|---|---|
| Reschedule requests | `/appointment-rescheduled-requests` | `/appointments/change-requests/reschedule` | `AppointmentChangeRequestListComponent` |
| Cancel requests | `/appointment-cancel-requests` | `/appointments/change-requests/cancel` | `AppointmentChangeRequestDetailComponent` |

**Modal routes:** No dedicated routes. Three modals are launched imperatively
from the pages above.

| Modal | Launch from | OLD component |
|---|---|---|
| Approve/Reject (all request types) | Action buttons on either list | `AppointmentChangeRequestEditComponent` |
| JAL Document Reject | Reject icon in document sub-row | `AppointmentChangeRequestViewComponent` |
| JAL Document Approve | Inline confirmation dialog | `RxDialog.confirmation()` |

**Backend endpoints (all pending Phase 17 -- not yet built):**
- `GET api/app/appointment-change-requests?statusTypeId=12&...` (reschedule list)
- `GET api/app/appointment-change-requests?statusTypeId=13&...` (cancel list)
- `POST api/app/appointment-change-requests/{id}/approve-cancellation`
- `POST api/app/appointment-change-requests/{id}/reject-cancellation`
- `POST api/app/appointment-change-requests/{id}/approve-reschedule`
- `POST api/app/appointment-change-requests/{id}/reject-reschedule`
- `PATCH api/app/appointment-change-request-documents/{docId}/approve`
- `PATCH api/app/appointment-change-request-documents/{docId}/reject`

## 2. Shell

Internal-user authenticated shell: top navigation bar + left side-nav (collapsed or
expanded). Navigation item "Change Requests" with two sub-items:
"Reschedule Requests" and "Cancel Requests".

Access is gated to `StaffSupervisor` and `ITAdmin` roles only (see Section 8).

## 3. Page Layouts

### 3a. Reschedule Requests Page

```
+------------------------------------------------------------------+
| [H2] Reschedule Requests                                        |
|   [Toggle] Reschedule Requests till Date  [Search] [Clear icon] |
+------------------------------------------------------------------+
| [main data table -- server-side paged, sortable]                |
|                                                                  |
| Patient | Gender | Type | Req'd On | Conf # | Exist Date/Time   |
| Req'd Date/Time | Reason | Reschedule Status | Req'd By          |
| Beyond Limit? | DOB | SSN | Claim # | DOI | Action             |
|                                                                  |
| [expandable detail row: JAL documents sub-table]                |
|   Doc Name | Status | Download | Approve | Reject | Notes | By  |
+------------------------------------------------------------------+
```

**Toggle ("Reschedule Requests till Date"):** boolean switcher; when ON, fetches
ALL historical requests (`rescheduleRequestTillDate=1`); when OFF (default),
fetches only current/recent pending (`rescheduleRequestTillDate=0`).

**Search:** text input (Enter key triggers); clear resets to empty (space trick
in OLD -- see Exception 1).

OLD source: `list/appointment-change-request-list.component.html:1-159`

### 3b. Cancel Requests Page

```
+------------------------------------------------------------------+
| [H2] Cancel Requests                                            |
|   [Toggle] Cancellation Requests till Date  [Search] [Clear]   |
+------------------------------------------------------------------+
| [main data table]                                               |
|                                                                  |
| Patient | Gender | Type | DOB | SSN | Claim # | DOI | Req'd On  |
| Conf # | Appt Date/Time | Cancel Reason | Status | Req'd By    |
| Action                                                          |
+------------------------------------------------------------------+
```

Same toggle/search pattern as reschedule page. Default page size from
`PaginationSettingEnums.RowCount`.

OLD source: `detail/appointment-change-request-detail.component.html:1-96`

## 4. Table Columns

### 4a. Reschedule Requests Table

| Column | OLD field name | Notes |
|---|---|---|
| Patient | `PatientName` | `columnClass="text-uppercase"` |
| Gender | `gender` | |
| Type | `AppointmentTypeName` | |
| Requested On | `RequestOn` | |
| Confirmation # | `AppoinmentRequestConfirmationNumber` | Clickable link; navigates to `/appointments/{AppointmentId}` |
| Existing Date & Time | `OldAppointmentDateTime` | |
| Requested Date & Time | `NewAppointmentDateTime` | |
| Reason | `Reason` | `columnClass="ellipsis"` (truncated with tooltip) |
| Reschedule Status | `RequestStatus` | Title typo in OLD: "Reschecule Status" -> fix to "Reschedule Status" in NEW (Exception 2) |
| Requested By | `UserName` | |
| Beyond Limit? | `isBeyodLimit` | Color-coded: "Yes" label with `.apporved` class (green), "No" with `.rejected` class (red) |
| Date Of Birth | `dateOfBirth` | Sortable |
| Social Security Number | `socialSecurityNumber` | Sortable |
| Claim # | `claimNumberList` | Multi-value; each item on its own line (comma-split) |
| Date Of Injury | `dateOfInjuryList` | Multi-value; each item on its own line (comma-split) |
| Action | -- | Approve/reject icons; visibility rules in Section 4c |

OLD source: `list/appointment-change-request-list.component.html:71-150`

### 4b. Cancel Requests Table

| Column | OLD field name | Notes |
|---|---|---|
| Patient Name | `PatientName` | `columnClass="text-uppercase"` |
| Gender | `gender` | |
| Type | `AppointmentTypeName` | |
| Date Of Birth | `dateOfBirth` | Sortable |
| Social Security Number | `socialSecurityNumber` | Sortable |
| Claim # | `claimNumberList` | Multi-value |
| Date Of Injury | `dateOfInjuryList` | Multi-value |
| Requested On | `RequestOn` | |
| Confirmation # | `AppoinmentRequestConfirmationNumber` | Clickable link |
| Appointment Date & Time | `OldAppointmentDateTime` | |
| Cancellation Reason | `Reason` | `columnClass="ellipsis"` |
| Request Status | `RequestStatus` | |
| Requested By | `UserName` | |
| Action | -- | Approve icon only (see Exception 3) |

OLD source: `detail/appointment-change-request-detail.component.html:34-84`

### 4c. Action Column Visibility Rules

**Reschedule action column** (both approve + reject icons):
```
if (showButtonBaseOnRole) {
  // Beyond-limit path: JAL must be Accepted before supervisor can act
  if (event.documentStatusId == Accepted && event.isBeyodLimitId) {
    show approve + reject  [only if event.RequestStatusId == Pending]
  }
  // Normal path: no doc required, act immediately on Pending
  if (!event.isBeyodLimitId) {
    show approve + reject  [only if event.RequestStatusId == Pending]
  }
  // Implicit: isBeyodLimitId=true AND doc not Accepted -> no action icons shown
}
```

OLD source: `list/appointment-change-request-list.component.html:124-150`

**Cancel action column** (approve icon only, NO reject):
```
if (showButtonBaseOnRole) {
  show approve  [only if event.RequestStatusId == Pending]
}
// Reject button is commented out in OLD HTML (line 81)
```

`showButtonBaseOnRole` is `true` only for `StaffSupervisor` or `ITAdmin`.
OLD source: `domain/appointment-change-request.domain.ts:35-38`

### 4d. JAL Document Sub-Table (Reschedule page only)

Each reschedule row expands to show its `AppointmentChangeRequestDocuments` in a
nested sub-table. The sub-table shows for ALL reschedule rows regardless of
`isBeyodLimitId` -- but documents only exist if the request was submitted with
`isBeyodLimit=Yes`.

| Column | Field / Behavior |
|---|---|
| Document Name | `documentName` |
| Status | `documentStatusName` |
| Joint Agreement Letter | Download link (`fa-download`) -> `api/DocumentDownload/DownloadFile?filePath=...` (replace in NEW -- see Section 10, item 5) |
| Approve | Checkmark icon (`fas fa-check`); only shown if `documentStatusId == Uploaded` |
| Reject | X icon (`fa fa-times`); only shown if `documentStatusId == Uploaded` |
| Rejection Notes | `rejectionNotes` |
| Reject By | `rejectedByUserName` |

Approve action: inline `RxDialog.confirmation()` -> calls PATCH to set
`documentStatusId = Accepted`. Toast: "Joint Agreement Letter Accepted".
Reject action: launches `AppointmentChangeRequestViewComponent` modal.

OLD source: `list/appointment-change-request-list.component.html:37-70`

## 5. Approve/Reject Modal (`AppointmentChangeRequestEditComponent`)

Shared modal for all four operations: cancel-approve, cancel-reject,
reschedule-approve, reschedule-reject. The content is dynamically determined
by `operationType (1=approve, 2=reject)` and `appointmentStatusId` of the
change request.

Modal class: `modal-dialog modal-lg`.

### 5a. Header content (from `setModelContent`)

| Request type | operationType | Header text | Subtitle |
|---|---|---|---|
| Cancel | 1 | "Approve Cancellation  Request" (see Exception 4) | "Please approve cancellation request from here." |
| Cancel | 2 | "Reject Cancellation  Request" (see Exception 4) | "Please reject cancellation request from here." |
| Reschedule | 1 | "Approve Reschedule request" | "Please approve Reschedule request from here." |
| Reschedule | 2 | "Reject Reschedule request" | "Please reject Reschedule request from here." |

OLD source: `domain/appointment-change-request.domain.ts:40-76`

### 5b. Cancel-Approve Body (`cancelRequest=true, operationType=1`)

Read-only fields:
- "Appointment Date Time": `appointmentChangeRequest.OldAppointmentDateTime`
- "Reason for Cancellation": `appointmentChange.cancellationReason`

Form field:
- **Appointment Status radio (required):**
  - "Cancelled-No Bill" (`AppointmentStatusTypeEnums.CancelledNoBill`)
  - "Cancelled-Late" (`AppointmentStatusTypeEnums.CancelledLate`)
  - Control: `appointmentStatusId`

Button: "Approve" (`btn btn-primary`)

OLD source: `edit/appointment-change-request-edit.component.html:13-56`

### 5c. Cancel-Reject Body (`cancelRequest=true, operationType=2`)

Read-only fields:
- "Appointment Date Time": same as above
- "Reason for Cancellation": same as above

Form field:
- **Notes textarea (required):**
  - Label: "Notes"
  - Control: `cancellationRejectionReason`
  - rows: 3

Button: "Reject" (`btn btn-primary btn-danger` -- danger style applied via
`[class.btn-danger]="operationType == 2"`)

OLD source: `edit/appointment-change-request-edit.component.html:30-55`

### 5d. Reschedule-Approve Body (`rescheduleRequest=true, operationType=1`)

Read-only display fields:
- "Existing Appointment Date & Time": `appointmentChangeRequest.OldAppointmentDateTime`
- "Requested Appointment Date & Time": `appointmentChangeRequest.NewAppointmentDateTime`
- "Reschedule reason": `appointmentChangeRequest.ReScheduleReason` (text display,
  no control)

Form fields:
1. **Appointment Status radio (required):**
   - "Rescheduled-No Bill" (`RescheduledNoBill`)
   - "Rescheduled-Late" (`RescheduledLate`)
   - Control: `appointmentStatusId`

2. **"Change Re-Schedule Date & Time" checkbox** (`isChangeInReschedule`):
   - Default: unchecked (`isChanged = false`)
   - When checked: reveals date picker + time select + reason field;
     `adminReScheduleReason` becomes required
   - When unchecked: all three fields reset to null, validators cleared

   (shown only when `isChanged=true`):

3. **New Appointment Date picker** (`newAppointmentDate`, required when `isChanged`):
   - Same `<rx-date>` pattern as reschedule submit (3-month, available dates
     whitelist from location API, no past dates)
   - Tooltip: "Please select 'Re-Schedule' date"

4. **New Appointment Time select** (`doctorAvailabilityId`, required when date
   picked and slots exist):
   - `<select>` bound to `doctorsAvailabilitiesLookUps`

5. **Re-schedule Change Reason textarea** (`adminReScheduleReason`, required when
   `isChanged`):
   - Label: "Re-schedule Change Reason"
   - rows: 3

OLD source: `edit/appointment-change-request-edit.component.html:58-148`

### 5e. Reschedule-Reject Body (`rescheduleRequest=true, operationType=2`)

Read-only display fields:
- "Existing Appointment Date & Time"
- "Requested Appointment Date & Time"

Form field:
- **Re-Schedule Rejection Reason textarea (required):**
  - Control: `reScheduleRejectionReason`
  - rows: 3

Button: "Reject" (danger style)

OLD source: `edit/appointment-change-request-edit.component.html:78-81`

### 5f. Submit logic and backend payload

**Cancel approve:**
- `requestStatusId = Accepted`
- `appointmentStatusId = (selected CancelledNoBill or CancelledLate)`
- `appointmentId = appointmentChangeRequest.AppointmentId`

**Cancel reject:**
- `requestStatusId = Rejected`
- `appointmentStatusId = Approved` (reverts appointment to Approved)
- `cancellationRejectionReason = (entered reason)`
- `appointmentId`

**Reschedule approve, no date override (adminReScheduleReason is null):**
- `requestStatusId = Accepted`
- `appointmentStatusId = (selected RescheduledNoBill or RescheduledLate)`
- `doctorAvailabilityId = appointmentChangeRequest.NewDoctorsAvailabilityId`
- `oldDoctorAvailabilityId = appointmentChangeRequest.OldDoctorsAvailabilityId`
- `requestedDoctorAvailabilityId = 0`
- `appointmentId`

**Reschedule approve WITH date override by supervisor (adminReScheduleReason filled):**
- `requestStatusId = Accepted`
- `appointmentStatusId = (selected)`
- `doctorAvailabilityId = (form-selected supervisor slot)`
- `requestedDoctorAvailabilityId = appointmentChangeRequest.NewDoctorsAvailabilityId`
  (preserves user's original request for audit)
- `oldDoctorAvailabilityId = appointmentChangeRequest.OldDoctorsAvailabilityId`
- `adminReScheduleReason = (entered reason)`
- `appointmentId`

**Reschedule reject:**
- `requestStatusId = Rejected`
- `appointmentStatusId = Approved`
- `doctorAvailabilityId = NewDoctorsAvailabilityId`
- `oldDoctorAvailabilityId = OldDoctorsAvailabilityId`
- `reScheduleRejectionReason = (entered reason)`
- `appointmentId`

OLD source: `edit/appointment-change-request-edit.component.ts:110-199`

**Toast messages on success:**
- Approve: "Your request has been approved successfully"
- Reject: "Your request has been rejected successfully"

OLD source: `edit/appointment-change-request-edit.component.ts:227-238`

## 6. JAL Document Reject Modal (`AppointmentChangeRequestViewComponent`)

Header: "Reject Document" + subtitle "Please reject document from here."

Body (one per document in the FormArray):
- "Document Name": `input[type="text"]`, `readonly`, bound to `documentName`
- "Rejection Reason": `textarea`, rows=3, control `rejectionNotes`,
  required, `maxLength(500)`

Footer:
- "Reject" button: `btn btn-danger`, `[disabled]="!appointmentChangeRequestFormGroup.valid"`

On submit: PATCHes the change request record with:
- `isDocumentUpdate = true`
- All documents patched with `documentStatusId = Rejected`, `rejectedById`,
  `modifiedById`, `modifiedDate`

Toast: "Joint Agreement Letter Rejected"
Close: `closePopup()` -- emits result `{ appointmentChangeRequests }` to parent;
parent calls `ngOnInit()` to refresh list.

OLD source: `view/appointment-change-request-view.component.html:1-35`
TS source: `view/appointment-change-request-view.component.ts:63-70`

## 7. Buttons

| Button | Context | OLD class | Disabled condition |
|---|---|---|---|
| Approve (row icon) | Reschedule / cancel list | `.apporve` (typo -- do NOT rename, CSS class) + `fa fa-check` | `event.RequestStatusId != Pending` |
| Reject (row icon) | Reschedule list only | `.reject` + `fa fa-times` | `event.RequestStatusId != Pending` |
| Approve doc (sub-row) | JAL sub-table | `.apporve` + `fas fa-check` | `event.documentStatusId != Uploaded` |
| Reject doc (sub-row) | JAL sub-table | `.reject` + `fa fa-times` | `event.documentStatusId != Uploaded` |
| Save (edit modal) | All 4 approve/reject flows | `btn btn-primary [class.btn-danger]="operationType==2"` | `!appointmentChangeRequestFormGroup.valid` |
| Close (edit modal) | All flows | `btn btn-secondary` | never |
| Reject (view modal) | JAL doc rejection | `btn btn-danger` | `!appointmentChangeRequestFormGroup.valid` |

Note: `apporve` is an OLD CSS class name typo. Do NOT rename it in NEW -- use
`app-approved` or replicate with the same intent in NEW styles.

## 8. Role Visibility Matrix

| Role | Reschedule page | Cancel page | Approve/reject action | JAL doc actions |
|---|---|---|---|---|
| IT Admin | Yes | Yes | Yes (showButtonBaseOnRole=true) | Yes |
| Staff Supervisor | Yes | Yes | Yes (showButtonBaseOnRole=true) | Yes |
| Clinic Staff | No (no route access) | No | No | No |
| External users | No | No | No | No |

Permission module IDs from OLD routing: `rootModuleId: 33, applicationModuleId: 6`.
NEW permission keys: `CaseEvaluation.AppointmentChangeRequests.Default`,
`CaseEvaluation.AppointmentChangeRequests.Approve`,
`CaseEvaluation.AppointmentChangeRequests.Reject`.

OLD source: `domain/appointment-change-request.domain.ts:35-38`

## 9. Branding Tokens

All four modal headers should use `--brand-primary` background with white text
(same pattern as all other modals -- see Section 11, Exception 5 for OLD
inconsistency).

| Element | Token |
|---|---|
| All modal header backgrounds | `--brand-primary` |
| All modal header text | `--brand-primary-text` (`#ffffff`) |
| Approve action icons / buttons | color via `.apporve` class -> `var(--brand-success)` or `#28a745` |
| Reject action icons / buttons | color via `.reject` class -> `var(--brand-danger)` or `#dc3545` |
| Reject buttons (`btn-danger`) | Bootstrap `#dc3545` |

## 10. NEW Stack Delta

1. **Two separate page components:** `RescheduleRequestsPageComponent` and
   `CancelRequestsPageComponent`. Do NOT combine into a single page with tab
   switch -- OLD has them as separate routes, and the column sets differ.

2. **Table:** Replace OLD `<rx-table>` with Angular Material `mat-table` + CDK
   `DataSource` for server-side pagination and sorting. Expandable row pattern
   (`mat-row detail`) for the JAL sub-table on the reschedule page.

3. **Approve/Reject modal:** Implement as `<app-change-request-action-modal>`
   with inputs:
   - `changeRequest: AppointmentChangeRequestDto`
   - `operationType: 'approve' | 'reject'`
   The modal branch (cancel vs reschedule body) is driven by `changeRequest.statusId`.

4. **JAL document reject modal:** `<app-jal-document-reject-modal>` with input
   `document: AppointmentChangeRequestDocumentDto`.

5. **JAL download:** Replace `api/DocumentDownload/DownloadFile?filePath=...` with
   `GET api/app/appointment-change-request-documents/{docId}/download` (no
   file path exposure to client). Same pattern as reschedule design doc Exception 9.

6. **Date picker and time select (supervisor override):** Same widget replacement
   as in `external-user-appointment-rescheduling-design.md` -- `mat-datepicker`
   with date filter + `ng-select` for time slots.

7. **Backend Phase 17 dependency:** All approve/reject endpoints are pending.
   Build UI against stub services that return 200/202; wire real endpoints when
   Phase 17 ships.

8. **Search clear:** Replace the `searchQuery = " "` space-trick (Exception 1) with
   explicit empty-string reset + re-query.

## 11. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Search clear `searchAppointmentClear()` | Sets `searchQuery = " "` (single space) then calls `bindGrid()` | Set `searchQuery = ""` and trigger query | Clear bug: space in a search field is not the same as empty; server may treat it differently |
| 2 | Column title "Reschecule Status" | Typo in column header | "Reschedule Status" | Cosmetic typo fix; no data impact |
| 3 | Cancel page action: no Reject button | `rejectAppointmentRequest()` exists in TS but the Reject icon is commented out in HTML | Include Reject action in NEW (uncomment + wire up) | The backend supports cancel-reject (status reverts to Approved, reason saved). The missing UI button appears to be an oversight. Flag: PARITY-FLAG if Adrian confirms rejection of cancellations is out of scope |
| 4 | Approve/reject modal header double space | "Approve Cancellation  Request" and "Reject Cancellation  Request" | Single space: "Approve Cancellation Request" | Cosmetic fix; double space is a string literal in `setModelContent()` |
| 5 | Edit modal header `text-white` inconsistency | h6 has `text-white`, h4 and close button do NOT (unlike add component which gives all three `text-white`) | Apply consistent `--brand-primary-text` to all three header elements | Likely oversight in OLD; the header background requires white text on all elements |
| 6 | `appointmenTime` typo in time select option | `item.appointmenTime` (missing 't') | `item.appointmentTime` | Spelling fix at DTO level; same as reschedule submit design exception |
| 7 | `appoinmentRequestConfirmationNumber` column field | `AppoinmentRequestConfirmationNumber` (capital A, missing 'e') | Use lowercase camelCase DTO: `appointmentRequestConfirmationNumber` | OLD uses PascalCase view model fields; NEW uses ABP conventions |
| 8 | JAL download link uses raw storage path | `api/DocumentDownload/DownloadFile?filePath=<encoded>` exposes storage path | Secure endpoint `/api/app/appointment-change-request-documents/{id}/download` | Security: clients must not receive direct storage paths |
| 9 | `showToastMessages()` calls `closePopup()` twice | One implicit call from `editAppointmentChangeRequest()` + one inside `showToastMessages()` (line 237) -- popup hides twice | Remove the redundant `closePopup()` inside `showToastMessages()` | Double-close is a silent no-op in OLD but represents a bug; clean up in NEW |

## 12. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `list/appointment-change-request-list.component.html` | 1-25 | Page header, toggle, search |
| `list/appointment-change-request-list.component.html` | 34-159 | Main table + sub-table + action column |
| `list/appointment-change-request-list.component.ts` | 43-44 | `statusTypeId = RescheduleRequested, requestStatusId = Pending` |
| `list/appointment-change-request-list.component.ts` | 79-195 | Approve/reject + doc approve/reject + download |
| `detail/appointment-change-request-detail.component.html` | 1-96 | Full cancel requests page |
| `detail/appointment-change-request-detail.component.ts` | 33-34 | `statusTypeId = CancellationRequested` |
| `detail/appointment-change-request-detail.component.ts` | 76-82 | Approve/reject methods |
| `edit/appointment-change-request-edit.component.html` | 1-155 | Full approve/reject modal |
| `edit/appointment-change-request-edit.component.ts` | 64-103 | `ngOnInit()` -- validator setup per type |
| `edit/appointment-change-request-edit.component.ts` | 110-200 | `updateAppointmentRequest()` -- all 4 submit paths |
| `edit/appointment-change-request-edit.component.ts` | 227-238 | Toast messages |
| `edit/appointment-change-request-edit.component.ts` | 252-309 | Date + time slot loading for supervisor override |
| `view/appointment-change-request-view.component.html` | 1-35 | JAL reject modal |
| `view/appointment-change-request-view.component.ts` | 45-70 | Init + rejectDocument() |
| `domain/appointment-change-request.domain.ts` | 30-82 | `setModelContent()`, role-based show flags |
| `appointment-change-requests.routing.ts` | 1-14 | Reschedule route + module |
| `detail/appointment-change-request-detail.routing.ts` | 1-12 | Cancel route |
| `start/app.lazy.routing.ts` | 72-78 | Full URL paths |
| `docs/parity/staff-supervisor-change-request-approval.md` | all | Full parity audit (gap table, backend flows, ABP wiring) |

## 13. Verification Checklist

**Reschedule requests page:**
- [ ] Page loads with pending reschedule requests (`statusTypeId = RescheduleRequested`)
- [ ] Columns: Patient, Gender, Type, Requested On, Conf #, Existing Date, Requested
      Date, Reason, Reschedule Status, Requested By, Beyond Limit?, DOB, SSN,
      Claim #, Date of Injury, Action
- [ ] Toggle "Reschedule Requests till Date" fetches all historical requests when ON
- [ ] Search by patient name or conf # works; clear resets the list
- [ ] Conf # is a link that navigates to the appointment view
- [ ] "Beyond Limit?" shows green "Yes" / red "No" labels
- [ ] Claim # and Date of Injury render one value per line for multi-value rows
- [ ] Row expands to show JAL sub-table (documents sub-table)
- [ ] JAL sub-table shows download link, approve/reject icons only when status = Uploaded
- [ ] JAL approve: inline confirmation dialog -> sets doc to Accepted ->
      toast "Joint Agreement Letter Accepted" -> list refreshes
- [ ] JAL reject: launches reject modal -> shows doc name + rejection notes field ->
      Save disabled until notes entered -> sets doc to Rejected ->
      toast "Joint Agreement Letter Rejected" -> list refreshes
- [ ] Action column (approve/reject) is only visible to StaffSupervisor / ITAdmin
- [ ] Beyond-limit rows: approve/reject only available AFTER JAL is Accepted
- [ ] Normal rows: approve/reject available immediately when Pending
- [ ] Action icons only shown for Pending requests; non-Pending rows show no icons

**Cancel requests page:**
- [ ] Page loads with pending cancel requests (`statusTypeId = CancellationRequested`)
- [ ] Columns: Patient, Gender, Type, DOB, SSN, Claim #, DOI, Requested On,
      Conf #, Appt Date/Time, Cancellation Reason, Request Status, Requested By, Action
- [ ] Approve icon shown for Pending requests; no Reject icon shown
- [ ] Toggle + search work same as reschedule page

**Approve/Reject modal -- cancel-approve:**
- [ ] Header: "Approve Cancellation Request" + subtitle
- [ ] Shows appointment date/time and cancellation reason (read-only)
- [ ] Radio: Cancelled-No Bill / Cancelled-Late; Save disabled until one is selected
- [ ] Submit: sets appointment status to selected value, request status = Accepted
- [ ] Toast: "Your request has been approved successfully"
- [ ] List refreshes after modal closes

**Approve/Reject modal -- cancel-reject:**
- [ ] Header: "Reject Cancellation Request" + subtitle
- [ ] Notes textarea required; Save disabled until filled
- [ ] Submit: appointment status reverts to Approved, request status = Rejected
- [ ] Toast: "Your request has been rejected successfully"

**Approve/Reject modal -- reschedule-approve (no override):**
- [ ] Header: "Approve Reschedule request" + subtitle
- [ ] Shows existing date, requested date, reschedule reason (read-only)
- [ ] Radio: Rescheduled-No Bill / Rescheduled-Late; required
- [ ] "Change Re-Schedule Date & Time" checkbox unchecked by default
- [ ] Submit without checking override: creates NEW appointment row with same
      confirmation #, new slot Booked, old slot Available, stakeholders notified

**Approve/Reject modal -- reschedule-approve (with supervisor override):**
- [ ] Checking "Change Re-Schedule Date & Time" reveals date picker + time select +
      reason field; all three become required
- [ ] Unchecking hides and resets all three
- [ ] Selecting a date with no slots shows "All slots booked" toast; time remains hidden
- [ ] Saving with override: supervisor's slot used instead of user's requested slot;
      `adminReScheduleReason` saved; `requestedDoctorAvailabilityId` preserves original request

**Approve/Reject modal -- reschedule-reject:**
- [ ] Rejection reason required; button styled as danger (red)
- [ ] Appointment reverts to Approved; new slot Released (Reserved -> Available)

**General modals:**
- [ ] Modal header uses `--brand-primary` background, white text
- [ ] Close (X) button works on all modals
- [ ] Approve button is blue (btn-primary); Reject button is red (btn-danger)
