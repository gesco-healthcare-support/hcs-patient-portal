---
feature: clinic-staff-check-in-check-out
date: 2026-05-04
phase: 2-frontend (NOT YET IMPLEMENTED in NEW; no check-in/check-out component in angular/src/app/)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointments/list/appointment-list.component.html (140 lines) + .ts (302 lines)
new-feature-path: n/a (check-in/check-out module does not yet exist in angular/src/app/)
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: partial (old/admin/03-checkin-checkout.png per status tracker; not yet captured)
---

# Design: Clinic Staff -- Check-In / Check-Out Appointments

## Overview

The Check-In / Check-Out page is the day-of-exam operations view. Clinic Staff use it to
progress appointments through the day-of status chain: Approved -> CheckedIn -> CheckedOut
-> Billed (or Approved -> NoShow for no-shows).

Each status transition is triggered by a conditional action icon in the table row. Only
the action relevant to the appointment's current status is visible.

In OLD this page lives at the misleadingly-named route `/appointment-approve-request`. It is
entirely separate from the all-appointments list and the approval/pending workflow.

In NEW, **this feature is not yet implemented**. No Angular component or AppService
method for day-of status transitions exists.

---

## 1. Route

| | OLD | NEW |
|---|---|---|
| Check-in / Check-out page | `/appointment-approve-request` (AppointmentListModule) | `/check-in` |

OLD route name `appointment-approve-request` is a legacy misnomer -- the page is the
day-of check-in/check-out view, not the approval workflow. NEW uses a descriptive route.

Guard:
- OLD: `canActivate: [PageAccess]` `rootModuleId: 33` `applicationModuleId: 6` `accessItem: 'add'`
- NEW: `canActivate: [authGuard, permissionGuard]`; `CaseEvaluation.Appointments.CheckIn`

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar).
Side-nav item: "Check-In / Check-Out" or "Today's Appointments".

---

## 3. OLD Page Layout

```
+--------------------------------------------------------------+
| [H2] Check-in & Check-out Appointments (N) Date: MM-DD-YYYY |
|                                [Search input] [Sync reset]   |
+--------------------------------------------------------------+
| [Today's Appointment btn] | [< Previous] [date picker] [Next >] |
+--------------------------------------------------------------+
| [rx-table]                                                   |
| Patient Name | Gender | Type | Conf # | Appt Time |          |
| DOB | SSN | Claim # | DOI | Location | Contact No |          |
| Status | Responsible User | Action                           |
| (Action: Check In / Check Out / Billed / Not Show icons)     |
+--------------------------------------------------------------+
```

OLD source: `appointments/list/appointment-list.component.html:1-140`

---

## 4. Date Navigation Controls

| Control | Behavior |
|---|---|
| "Today's Appointment" button | Calls `ngOnInit()` -- resets to today's date and reloads |
| Previous button | Steps date back one day; reloads table |
| Date picker (`rx-date`) | `MM/DD/YYYY` format; selecting a date reloads table via `getDateForAppointmentData()` |
| Next button | Steps date forward one day; reloads table |

Default: page loads with today's date on `ngOnInit()`.
Date state held in `this.appointmentDate` (JavaScript `Date` object).

Caption format in page header: `MM-dd-yyyy` (e.g. `05-04-2026`).

OLD source: `appointment-list.component.ts:59-68` (init), `248-273` (prev/next/date selected),
`appointment-list.component.html:26-50` (controls)

---

## 5. Search

| Element | Behavior |
|---|---|
| Text input | Free-text search; submits on Enter key or search icon click |
| Sync / reset icon | Clears `searchQuery` to `" "` (single space -- parity flag; see Exception 1) and reloads |

OLD source: `appointment-list.component.ts:281-294`

---

## 6. Table Columns

| Column | Field | Notes |
|---|---|---|
| Patient Name | `patientName` | `text-uppercase` CSS |
| Gender | `gender` | |
| Type | `appointmentTypeName` | |
| Confirmation # | `requestConfirmationNumber` | Clickable link → `appointments/{appointmentId}` |
| Appointment Time | `appointmentDateTime` | Day-of time (not just date) |
| Date Of Birth | `dateOfBirth` | Sortable |
| SSN | `socialSecurityNumber` | Sortable |
| Claim # | `claimNumberList` | Split display (multiple claims) |
| Date Of Injury | `dateOfInjuryList` | Split display (multiple claims) |
| Location | `locationName` | |
| Contact No | `contactNumber` | |
| Status | `appointmentStatusName` | Text label; no color badge on this page |
| Responsible User | `responsiblePerson` | |
| Action | -- | Conditional icons; see Section 7 |

