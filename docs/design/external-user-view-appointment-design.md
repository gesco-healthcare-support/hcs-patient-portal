---
feature: external-user-view-appointment
date: 2026-05-04
phase: 2-frontend (backend appointment read/update endpoints done; Angular UI exists at 1604 LOC)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointments/
old-components:
  - my-appointments/my-appointment-list.component.ts + .html (external user list, 222/206 lines)
  - edit/appointment-edit.component.ts + .html (shared external+internal view/edit, 1009 lines)
new-feature-path: angular/src/app/appointments/appointment/components/appointment-view.component.ts + .html (1604/1035 lines)
shell: external-user-authenticated (top-bar + content; no side-nav)
screenshots: pending
---

# Design: External User -- View Appointment

## Overview

Two surfaces covered by this doc:

1. **My Appointments list** (`/appointments`) -- External user's paginated list of all their
   appointments with filters. Clicking a Confirmation # navigates to the view page.

2. **Appointment View/Detail** (`/appointments/view/:id`) -- Full read/view/edit page for
   a single appointment. Role and status drive which fields are editable and which
   action buttons are visible.

In OLD, the view/detail page was the `AppointmentEditComponent` (shared by all roles
via `showActionButtonForInternalUser` vs `showActionButtonForExternalUser` conditionals).
In NEW, it is a dedicated `appointment-view.component` (1604 lines) with per-field
`canEdit()` locking and a new AwaitingMoreInfo/send-back flow absent in OLD.

---

## 1. Routes

| | OLD | NEW |
|---|---|---|
| External user list | `/home` (patient home redirects to list) | `/appointments` |
| Appointment view/edit | `/appointments/:appointmentId` (AppointmentEditModule -- no guard) | `/appointments/view/:id` |
| Change log | n/a | `/appointments/view/:id/change-log` |

Guards:
- OLD: `canActivate: [PageAccess]` `applicationModuleId: 6` on the list; NO guard on the
  edit/view route (lazy-loaded module without `canActivate`).
- NEW: `canActivate: [authGuard, permissionGuard]`; external users need
  `CaseEvaluation.Appointments.Default`.

OLD source: `appointments/appointments.routing.ts` (list + edit parent routes),
`edit/appointment-edit.routing.ts` (no guard confirmed).

---

## 2. Shell

External-user authenticated shell: top-bar only, no side-nav.
Same shell as the booking form (`external-user-appointment-request-design.md`).

Internal users reaching the same view page use the internal-user shell (side-nav + top-bar);
the view component is shared -- the shell switches per role.

---

## 3. My Appointments List

### 3a. Layout

```
+-------------------------------------------------------+
| [H2] My Appointments                                 |
| [Advanced Search -- collapsible accordion card]      |
|   Appointment Type [select]  Confirmation No. [text] |
|   Location [select]          Status [select]         |
|   Claim # [text]             Date Of Injury [date]   |
|   Date Of Birth [date]*      SSN [masked]            |
|   (* hidden for Patient role)                        |
|   [Search]  [Reset]                                  |
+-------------------------------------------------------+
| [Table rx-table]                                     |
| Type | Patient Name | Gender | Conf # |              |
| Appt Date | SSN | Claim # | DOI |                   |
| Location | Status | Action                           |
+-------------------------------------------------------+
| [Pagination]                                         |
+-------------------------------------------------------+
```

OLD source: `my-appointments/my-appointment-list.component.html:1-206`

### 3b. Filters (Advanced Search accordion)

| Filter | Field | Visibility |
|---|---|---|
| Appointment Type | dropdown lookup | all external roles |
| Confirmation Number | text | all |
| Location | dropdown lookup | all |
| Appointment Status | dropdown lookup | all |
| Claim # | text | all |
| Date Of Injury | date picker | all |
| Date Of Birth | date picker | `*ngIf="!isUserIsPatient"` -- hidden for Patient, shown for Adjuster/Attorney |
| Social Security # | masked input | all |

Buttons: **Search**, **Reset**, **Sync** (refresh icon).

OLD source: `my-appointments/my-appointment-list.component.html:30-75`

### 3c. Table Columns

