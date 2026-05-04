---
feature: appointment-change-log
date: 2026-05-04
phase: 2-frontend (per-appointment view IMPLEMENTED in NEW; standalone search page NOT implemented)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-change-log/appointment-change-logs/list/appointment-change-log-list.component.html (175 lines)
new-feature-path: angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.ts (per-appointment view only)
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: pending
---

# Design: Appointment Change Log

## Overview

The Change Log records field-level changes to appointments: who changed what field,
from what value to what value, and when.

In OLD, the Change Log is a **standalone searchable list** at `/appointment-change-logs`
showing changes across ALL appointments. Internal users can filter by confirmation number,
field name, old/new value, and date.

In NEW, a **per-appointment change log** view exists at
`angular/src/app/appointments/appointment-change-logs/` and uses ABP's built-in audit
logging service. However, the standalone cross-appointment search page is NOT implemented.

**Parity gap:** OLD has cross-appointment search; NEW has per-appointment view only.
See Exception 1.

---

## 1. Routes

| Surface | OLD | NEW |
|---|---|---|
| Standalone change log search | `/appointment-change-logs` | Not implemented |
| Per-appointment change log | Not a dedicated route (accessed from appointment-view) | `/appointments/change-log/:id` (or embedded in appointment-view) |

Guard:
- OLD: `canActivate: [PageAccess]` `applicationModuleId: 14`
- NEW: `canActivate: [authGuard, permissionGuard]`; `CaseEvaluation.AppointmentChangeLogs`

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar).

---

## 3. OLD Standalone Change Log Page

### 3a. Layout

```
+----------------------------------------------------------+
| [H2] Change Logs       [Search input] [Reset]           |
| [Advanced Search accordion]                             |
|   Confirmation Number [text]  Field Name [text]         |
|   Old Value [text]            New Value [text]          |
|   Modified Date [date picker]                           |
|                               [Reset]  [Search]         |
+----------------------------------------------------------+
| [rx-table]                                              |
| Conf # (link) | Status (badge) | Field Name |           |
| Old Value | New Value | Modified Date | Modified By     |
+----------------------------------------------------------+
```

OLD source: `appointment-change-log/appointment-change-logs/list/appointment-change-log-list.component.html:1-175`

### 3b. Filters (Advanced Search accordion)

| Filter | Type | Notes |
|---|---|---|
| Confirmation Number | text | Placeholder "A0000" |
| Field Name | text | Name of the changed field |
| Old Value | text | Previous field value |
| New Value | text | New field value |
| Modified Date | date picker | `MM/DD/YYYY` |

### 3c. Table Columns

| Column | Notes |
|---|---|
| Confirmation # | Clickable link → `router.navigate(['appointments', appointmentId])` |
| Status | Color-coded badge (all 13 statuses; see `external-user-view-appointment-design.md` Section 3d) |
| Field Name | `fieldName` (name of the changed column) |
| Old Value | `oldValue` |
| New Value | `newValue` |
| Modified Date | `changedDate` |
| Modified By | `modifiedBy` (user display name) |

**Entity Name column** (`tableName`) was present but is commented out in OLD.

OLD route guard: `applicationModuleId: 14` (mapped to `MODULES.AppointmentChangeLogs`)

---

## 4. NEW Per-Appointment Change Log View

Already implemented at:
`angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.ts`

### 4a. Data Source

Uses ABP's `AuditLogsService.getEntityChangesWithUsername()` with:
- `entityId` = appointment ID (from route param `:id`)
- `entityTypeFullName` = `'HealthcareSupport.CaseEvaluation.Appointments.Appointment'`

ABP audit logging records changes automatically for `[Audited]` entities.

### 4b. NEW Columns

| Column | Notes |
|---|---|
| When | `entityChange.changeTime` (Angular `| date:'short'`) |
| Who | `entry.userName` |
| Type | `changeTypeLabel()` → "Created" / "Updated" / "Deleted" |
| Property | `prop.propertyName` (field name) |
| Old value | `prop.originalValue` |
| New value | `prop.newValue` |

No Status column (not needed per-appointment; status is shown in the appointment header).
No Confirmation # column (already in context).

