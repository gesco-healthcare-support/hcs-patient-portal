---
feature: staff-supervisor-doctor-management
date: 2026-05-04
phase: 2-frontend (backend DoctorAppService + AvailabilityAppService + LocationAppService done; Angular UI exists)
status: draft
old-source: patientappointment-portal/src/app/components/doctor-management/
old-components:
  - doctors/edit/doctor-edit.component.ts + .html (doctor profile inline form)
  - doctors-availabilities/list/doctors-availability-list.component.html (availability list, 141 lines)
  - doctors-availabilities/add/doctors-availability-add.component.html (slot generation form, 278 lines)
  - doctor-preferred-locations/list/doctor-preferred-location-list.component.html (checkbox list)
  - locations/list/location-list.component.html (locations + WCAB master list, 99 lines)
new-feature-path: angular/src/app/doctors/ + angular/src/app/doctor-availabilities/ + angular/src/app/locations/
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: pending
---

# Design: Staff Supervisor -- Doctor Management

## Overview

Four sub-surfaces covered by this doc:

1. **Doctor Profile CRUD** -- Edit the doctor's basic profile (name, email, gender, identity user link).
2. **Doctor Appointment Types** -- Assign which appointment types the doctor accepts.
3. **Doctor Preferred Locations** -- Assign which clinic locations the doctor works at.
4. **Doctor Availability** -- Generate and manage time-slot calendars (slot by dates or by weekdays).
5. **Locations Master** (brief) -- Create/edit clinic locations. Full CRUD in `master-data-crud-design.md`.

OLD is **single-doctor-per-deploy**. The Doctor entity exists but the Doctor user role does NOT
(it was removed as cleanup task #7). The Staff Supervisor (and IT Admin) manage "the doctor".
NEW supports multiple doctors but the Phase 1 demo has one doctor.

Doctor management data drives the booking form's slot picker, location dropdown, and
appointment-type dropdown. All three filtering rules flow from these records.

---

## 1. Routes

| Surface | OLD | NEW |
|---|---|---|
| Doctor list | n/a (no list -- single-doctor-per-deploy) | `/doctor-management/doctors` |
| Doctor edit | `/doctors/:doctorId` | modal overlay from list row action |
| Availability list | `/doctors-availabilities` | `/doctor-management/doctor-availabilities` |
| Availability generate | `/doctors-availabilities/add` | `/doctor-management/doctor-availabilities/generate` |
| Locations list | `/locations` | `/locations` (separate module) |
| Location add/edit | `/locations/add` or `/locations/edit/:id` | modal overlay from list row action |

Guards:
- OLD: `canActivate: [PageAccess]` per route with `applicationModuleId` matching the module.
  Doctor edit: `applicationModuleId: X`; Availability: `applicationModuleId: Y`.
- NEW: `canActivate: [authGuard, permissionGuard]`; requires Staff Supervisor or IT Admin role.
  Doctor CRUD: `CaseEvaluation.Doctors.*`; Availability: `CaseEvaluation.DoctorAvailabilities.*`;
  Locations: `CaseEvaluation.Locations.*`.

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar). Side-nav items:
- "Doctor Management" section (Staff Supervisor + IT Admin only)
  - Doctors
  - Doctor Availabilities
- "Locations" section (IT Admin; may also appear for Staff Supervisor depending on role matrix)

---

## 3. Doctor Profile CRUD

### 3a. OLD Doctor Edit Page

In OLD there is no doctor list page. Staff Supervisor navigates directly to
`/doctors/:doctorId`. The edit page is a full-page form:

```
+-------------------------------------------------------+
| [H2] Manage Doctor Details          [Edit User]      |
+---------------------------+---------------------------+
| Doctor Details (card)     | Appt Types + Locations   |
|                           |                          |
| First Name   [text]       | app-doctors-appointment-  |
| Last Name    [text]       |   type-list (inline card) |
| Email        [email]      |                          |
| Gender       [radio]      | app-doctor-preferred-    |
|                           |   location-list (inline) |
| [Save Doctor]  [Cancel]   |                          |
+---------------------------+--------------------------+
```

Layout: two-column (col-12 col-md-6). Doctor details on left; appointment types and
preferred locations (nested child components) on right.

OLD source: `doctors/edit/doctor-edit.component.html:1-60`