| Column | OLD field | Notes |
|---|---|---|
| Type | `appointmentTypeName` | |
| Patient Name | `patientName` | uppercase CSS |
| Gender | `genderName` | |
| Confirmation # | `appointmentConfirmNumber` | clickable link -- navigates to view/edit page |
| Appointment Date | `appointmentDate` | |
| SSN | `socialSecurityNumber` | masked display |
| Claim # | `claimNumber` | |
| Date Of Injury | `dateOfInjury` | |
| Location | `locationName` | |
| Status | `appointmentStatusId` | Color-coded badge (see 3d) |
| Action | -- | "Document Manager" button -- navigates to document upload page |

OLD source: `my-appointments/my-appointment-list.component.html:80-175`

### 3d. Status Badge Colors

| Status | CSS class | NEW token |
|---|---|---|
| Pending | `lblPending` | `--status-pending` (yellow) |
| Approved | `lblApprove` | `--status-approved` (green) |
| Rejected | `lblReject` | `--status-rejected` (red) |
| No Show | `lblNoShow` | `--status-noshow` (orange) |
| Cancelled NoBill | `lblCancel` | `--status-cancelled` (grey) |
| Cancelled Late | `lblCancel` | `--status-cancelled` (grey) |
| Rescheduled NoBill | `lblReschedule` | `--status-rescheduled` (blue) |
| Rescheduled Late | `lblReschedule` | `--status-rescheduled` (blue) |
| Checked In | `lblCheckIn` | `--status-checked-in` (teal) |
| Checked Out | `lblCheckOut` | `--status-checked-out` (dark) |
| Billed | `lblBilled` | `--status-billed` (purple) |
| Reschedule Requested | `lblRescheduleReq` | `--status-rescheduled` |
| Cancellation Requested | `lblCancelReq` | `--status-cancelled` |

OLD source: `my-appointments/my-appointment-list.component.html:97-145`
Token definitions: `_design-tokens.md` (status colors section).

---

## 4. Appointment View Page -- Header

```
+---------------------------------------------------------------------+
| [H2] {AppointmentType} Appointment  [Status badge]                 |
|     > {FirstName} {LastName}                                        |
|                                          [Action buttons -- right] |
+---------------------------------------------------------------------+
```

The status badge appears inline with the heading. A second set of identical action
buttons is repeated at the very bottom of the 1009-line form for scroll convenience.

OLD source: `edit/appointment-edit.component.html:1-8` (heading),
`edit/appointment-edit.component.html:969-1007` (bottom button repeat).

---

## 5. Action Buttons -- External User

Condition: `showActionButtonForExternalUser`

| Button | Condition | Action |
|---|---|---|
| Save | `!isView` | `editAppointment()` -- save form changes |
| Re-schedule Appointment | `approveState && !isCancelRescheduleRequestPending` | `onCancelAppoinment(reschedule)` -- opens reschedule-request modal |
| Cancel Appointment | `approveState && !isCancelRescheduleRequestPending` | `onCancelAppoinment(cancelled)` -- opens cancellation modal |
| Upload Documents | `!isRescheduleRequestUploadDocumentReasonShow` | `uploadDocuments()` |
| Upload Documents (reschedule doc) | `isRescheduleRequestUploadDocumentReasonShow` | `uploadRescheduleRequestDocument()` |
| Re-Request | `currentStatus == Rejected && createdById == loginUserId` | `reApplyAppointmentRequest()` -- rebooking from rejected state |
| Help | always | `onAddQuery()` -- opens submit-query modal |
| Back | always | `routerLink="/home"` |

**Key rule:** Re-schedule and Cancel are mutually exclusive with a pending change request.
Once a request is pending (`isCancelRescheduleRequestPending = true`), both buttons hide
until the request is resolved. Only one pending change request at a time.

OLD source: `edit/appointment-edit.component.html:20-65`

---

## 6. Action Buttons -- Internal User (for completeness)

Condition: `showActionButtonForInternalUser`

| Button | Condition |
|---|---|
| Save | `!isView` |
| Approve Request | `pendingState` |
| Reject Request | `pendingState` |
| Cancel Appointment | `pendingState && !isView` |
| Upload Documents | always |

