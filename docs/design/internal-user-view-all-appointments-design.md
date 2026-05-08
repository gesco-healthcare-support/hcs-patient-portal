---
feature: internal-user-view-all-appointments
date: 2026-05-04
phase: 2-frontend (Angular appointments list component exists; NEW appointment list has filters)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointments/search/appointment-search.component.html (230 lines)
new-feature-path: angular/src/app/appointments/appointment/components/appointment.component.html
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: partial (new/admin/03-appointments-list-host-context.png, new/t1-doctor/03-appointments-list-13-rows.png)
---

# Design: Internal User -- View All Appointments

## Overview

The all-appointments list lets Clinic Staff, Staff Supervisor, and IT Admin search and
browse all appointments with filters. Clicking a Confirmation # navigates to the full
appointment-view page.

This is the internal-user equivalent of the external user's "My Appointments" list, but
without the role scope limitation -- internal users see ALL appointments (subject to
the role-scoping rules: Clinic Staff sees assigned appointments only; Supervisor/Admin
see all).

---

## 1. Route

| | OLD | NEW |
|---|---|---|
| All appointments list | `/appointments/search` (AppointmentSearchModule) | `/appointments` |

Guard: `canActivate: [PageAccess]` `applicationModuleId: 6` (OLD).
NEW: `canActivate: [authGuard, permissionGuard]`; `CaseEvaluation.Appointments.Default`.

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar).
Side-nav item: "Appointments" (or similar) under the main navigation.

---

## 3. OLD Page Layout

```
+-------------------------------------------------------+
| [H2] All Appointments  [Search input] [Sync]         |
| [Advanced Search accordion]                          |
|   Appt Type [select]   Conf # [text]                 |
|   Location [select]    Status [select]               |
|   Claim # [text]       Date Of Injury [date]         |
|   Date Of Birth [date] SSN [masked]                  |
|   [Search]  [Reset]                                  |
+-------------------------------------------------------+
| [Table rx-table]                                     |
| Patient Name | Gender | Type | Conf # |              |
| Appt Date | DOB | SSN | Claim # | DOI |              |
| Location | Status | Action (Document Manager)        |
+-------------------------------------------------------+
```

OLD source: `appointments/search/appointment-search.component.html:1-230`

---

## 4. Filters

| Filter | Notes |
|---|---|
| Appointment Type | dropdown lookup |
| Confirmation Number | text |
| Location | dropdown lookup |
| Appointment Status | dropdown lookup |
| Claim # | text |
| Date Of Injury | date picker |
| Date Of Birth | date picker (visible to all internal roles) |
| Social Security # | masked input |

Note: Date of Birth filter visible to ALL internal users (unlike external-user list where
it is hidden for Patient role).

---

## 5. Table Columns

| Column | Notes |
|---|---|
| Patient Name | uppercase CSS |
| Gender | |
| Type | Appointment Type name |
| Confirmation # | clickable link → appointment-view page |
| Appointment Date | |
| Date Of Birth | sortable |
| SSN | masked, sortable |
| Claim # | split display (multiple claims per appointment) |
| Date Of Injury | split display (multiple claims) |
| Location | |
| Status | color-coded badge (13 statuses) |
| Action | "Document Manager" button → document upload page |

Status badge colors: same 13-status color matrix as external-user list
(see `external-user-view-appointment-design.md` Section 3d).

---

## 6. Role-Scoped Filtering

- **Clinic Staff:** Results filtered to appointments where `PrimaryResponsibleUserId == currentUserId`.
- **Staff Supervisor / IT Admin:** No filter -- see all appointments.

This filtering is applied at the backend query level, not the UI.

---

## 7. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Role-scoped filtering | Clinic Staff sees own assigned; Supervisor sees all | NEW must apply same scope | Verify `IAppointmentAccessPolicy` is applied in list query |
| 2 | Status filter includes all 13 statuses | Full status dropdown | NEW dropdown should include all statuses including RescheduleRequested, CancellationRequested | Match OLD exactly |

---

## 8. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `appointments/search/appointment-search.component.html` | 1-230 | Full list with filters, columns, status colors |

---

## 9. Verification Checklist

- [ ] Internal user sees all appointments (no role restriction for Supervisor/Admin)
- [ ] Clinic Staff sees only appointments assigned to them
- [ ] All 8 filters work: Type, Confirmation#, Location, Status, Claim#, DOI, DOB, SSN
- [ ] Status filter dropdown includes all 13 statuses
- [ ] Date of Birth filter visible to ALL internal users
- [ ] Confirmation # link navigates to full appointment-view page
- [ ] Status badges show correct colors for all statuses
- [ ] Document Manager action navigates to document upload