---

## 7. Action Icons (Status-Conditional)

Each icon appears ONLY when the appointment's current status matches its precondition.
At any given moment, a row shows at most two icons (Check In + Not Show for Approved;
or one other icon for post-check-in statuses).

| Icon | Title | Condition | Target status |
|---|---|---|---|
| `fa-arrow-alt-circle-right` (green) | "Check In" | `appointmentStatusId == Approved (2)` | CheckedIn (9) |
| `fa-eye-slash` (yellow/orange) | "Not Show" | `appointmentStatusId == Approved (2)` | NoShow (4) |
| `fa-arrow-alt-circle-left` (blue) | "Check Out" | `appointmentStatusId == CheckedIn (9)` | CheckedOut (10) |
| `fa-file-alt` (grey) | "Billed" | `appointmentStatusId == CheckedOut (10)` | Billed (11) |

Status enum values (from `appointment-status-type.ts`):
`Approved=2, NoShow=4, CheckedIn=9, CheckedOut=10, Billed=11`

**Notes icon** (`fa-info` / `AppointmentInfoComponent`): present in OLD template but commented
out. Not to be implemented in NEW for Phase 1. See Exception 2.

OLD source: `appointment-list.component.html:82-128`

---

## 8. Status Transition Flow

```
Approved (2)
  ├── [Check In]   → CheckedIn (9)
  │                     └── [Check Out] → CheckedOut (10)
  │                                           └── [Billed] → Billed (11)
  └── [Not Show]  → NoShow (4)
```

Each transition requires:
1. Confirmation dialog with message:
   - Check In: "Checked In Appointment"
   - Check Out: "Checked Out Appointment"
   - Billed: "Billed Appointment"
   - Not Show: "No Show Appointment"
2. On confirm: `PATCH /api/appointments/{appointmentId}` with:
   ```json
   {
     "appointmentStatusId": <target_status_id>,
     "appointmentId": <id>,
     "isInternalUserUpdateStatus": true,
     "isDataUpdate": false,
     "isStatusUpdate": true
   }
   ```
3. Success toast messages:
   - "Appointment Request CheckedIn"
   - "Appointment Request CheckedOut"
   - "Appointment Request Billed"
   - "Appointment Request No Showed"
4. Table reloads after 500ms delay.

OLD source: `appointment-list.component.ts:113-240`

---

## 9. API

| Operation | OLD | NEW |
|---|---|---|
| Load appointments for date | `GET api/appointments` with params `[orderByColumn, sortOrder, pageIndex, rowCount, appointmentStatusId, date, searchQuery]` | `GET /api/app/appointments?date={date}&...` |
| Status transition | `PATCH api/appointments/{id}` with status payload | `POST /api/app/appointments/{id}/check-in`, `POST /api/app/appointments/{id}/check-out`, `POST /api/app/appointments/{id}/bill`, `POST /api/app/appointments/{id}/no-show` |

**OLD status filter note:** The component initializes `appointmentStatusId = Approved (2)` and
passes it to the query, but the presence of Check Out and Billed icons in the template
suggests the API shows appointments at ALL day-of statuses regardless of this parameter
(otherwise CheckedIn/CheckedOut rows would never appear). This is a parity flag -- verify
in OLD whether the filter is actually applied server-side. See Section 11.

---

## 10. Role Visibility

| Role | Access | Notes |
|---|---|---|
| External users | No | |
| Clinic Staff | Yes (own appointments, scoped by `PrimaryResponsibleUserId`) | Primary actor for day-of operations |
| Staff Supervisor | Yes (all appointments) | Supervisory oversight |
| IT Admin | Optional -- confirm with Adrian | System oversight |

The `accessPermissionService.urlPermission(MODULES.AppointmentCheckInCheckOut)` guard in OLD
maps to the `CaseEvaluation.Appointments.CheckIn` permission in NEW.

---

## 11. Branding Tokens

| Element | Token |
|---|---|
| Page heading | `--text-primary` |
| Today's Appointment button | `btn-primary` via `--brand-primary` |
| Previous / Next buttons | `btn-secondary` |
| Check In icon | green via `--status-approved` |
| Check Out icon | blue via `--brand-primary` |
| Billed icon | grey/neutral |
| Not Show icon | orange/yellow via `--status-pending` |
| Success toast | `--status-approved` (green) |