### 3b. OLD Doctor -- Appointment Types (child component)

`<app-doctors-appointment-type-list>` inline in the doctor-edit page:
- Displays all available appointment types as a list.
- Each row has a toggle (checkbox or active/inactive toggle) to include or exclude.
- Saves immediately per-toggle or on "Save" for the parent form.

OLD source: `doctor-management/doctors-appointment-types/` component.

### 3c. OLD Doctor -- Preferred Locations (child component)

`<app-doctor-preferred-location-list>` inline in the doctor-edit page:
- Simple checkbox list of all clinic locations.
- Checked = doctor accepts appointments at this location.
- Saves as `DoctorPreferredLocation` records.

OLD source: `doctor-preferred-locations/list/doctor-preferred-location-list.component.html`

---

### 3d. NEW Doctor List + Modal

NEW introduces a full doctor list (ABP DataTable):

```
+-------------------------------------------------------+
| [H2] Doctors                   [Create] [Refresh]    |
| [ABP Advanced Filters]                               |
|   First Name [text]  Last Name [text]  Email [text]  |
|   Identity User [lookup]  Appt Type [typeahead]      |
|   Location [typeahead]                               |
|   [Search]  [Clear]                                  |
+-------------------------------------------------------+
| [NGX-DataTable]                                      |
| Actions | First Name | Last Name | Email | Gender |  |
| Identity User | Tenant                               |
+-------------------------------------------------------+
| [Pagination]                                         |
+-------------------------------------------------------+
```

**Actions dropdown per row:** Edit, Delete (permission-gated).

NEW source: `doctors/doctor/components/doctor.component.html:1-210`

### 3e. NEW Doctor Detail Modal (3 tabs)

Opens via "Create" button or "Edit" action:

```
+-------------------------------------------------------+
| Doctor Details                                 [X]   |
| [Tab: Doctor] [Tab: Appointment Types] [Tab: Locations] |
+-------------------------------------------------------+
| TAB 1 -- Doctor:                                     |
|   First Name (required)  [text]                      |
|   Last Name (required)   [text]                      |
|   Email                  [email]                     |
|   Gender                 [select enum]               |
|   Identity User          [lookup]                    |
+-------------------------------------------------------+
| TAB 2 -- Appointment Types:                          |
|   [abp-lookup-typeahead-mtm]  (multi-to-many)        |
|   Shows count of assigned types                      |
+-------------------------------------------------------+
| TAB 3 -- Locations:                                  |
|   [abp-lookup-typeahead-mtm]  (multi-to-many)        |
|   Shows count of assigned locations                  |
+-------------------------------------------------------+
| [Cancel]  [Save]                                     |
+-------------------------------------------------------+
```

NEW source: `doctors/doctor/components/doctor-detail.component.html:1-140`

### 3f. Doctor Fields Summary

| Field | OLD | NEW |
|---|---|---|
| First Name | text input (required) | text input (required) |
| Last Name | text input (required) | text input (required) |
| Email | email input | email input |
| Gender | radio (Male/Female) | select (enum lookup -- includes Non-Binary) |
| Identity User | n/a (doctor not a user in OLD) | lookup to ABP Identity user |
| Appointment Types | inline nested component (checkbox list) | Tab 2: multi-to-many typeahead |
| Preferred Locations | inline nested component (checkbox list) | Tab 3: multi-to-many typeahead |

---

## 4. Doctor Availability List

### 4a. OLD Availability List

```
+-------------------------------------------------------+
| [H2] Doctor Availability      [Add + button]         |
| [Advanced Search accordion]                          |
|   Location [select]  From Date [date]  To Date [date]|
|   [Search]  [Reset]                                  |
+-------------------------------------------------------+
| [Accordion table -- grouped by date+location]        |
| [Row] LocationName | AvailableDate | Available |      |
|       Booked | Reserved | Total           [>]         |
|   [Expanded child rows]                              |
|   Location | TimeSlots | Status | Delete             |
+-------------------------------------------------------+
| [Pagination]                                         |
+-------------------------------------------------------+
```

Parent row summarizes: `AvailableSlot`, `BookedSlot`, `ReservedSlot`, `TotalSlot`.
Clicking expands to show individual time slots with status badge and delete action.

Status badges:
- Available: pending-style badge (yellow/blue)
- Booked: billed-style badge (purple)
- Reserved: rejected-style badge (red)