Repeated identically at the bottom of the page. This design doc covers the external-user
surface; internal-user approval buttons are documented in
`clinic-staff-appointment-approval-design.md`.

OLD source: `edit/appointment-edit.component.html:9-19, 969-1007`

---

## 7. Section 1 -- Appointment Details

All fields read-only (disabled) for external users in standard view.

```
+---------------------------------------------+
| Appointment Details                         |
| Status: [badge]                             |
| [Reschedule reason banner -- if pending]    |
+---------------------------------------------+
| Current Date & Time      [value]            |
| Requested Date & Time    [value -- if RescheduleRequested only] |
| Appointment Type         [value]            |
| Confirmation Number      [value]            |
| Panel Number             [value]            |
| Location                 [value]            |
| Appointment Date         [value]            |
| Appointment Time         [value]            |
+---------------------------------------------+
| [Rejection Note -- shown if status=Rejected]|
| [Admin Reschedule Reason -- if admin changed date] |
+---------------------------------------------+
```

Conditional banners within this section:
- Reschedule reason: `isCancelRescheduleRequestPending` -- explains why buttons are hidden
- Document status + rejection reason: when reschedule-request supporting doc was rejected
  by staff (shows "Document Status: Rejected" + rejection note)
- Rejection Note: shown when `appointmentStatusId == Rejected` (staff rejection note)
- Admin Rescheduled Appointment Reason: shown when staff unilaterally rescheduled

OLD source: `edit/appointment-edit.component.html:82-276`

---

## 8. Section 2 -- Patient Demographics

Fields are disabled in view mode; enabled only during re-apply (rejected appointment) or
for internal users with edit rights.

| Field | Control | Notes |
|---|---|---|
| Last Name | text | |
| First Name | text | |
| Middle Name | text | |
| Gender | radio | Male / Female / Non-Binary |
| Date of Birth | date picker | `rx-date` in OLD; Angular Material in NEW |
| Email | text | Always readonly for Patient role (identity-linked) |
| Cell Phone Number | masked | |
| Phone Number | masked | Secondary phone; shown conditionally |
| Phone Number Type | radio | Home / Work / Other -- shown when secondary phone is present |
| Social Security # | masked | |
| Street | text | |
| Unit # | text | `apptNumber` form control name |
| City | text | |
| State | select | Lookup |
| Zip | masked | |
| Language | select | Includes "Other" option |
| Other Language | text | `*ngIf="languageIsOther"` -- appears when Other selected |
| Do you need an interpreter? | radio | Yes / No |
| Interpreter Vendor Name | text | `*ngIf="needsInterpreter == Yes"` |
| Referred By | text | |

OLD source: `edit/appointment-edit.component.html:280-418`

---

## 9. Section 3 -- Employer Details

Repeating section (FormArray, one entry per employer). Same fields as in booking form:

| Field | Notes |
|---|---|
| Employer Name | |
| Occupation | |
| Phone Number | masked |
| Street | |
| City | |
| State | select (lookup) |
| Zip | masked |

Read-only for external users in view mode. For multi-employer appointments the array
renders one card per employer. See `external-user-appointment-request-design.md`
Exception 1 for multi-employer context.

OLD source: `edit/appointment-edit.component.html:420-460`

---

## 10. Section 4 -- Claim Information

Read-only table of all claims linked to the appointment. The Bootstrap `#myModal` inline
from the booking form is re-used in OLD's edit component for adding or editing claims.

### 10a. Claims Table (always visible)

| Column | Notes |
|---|---|
| Date Of Injury | Single date, or "From -- To" range if cumulative trauma |
| Claim # | |
| WCAB Office | |
| ADJ# | |
| Insurance Company | |
| Claim Examiner | |
| Action | Edit / Delete -- conditional on edit mode |

### 10b. Claim Modal (when adding/editing a claim)

Same fields as the booking form modal:
- Cumulative Trauma Injury (Yes/No radio)
- Date Of Injury / From Date + To Date (conditional)
- Claim Number, WCAB Office (dropdown), ADJ#, Body Parts (textarea)
- Insurance section toggle: Company Name, Attention, Phone, Fax, Street, STE, City, State, Zip
- Claim Examiner section toggle: Name, Email (readonly for Adjuster role), Phone, Fax,
  Street, STE, City, State, Zip