### 4c. NEW Behaviors

- Loading state: "Loading change log..."
- Empty state: "No changes recorded for this appointment yet."
- Error state: "Failed to load change log."
- "Back to appointment" button navigates to `/appointments/view/{id}`

---

## 5. Parity Gap: Cross-Appointment Search

OLD's standalone search page at `/appointment-change-logs` shows changes across ALL
appointments with rich filtering (by field, value, date). NEW has no equivalent.

**Decision (Adrian, 2026-05-04):** Option A (parity) is the target -- implement the
standalone search page to replicate OLD behavior. However, this is **lowest-priority** work:
build it AFTER the primary appointment booking lifecycle is complete and verified.

Before implementing: verify whether ABP's `AuditLogsService` exposes field-level query
parameters (`propertyName`, `originalValue`, `newValue`) needed for the 5-filter search.

---

## 6. Role Visibility

| Role | Access | Notes |
|---|---|---|
| External users | No | |
| Clinic Staff | Per-appointment only | Own assignments |
| Staff Supervisor | Yes (all appointments) | Both per-appointment and standalone |
| IT Admin | Yes (all appointments) | Full audit visibility |

---

## 7. Branding Tokens

| Element | Token |
|---|---|
| Page heading | `--text-primary` |
| Status badge | Same 13-status color matrix as `external-user-view-appointment-design.md` |
| Table | Standard `table-striped` |
| Back button | `btn-outline-secondary` |
| Loading text | `--text-muted` |
| Error alert | `--status-rejected` (red) |

---

## 8. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Data source | Dedicated `AppointmentChangeLogs` table + stored proc | ABP audit logging (`EntityChanges` + `EntityPropertyChanges` tables) |
| Scope | Standalone cross-appointment search | Per-appointment view (navigated from appointment-view page) |
| Status column | Color-coded badge with all 13 statuses | Not present in per-appointment view |
| Filters | 5 filter fields (confirmation #, field name, old/new value, date) | No filters in current per-appointment view |
| Confirmation # link | Navigates to appointment-view | Not needed (already in context) |

---

## 9. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Standalone search page | `/appointment-change-logs` with cross-appointment search + 5 filters | Deferred -- per-appointment view only until after main booking lifecycle is verified | Adrian confirmed parity target (Option A) but deferred to lowest priority; verify ABP audit API field-level filter support before building |
| 2 | `AppointmentChangeLogs` table | Dedicated audit table with `requestConfirmationNumber`, `fieldName`, `oldValue`, `newValue`, `changedDate`, `modifiedBy` | ABP EntityChanges + EntityPropertyChanges tables (accessed via audit logging service) | Framework change; ABP audit logging is the standard mechanism and is already wired |
| 3 | Status column in change log | Shows current appointment status as color-coded badge in each change log row | Not needed in per-appointment view (status is in appointment-view header context) | Per-appointment view has context already |

---

## 10. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `appointment-change-log/appointment-change-logs/list/appointment-change-log-list.component.html` | 1-175 | Full standalone change log: search input, advanced filters, 7-column table with status badges |
| `appointment-change-log/appointment-change-logs/appointment-change-logs.routing.ts` | 1-14 | Route guard: `PageAccess` `applicationModuleId: 14` |

NEW source: `angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.ts` (full per-appointment view implementation)

---

## 11. Verification Checklist

- [ ] Per-appointment change log accessible from appointment-view page for internal users
- [ ] Clicking change log link navigates to `/appointments/change-log/{id}`
- [ ] Change log shows: When, Who, Type, Property, Old value, New value columns
- [ ] Loading state shown while fetching
- [ ] Empty state "No changes recorded" shown when no audit entries exist
- [ ] Error state shown if ABP audit API call fails
- [ ] "Back to appointment" navigates to appointment-view page
- [ ] External users cannot access change log
- [ ] Clinic Staff can only see change log for their assigned appointments
- [ ] Staff Supervisor / IT Admin can see change log for all appointments
- [ ] (Phase 19b) Standalone search page at `/change-logs` -- verify ABP audit API supports field-level filter (Exception 1)