OLD source: `doctors-availabilities/list/doctors-availability-list.component.html:1-141`

### 4b. NEW Availability List

Functionally identical to OLD; implemented with plain HTML table + NGBootstrap pagination.

```
+-------------------------------------------------------+
| [H2] Doctor Availabilities    [Generate] [Refresh]   |
| [ABP Advanced Filters]                               |
|   AvailDateMin/Max [date]  FromTime/ToTime [time]    |
|   BookingStatus [enum select]  Location [lookup]     |
|   AppointmentType [lookup]                           |
|   [Search]  [Clear]                                  |
+-------------------------------------------------------+
| [Custom accordion table]                             |
| Location | Date | Available | Booked | Reserved |    |
| Total           [Expand icon]  [Delete group]        |
|   [Expanded nested table]                            |
|   Location | TimeSlots (HH:mm - HH:mm) | Status |   |
|   Delete slot                                        |
+-------------------------------------------------------+
| [Page size select]  [NGBootstrap pagination]         |
+-------------------------------------------------------+
```

**Delete actions:**
- "Delete group" button: deletes all slots for a date+location pair. Blocked if any slot is Booked or Reserved.
- "Delete slot" icon (per child row): deletes single slot. Blocked if referenced by an appointment or change request.

NEW source: `doctor-availabilities/doctor-availability/components/doctor-availability.component.html:1-332`

---

## 5. Doctor Availability Generation (Slot Generator)

### 5a. Layout (both OLD and NEW)

Two slot-generation modes toggled by radio button:

**Slot By Date(s):** specify an explicit date range.
**Slot By Weekdays:** specify a month + day-of-week range.

```
+-------------------------------------------------------+
| [H2] Generate Doctor Availability  [Back]            |
+-------------------------------------------------------+
| Slot Mode: ( ) Slot By Date(s)  ( ) Slot By Weekdays |
+-------------------------------------------------------+
| [Common fields]                                      |
|   Location         [lookup]     (required)           |
|   From Time        [time]       (required)           |
|   To Time          [time]       (required)           |
|   Appointment Type [lookup]     (optional -- filter) |
|   Booking Status   [enum]       (default: Available) |
|   Time Slot Interval [minutes]  (e.g. 60)            |
+-------------------------------------------------------+
| [DATES MODE -- shown if Slot By Date(s)]             |
|   From Date  [date]  (required)                      |
|   To Date    [date]  (required)                      |
+-------------------------------------------------------+
| [WEEKDAYS MODE -- shown if Slot By Weekdays]         |
|   Use Current Month  [radio: Yes/No]                 |
|   Month Picker       [disabled if current month]     |
|   From Day           [select: 1..31 of month]        |
|   To Day             [select: 1..31 of month]        |
+-------------------------------------------------------+
| [Generate / Preview] button                          |
+-------------------------------------------------------+
| [Preview table -- appears after Generate]            |
| Grouped by Month:                                    |
|   [Month header]  Location | Dates | Time | Days  [>]|
|   [Expanded nested table]                            |
|   Location | TimeSlots | Status | Conflict | Delete  |
+-------------------------------------------------------+
| [Submit]  [Cancel]                                   |
+-------------------------------------------------------+
```

### 5b. Slot Generation Fields

| Field | Notes |
|---|---|
| Location | Required; filters doctor's preferred locations |
| From Time | Required; slot start (e.g., "09:00") |
| To Time | Required; slot end (e.g., "17:00") |
| Appointment Type | Optional; limits slot to one type (null = any type) |
| Booking Status | Default: Available. Enum: Available / Reserved / Booked |
| Time Slot Interval | Minutes per slot (e.g., 60 = 1-hour slots); divides From-To range |
| From Date / To Date | DATES mode: inclusive date range |
| Use Current Month | WEEKDAYS mode: locks Month Picker if Yes |
| Month Picker | WEEKDAYS mode: month selector (disabled if current month selected) |
| From Day / To Day | WEEKDAYS mode: day-of-month range within the selected month |

### 5c. Preview Table

After clicking Generate, a preview accordion appears showing what slots will be created.
Each slot row shows a **Conflict** indicator if the slot overlaps an existing Available/Reserved/Booked
slot at the same location. Conflicting slots can be individually deleted from the preview
before final submission.