In view mode (non-editable status), the "Add" button and Edit/Delete action icons are hidden.

OLD source: `edit/appointment-edit.component.html:463-738`

---

## 11. Section 5 -- Additional Authorized Users

| Column | Notes |
|---|---|
| Name | Full name |
| Email | Login email |
| User Role | Role name |
| Rights | View (AccessType 23) or Edit (AccessType 24) |
| Action | Edit icon / Delete icon -- hidden in view mode |

**Add** button visible only in edit mode. Opens accessor modal (email select + access type select).
External users with View rights can see the section but not modify it.

OLD source: `edit/appointment-edit.component.html:740-796`

---

## 12. Section 6 -- Attorney Details

Two-column layout on desktop (stacked on mobile):

**Applicant Attorney** (left half):
- Include Applicant Attorney toggle (checkbox on/off)
- Name
- Email (readonly for Applicant Attorney role -- cannot change own email)
- Firm Name, Web Address
- Phone Number (masked), Fax (masked)
- Street, City, State (select), Zip

**Defense Attorney** (right half):
- Include Defense Attorney toggle (checkbox on/off)
- Name
- Email (readonly for Defense Attorney role -- cannot change own email)
- Firm Name, Web Address
- Phone Number (masked), Fax (masked)
- Street, City, State (select), Zip

The toggles show/hide the respective section's fields. Both are collapsed by default if
no attorney is assigned.

OLD source: `edit/appointment-edit.component.html:799-937`

NEW deviation: In NEW, attorney sections are loaded via email-search lookup (type email,
click "Load Applicant Attorney" to pull identity-user data). See Exception 5.

---

## 13. Section 7 -- Additional Details (Custom Fields)

Dynamic list of appointment-type-specific custom fields. Rendered from `customFields[]` array.

| Field type | Control |
|---|---|
| Text (CustomFieldTypeEnum=2) | text input |
| Number (CustomFieldTypeEnum=3) | number input |
| Date (CustomFieldTypeEnum=1) | date picker |

Section header "Additional Details" only appears when `isCustomeFileds` is true
(at least one active custom field exists for this appointment type).

OLD source: `edit/appointment-edit.component.html:939-965`

---

## 14. NEW-Only Sections (not in OLD)

### 14a. AwaitingMoreInfo Banner

When status = AwaitingMoreInfo and a latest unresolved send-back exists:

```
+---------------------------------------------+
| Fields Needing Review                       |
| Note: "{staffNote}"                         |
| Sections flagged: {sectionList}             |
| Fields flagged: {fieldList}  [needs-review] |
+---------------------------------------------+
```

Flagged fields display a `needs-review` highlight (left border in `--status-pending` color)
and remain editable. All other fields remain locked. "Save & Resubmit" replaces "Save"
in this status.

NEW source: `appointment-view.component.html` (banner block, `@if (isResubmitMode)`)

### 14b. Appointment Documents (embedded component)

`<app-appointment-documents>` embedded below custom fields. Renders the patient's
document upload/download interface inline (package documents + ad-hoc uploads).

### 14c. Appointment Packet (embedded component)

`<app-appointment-packet>` embedded after documents. Renders the package document list
for the appointment's assigned package.

### 14d. Change Log Navigation

A **"View Change Log"** button visible to users with `CaseEvaluation.AppointmentChangeLogs`
permission. Navigates to `/appointments/view/:id/change-log` sub-page.

---

## 15. Role Visibility Matrix

| Role | List access | View page -- fields | Action buttons |
|---|---|---|---|
| Patient | Own appointments | Read-only (all sections) | Re-schedule, Cancel (if Approved + no pending), Re-Request (if Rejected), Upload Docs, Help, Back |
| Adjuster / Claim Examiner | Own appointments | Same as Patient; DOB filter shown in list | Same as Patient |
| Applicant Attorney | Own appointments | Same; own email always readonly | Same as Patient |
| Defense Attorney | Own appointments | Same; own email always readonly | Same as Patient |
| Clinic Staff | All appointments | Full view; Approve/Reject/Cancel actions in Pending | Approve, Reject, Cancel, Save, Upload Docs |
| Staff Supervisor | All appointments | Same as Clinic Staff | Same |
| IT Admin | All appointments | Full view + can edit email fields | All buttons |
| Doctor | No list access | No access | n/a |