---

## 12. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Date navigation | `rx-date` date picker + manual `previousDate()` / `nextDate()` | Angular Material `mat-datepicker` + date navigation buttons |
| Status updates | Single `PATCH /api/appointments/{id}` with generic payload | Dedicated action endpoints per transition (check-in, check-out, bill, no-show) for clarity and auditability |
| Confirmation dialog | `RxDialog.confirmation()` | `MatDialog` confirmation modal |
| Toast | `RxToast.show()` | ABP `Confirmation` service or `MatSnackBar` |
| Table | `rx-table` with stored proc pagination | `mat-table` with server-side pagination |
| Search clear | Sets `searchQuery = " "` (space, not empty string) | `searchQuery = ""` (empty string) |

---

## 13. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Search clear sets space not empty | `searchAppointmentClear()` sets `searchQuery = " "` (single space) then queries | NEW sets empty string `""` | Bug in OLD: trailing space forces a non-empty query that may differ from an empty query server-side. NEW corrects this. |
| 2 | Notes popup (AppointmentInfoComponent) | `fa-info` icon triggers `AppointmentInfoComponent` popup -- COMMENTED OUT in OLD template | Not implemented in Phase 1 | Notes feature is commented out in OLD and not part of Phase 1 scope; appointment notes handled via the notes tab on the appointment-view page instead |
| 3 | Route name `/appointment-approve-request` | Misleading legacy route name | NEW uses `/check-in` (descriptive) | Route name correction; functionality identical |
| 4 | Status filter in query | `appointmentStatusId = Approved (2)` passed to API (may be ignored server-side) | NEW shows all appointments for selected date regardless of status filter | Verify OLD server-side behavior; NEW should show all day-of statuses so staff can see the full day's picture |
| 5 | PATCH for status updates | Single PATCH endpoint with generic payload + flags | Dedicated POST endpoints per action | Explicit action endpoints are clearer, easier to audit, and map to domain events |

---

## 14. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `appointments/list/appointment-list.component.html` | 1-140 | Full day-of list: date navigation, table, conditional action icons |
| `appointments/list/appointment-list.component.ts` | 1-302 | `checkInRequest()`, `checkOutRequest()`, `billedRequest()`, `noShowRequest()`, `updateAppointmentRequest()`, date nav, search |
| `appointments/list/appointment-list.routing.ts` | 1-15 | Route guard: `PageAccess` `applicationModuleId: 6` |
| `start/app.lazy.routing.ts` | 116-118 | Top-level route: `/appointment-approve-request` → `AppointmentListModule` |
| `const/appointment-status-type.ts` | 1-16 | Status enum: `Approved=2, CheckedIn=9, CheckedOut=10, Billed=11, NoShow=4` |

---

## 15. Verification Checklist

*(Pending implementation)*

- [ ] Check-in page accessible to Clinic Staff, Staff Supervisor; blocked for external users
- [ ] Page loads with today's date on initial navigation
- [ ] "Today's Appointment" button resets to today's date and reloads
- [ ] Previous and Next buttons step date by one day and reload table
- [ ] Date picker allows arbitrary date selection; table reloads on date change
- [ ] Page header shows "Check-in & Check-out Appointments (N) Date: MM-DD-YYYY"
- [ ] Table shows all appointments for selected date
- [ ] Patient Name column is uppercase
- [ ] Confirmation # is a clickable link to appointment-view page
- [ ] Check In icon (right arrow) visible only for Approved-status rows
- [ ] Not Show icon (eye-slash) visible only for Approved-status rows
- [ ] Check Out icon (left arrow) visible only for CheckedIn-status rows
- [ ] Billed icon (file-alt) visible only for CheckedOut-status rows
- [ ] No action icon visible for Billed, NoShow, Rejected, or Pending rows
- [ ] Each action shows confirmation dialog before executing
- [ ] Confirmed Check In transitions appointment from Approved to CheckedIn
- [ ] Confirmed Check Out transitions from CheckedIn to CheckedOut
- [ ] Confirmed Billed transitions from CheckedOut to Billed
- [ ] Confirmed Not Show transitions from Approved to NoShow
- [ ] Success toast shown after each transition
- [ ] Table reloads after status transition
- [ ] Clinic Staff sees only their assigned appointments (role-scoped)
- [ ] Staff Supervisor sees all appointments (no user filter)
- [ ] Text search filters table by search query; Enter key triggers search
- [ ] Search clear resets to full list for selected date