**Conflict detection rule (strict parity):**
An overlap exists if: `existingSlot.FromTime < newSlot.ToTime AND existingSlot.ToTime > newSlot.FromTime`
AND same `LocationId` AND same `AvailableDate`.

After reviewing the preview, click **Submit** to persist all non-deleted slots.

OLD source: `doctors-availabilities/add/doctors-availability-add.component.html:1-278`
NEW source: `doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.html:1-357`

---

## 6. Locations CRUD

Locations are managed by IT Admin (and Staff Supervisor per OLD permission matrix) as
master data. Full detail is in `master-data-crud-design.md`. Brief surface documented here
for doctor-management context.

### 6a. OLD Locations List

```
+-------------------------------------------------------+
| [H2] Locations                   [Add + button]      |
| ( ) Location  ( ) WCAB Office   [radio toggle]       |
| [Search input]  [Refresh]                            |
+-------------------------------------------------------+
| [Table]                                              |
| Name | Address | City | State | ZipCode |            |
| ParkingFee | AppointmentType | Status | Edit         |
+-------------------------------------------------------+
```

Radio toggle switches between Locations and WCAB Offices (two separate entity types
displayed in the same list component via the radio).

OLD source: `doctor-management/locations/list/location-list.component.html:1-99`

### 6b. NEW Locations List

ABP standard list with filters:
- Filters: Name, City, ZipCode, Min/Max ParkingFee, IsActive
- Table columns: Name, Address, City, ZipCode, ParkingFee, IsActive
- Create / Edit modal fields: Name (required), Address, City, ZipCode, ParkingFee (required), IsActive

WCAB Offices are managed separately in NEW (see `master-data-crud-design.md`).

NEW source: `locations/location/components/location.component.html`,
`locations/location/components/location-detail.component.html`

---

## 7. Role Visibility Matrix