---

## 16. Branding Tokens

| Element | Token |
|---|---|
| Page heading | `--text-primary`, heading weight |
| Status badge -- Pending | `--status-pending` (yellow) |
| Status badge -- Approved | `--status-approved` (green) |
| Status badge -- Rejected | `--status-rejected` (red) |
| Status badge -- Cancelled | `--status-cancelled` (grey) |
| Status badge -- Rescheduled | `--status-rescheduled` (blue) |
| Status badge -- Checked In | `--status-checked-in` (teal) |
| Status badge -- Billed | `--status-billed` (purple) |
| Save / Re-Request buttons | `btn-primary` via `--brand-primary` |
| Approve button | `btn-secondary` |
| Reject button | `btn-danger` |
| AwaitingMoreInfo banner | border `--status-pending` |
| Needs-review field highlight | left-border `--status-pending` |

Token definitions: `_design-tokens.md`.

---

## 17. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Component | `AppointmentEditComponent` shared across roles | Dedicated `appointment-view.component` per role split |
| Route | `/appointments/:appointmentId` (no guard on edit module) | `/appointments/view/:id` with `authGuard + permissionGuard` |
| Date picker | `rx-date` custom control | Angular Material `mat-datepicker` or `ngbDatepicker` |
| Phone/SSN mask | `rx-mask` custom directive | `ngx-mask` or Angular CDK |
| Table | `rx-table` custom component | Angular Material `mat-table` |
| Modals | Bootstrap inline `#myModal` + `RxDialog` | `MatDialog` CDK overlay |
| Status state machine | Approve / Reject / Cancel only | Adds AwaitingMoreInfo + send-back + Save & Resubmit flow |
| Field-level locking | `[disabled]="isView"` global flag | Per-field `canEdit('fieldName')` keyed on status + flagged-field list |
| Change log | Not present | `/appointments/view/:id/change-log` sub-route |
| Documents | Separate page: `/appointment-new-documents/{id}` | Embedded `<app-appointment-documents>` + `<app-appointment-packet>` |
| Attorney entry | Manual free-text inputs | Email-search lookup → loads from identity user pool |

---

## 18. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | External user view page | No dedicated view page; uses `AppointmentEditComponent` with role-conditional sections | Dedicated `appointment-view.component` at `/appointments/view/:id` | Architectural improvement: cleaner role separation; no behavioral regression |
| 2 | AwaitingMoreInfo + send-back | Does not exist; staff can only Approve or Reject | NEW adds AwaitingMoreInfo status with flagged-field banner and Save & Resubmit | NEW-only feature; deliberate enhancement for iterative booking review |
| 3 | Change log | Does not exist | `/appointments/view/:id/change-log` sub-page with audit trail | NEW-only feature; no parity impact |
| 4 | Claim editing in view page | External users can add/edit claims via Bootstrap modal while re-applying after rejection | NEW: Claims in view page are read-only; claim editing only during initial booking at `/appointments/add` | Surface to Adrian -- may be an intentional restriction or an unimplemented gap |
| 5 | Attorney data entry | Manual free-text input for all attorney fields | Email-search lookup populates attorney details from identity user pool; fields readonly after load | Enhancement; attorney data is identity-linked rather than free-text. Common-path behavior unchanged |
| 6 | Document upload | "Upload Documents" button navigates away to `/appointment-new-documents/{id}` | Inline embedded `<app-appointment-documents>` component keeps documents in context | Architectural improvement; preserves document-in-context UX |
| 7 | "Re-Request" after rejection | Button visible for rejected appointments created by current user; calls `reApplyAppointmentRequest()` | Not clearly present in NEW `appointment-view.component` -- verify re-booking from rejected state is implemented | Surface to Adrian; may need implementation in Phase 19b if absent |
| 8 | Language "Other" free-text | `*ngIf="languageOtherId == selectedLanguage"` shows plain text input | NEW should replicate same conditional: text input replaces select when Other chosen | Match OLD exactly |
| 9 | Date of Birth filter (list) | `*ngIf="!isUserIsPatient"` -- hidden for Patient role, shown for Adjuster/Attorney | NEW list view must replicate same condition | Match OLD exactly |
| 10 | Internal user comments visibility | Staff notes displayed in Appointment Details section when internal user adds comments | NEW: `internalUserComments` field exists; confirm it renders in view page for external users (read-only) | Verify display -- if absent, add as read-only field in Section 1 |