| Role | Doctor CRUD | Availability CRUD | Location CRUD |
|---|---|---|---|
| Staff Supervisor | Edit (no create/delete in OLD) | Full CRUD | Read + create (per OLD) |
| IT Admin | Full CRUD | Full CRUD | Full CRUD |
| Clinic Staff | No access | Read-only (slot counts for scheduling reference) | No access |
| External users | No access | No access | No access |
| Doctor | No access (Doctor role removed -- cleanup #7) | No access | No access |

---

## 8. Branding Tokens

| Element | Token |
|---|---|
| Page heading | `--text-primary` |
| Add / Generate / Save buttons | `btn-primary` via `--brand-primary` |
| Delete button | `btn-danger` |
| Status badge -- Available | `--status-pending` (yellow) |
| Status badge -- Booked | `--status-billed` (purple) |
| Status badge -- Reserved | `--status-rejected` (red) |
| Conflict indicator | `--status-rejected` (red text or badge) |
| Active toggle -- active | `--status-approved` (green) |

Token definitions: `_design-tokens.md`.

---

## 9. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Doctor list | No list page (single-doctor-per-deploy, access via route ID) | Full list with ABP DataTable + create/edit modal |
| Doctor form | Full-page inline form with nested child components | Tabbed modal (Doctor / AppointmentTypes / Locations tabs) |
| AppointmentTypes assignment | Inline `app-doctors-appointment-type-list` (checkbox) | Tab 2 multi-to-many typeahead (`abp-lookup-typeahead-mtm`) |
| Preferred Locations assignment | Inline `app-doctor-preferred-location-list` (checkbox) | Tab 3 multi-to-many typeahead |
| Availability list table | `rx-table` with collapse + advanced search | Custom HTML table + accordion + NGBootstrap pagination |
| Availability filters | Accordion "Advanced Search" card (Location, FromDate, ToDate) | ABP `abp-advanced-entity-filters` (date range, time range, booking status, location, appt type) |
| Slot generation | Inline Bootstrap 4 form + preview accordion | Same UX, Angular 20 `@if` conditional rendering + accordion |
| Locations list | Custom inline table + dual-type radio toggle | ABP list + modal; WCAB Offices in separate module |
| Framework | Bootstrap 4, rx-control-design, Lighthouse Theme | Angular Material, LeptonX, ng-select, ABP standard components |

---

## 10. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Doctor list page | No list -- single-doctor route; navigate directly via `/doctors/:id` | Full list page + create/edit modal | NEW supports multiple doctors for future multi-tenant Phase 2; single-doctor parity met through Phase 1 seed data |
| 2 | Doctor AppointmentTypes UI | Inline nested component with per-row checkbox | Tabbed modal with multi-to-many typeahead | UX improvement; functional parity preserved (same M:N relationship) |
| 3 | Doctor Preferred Locations UI | Inline nested component with checkbox list | Tabbed modal with multi-to-many typeahead | Same as #2; functional parity preserved |
| 4 | Gender field | Radio (Male / Female) | Select enum (includes Non-Binary) | Deliberate expansion per modern standards; OLD values still valid |
| 5 | Doctor Identity User link | Doctor entity has no link to ABP Identity (Doctor role was removed) | NEW `IdentityUserId` on Doctor entity links to staff user who represents the doctor | NEW enhancement; required for permission scoping |
| 6 | Availability filters | Location, From Date, To Date only | Adds booking status, time range, appointment type filters | Enhancement; OLD's 3 filters preserved as subset |
| 7 | WCAB Offices in Locations list | Radio toggle switches Location/WCAB in same list component | WCAB Offices are a separate list module | Architectural separation; same data, different navigation path |
| 8 | Slot conflict check scope | Conflict detection per location per date (time overlap) | Same rule implemented in `BookingPolicyValidator` | Strict parity: `existingFromTime < newToTime AND existingToTime > newFromTime` |
| 9 | Appointment Type on slot | Nullable -- null means any type accepted | Same nullable field; booking form filters slots by type match (type-specific OR null) | Match OLD exactly: `slot.AppointmentTypeId == null || slot.AppointmentTypeId == requestedTypeId` |

---

## 11. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `doctors/edit/doctor-edit.component.html` | 1-60 | Doctor profile form + two nested child components |
| `doctor-preferred-locations/list/doctor-preferred-location-list.component.html` | 1-18 | Checkbox list of preferred locations |
| `doctors-availabilities/list/doctors-availability-list.component.html` | 1-141 | Availability list with accordion + status badges |
| `doctors-availabilities/add/doctors-availability-add.component.html` | 1-278 | Slot generation form (both modes) + preview accordion |
| `locations/list/location-list.component.html` | 1-99 | Locations + WCAB dual-type list |
| `docs/parity/staff-supervisor-doctor-management.md` | all | Full parity audit: slot rules, validation, domain logic, phase implementation status |

---

## 12. Verification Checklist

- [ ] Staff Supervisor navigates to Doctor Management and sees the doctor list
- [ ] Create Doctor modal opens with 3 tabs (Doctor / Appointment Types / Locations)
- [ ] Doctor tab: First Name, Last Name, Email, Gender, Identity User save correctly
- [ ] Appointment Types tab: typeahead allows selecting/deselecting appointment types
- [ ] Locations tab: typeahead allows selecting/deselecting clinic locations
- [ ] Saved appointment-type assignments filter the booking form's appointment-type dropdown
- [ ] Saved location preferences filter the booking form's location dropdown
- [ ] Doctor Availabilities list shows accordion table grouped by date+location
- [ ] Parent row shows Available / Booked / Reserved / Total slot counts
- [ ] Expanding a row shows individual slots with time range and status badge
- [ ] "Delete group" is blocked when any slot in the group is Booked or Reserved
- [ ] "Delete slot" is blocked when the slot is referenced by an appointment or change request
- [ ] Slot generation form shows Location, From Time, To Time, Appointment Type, Status, Interval fields
- [ ] "Slot By Date(s)" mode shows From Date + To Date fields
- [ ] "Slot By Weekdays" mode shows Month picker (disabled when "Use Current Month" = Yes) + From/To Day selects
- [ ] Generate button produces preview accordion grouped by month
- [ ] Conflicting slots are flagged in the preview (overlap with existing slots)
- [ ] Individual conflicting slots can be deleted from the preview before submission
- [ ] Submit persists all non-deleted preview slots
- [ ] Availability slots from generation appear in the booking form slot picker
- [ ] Slots filtered to appointment type when `AppointmentTypeId` is set on the slot
- [ ] Locations list shows Name, Address, City, ZipCode, ParkingFee, IsActive columns
- [ ] Location create/edit modal saves correctly and new location appears in availability generator
- [ ] Non-Staff-Supervisor roles see 403 when accessing doctor management pages