---

## 19. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `my-appointments/my-appointment-list.component.html` | 1-206 | External user list -- filters, table, status colors, Document Manager action |
| `my-appointments/my-appointment-list.component.ts` | 1-222 | List logic, `isUserIsPatient()`, navigation to view/edit page |
| `edit/appointment-edit.component.html` | 1-79 | Header + action button split (internal vs external) |
| `edit/appointment-edit.component.html` | 80-276 | Section 1: Appointment Details -- status, banners, dates |
| `edit/appointment-edit.component.html` | 280-418 | Section 2: Patient Demographics (18+ fields) |
| `edit/appointment-edit.component.html` | 420-460 | Section 3: Employer Details (FormArray) |
| `edit/appointment-edit.component.html` | 463-738 | Section 4: Claim Information (Bootstrap modal + table) |
| `edit/appointment-edit.component.html` | 740-796 | Section 5: Additional Authorized Users |
| `edit/appointment-edit.component.html` | 799-937 | Section 6: Attorney Details (Applicant + Defense) |
| `edit/appointment-edit.component.html` | 939-965 | Section 7: Additional Details / Custom Fields |
| `edit/appointment-edit.component.html` | 969-1007 | Bottom action button repeat (both roles) |
| `edit/appointment-edit.routing.ts` | all | Route: `/appointments/:appointmentId` (no guard) |
| `appointments/appointments.routing.ts` | all | Parent routing: `applicationModuleId: 6` |

---

## 20. Verification Checklist

- [ ] External user (Patient role) navigates to `/appointments/view/{id}` and sees their appointment
- [ ] All appointment fields are read-only for external user in Pending/Approved/Rejected states
- [ ] Re-schedule and Cancel buttons are visible when status = Approved and no change request is pending
- [ ] Re-schedule and Cancel buttons are hidden when a change request is already pending
- [ ] "Re-Request" action is available when status = Rejected and user is the original booker
- [ ] Help button opens the submit-query modal
- [ ] Upload Documents button opens the document upload interface
- [ ] AwaitingMoreInfo status shows flagged-fields banner with staff note and field list
- [ ] Flagged fields are editable; non-flagged fields remain locked during AwaitingMoreInfo
- [ ] "Save & Resubmit" replaces "Save" in AwaitingMoreInfo status
- [ ] Status badge renders correct color for all 13 statuses (list page)
- [ ] Language select shows "Other" free-text input when Other language is selected
- [ ] Interpreter Vendor input appears only when "Do you need an interpreter?" = Yes
- [ ] Phone Number Type radio (Home/Work/Other) is visible and functional
- [ ] Date of Birth filter in list is hidden for Patient role, visible for Adjuster/Attorney
- [ ] "Document Manager" button in list navigates to document upload/view
- [ ] Claims table shows all linked claims in read-only format for external users
- [ ] Authorized users table shows CRUD actions in edit mode, read-only otherwise
- [ ] Applicant / Defense Attorney sections toggle on/off when checkbox is toggled
- [ ] Attorney email is readonly for the attorney's own role (Applicant Attorney cannot edit their email)
- [ ] Custom fields section renders all configured fields with correct input types (text/number/date)
- [ ] Internal user sees Approve/Reject/Cancel buttons on Pending appointments
- [ ] Change log link visible to users with `CaseEvaluation.AppointmentChangeLogs` permission
- [ ] Embedded appointment documents + packet components render below custom fields
