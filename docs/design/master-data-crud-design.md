---
feature: master-data-crud
date: 2026-05-04
phase: 2-frontend (PARTIALLY IMPLEMENTED: appointment-types, appointment-statuses, appointment-languages, locations, states, wcab-offices all have ABP-generated CRUD in angular/src/app/; document types and document library NOT yet ported)
status: draft
old-source: multiple -- see Section 9
new-feature-path: angular/src/app/{appointment-types,appointment-statuses,appointment-languages,locations,states,wcab-offices}/
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: pending
---

# Design: Master Data CRUD (Admin Reference Tables)

## Overview

Master data pages let IT Admin and Staff Supervisor manage the reference tables that
drive appointment booking: locations, WCAB offices, appointment types, document types,
and the document library.

In OLD, the relevant admin surfaces are:

| OLD route | Surface | OLD module |
|---|---|---|
| `/locations` | Locations + WCAB Offices (dual-mode, radio toggle) | `doctor-management/locations` |
| `/appointment-document-types` | Document Types | `document/appointment-document-types` |
| `/documents` | Document Library (files used in packages) | `document-management/documents` |
| (no route) | Appointment Types | `doctor-management/appointment-types` (stub only -- no active route) |
| (no route) | Appointment Statuses | Not present -- enum-only in OLD |
| (no route) | Languages | Not present -- seeded data in OLD |
| (no route) | States | Not present -- seeded lookup data in OLD |

In NEW, ABP Suite generated standalone CRUD for ALL of the above. The OLD-equivalent
surfaces (Locations, WCAB, Document Types, Document Library) need field-level parity.
The NEW-only additions (Appointment Types, Statuses, Languages, States) have no OLD
source to match -- they are NEW enhancements and just need to work correctly.

---

## 1. Routes

| Surface | OLD | NEW |
|---|---|---|
| Locations list | `/locations/location` (`:type` route, type=`location`) | `/locations` |
| WCAB Offices list | `/locations/wcab` (`:type` route, type=`wcab`) | `/wcab-offices` |
| Document Types | `/appointment-document-types` | `/appointment-document-types` |
| Document Library | `/documents` | `/documents` |
| Appointment Types | No active route | `/appointment-types` |
| Appointment Statuses | No active route | `/appointment-statuses` |
| Languages | No active route | `/appointment-languages` |
| States | No active route | `/states` |

Guards:
- OLD: `canActivate: [PageAccess]` with `applicationModuleId: 9` (locations), various for others
- NEW: `canActivate: [authGuard, permissionGuard]` per entity permissions (see Section 8)

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar) for all master-data pages.
Side-nav group: "Administration" or "Master Data" menu section.

---

## 3. Surface: Locations + WCAB Offices

### 3a. OLD Dual-Mode Toggle

OLD uses a SINGLE page at `/locations` with radio buttons to toggle between
"Location" and "Wcab" views. The table and Add form switch based on selection.

```
+----------------------------------------------------------+
| [H2 "Location" or "WCAB Office"]   [Search]  [Reset]   |
+----------------------------------------------------------+
| (o) Location  ( ) Wcab                        [Add +]   |
+----------------------------------------------------------+
| Location view: Name | Address | City | State | Zip |    |
|   Parking Fee | Appt Type | Status | Edit               |
|                                                          |
| WCAB view: Name | Address | City | State | Zip |        |
|   Status | Edit                                          |
+----------------------------------------------------------+
```

NEW splits this into two separate routes (`/locations` and `/wcab-offices`).
See Exception 1.

### 3b. Location Fields (Add / Edit)

| Field | Type | Notes |
|---|---|---|
| Location Name | text | Required |
| Location Address | text | Required |
| City | text | Required |
| State | dropdown | From `statesLookUps` (seeded reference data) |
| ZipCode | text | Required |
| Parking Fee | text | Optional |
| Appointment Type | dropdown | From appointment types lookup; 1 type per location |
| Status | radio | Active (1) / Inactive (2) |

### 3c. WCAB Office Fields (Add / Edit)

| Field | Type | Notes |
|---|---|---|
| WCAB Office Name | text | Required |
| WCAB Address | text | Required |
| City | text | Required |
| State | dropdown | From `statesLookUps` |
| ZipCode | text | Required |
| Status | radio | Active (1) / Inactive (2) |

### 3d. Location Table Columns

| Column | Notes |
|---|---|
| Location Name | |
| Location Address | |
| City | |
| State | |
| ZipCode | |
| Parking Fee | |
| Appointment Type | Linked appointment type name |
| Status | Active / Inactive |
| Actions | Edit (pen icon); Delete was **commented out** in OLD (no delete for locations) |

### 3e. WCAB Office Table Columns

| Column | Notes |
|---|---|
| WCAB Office Name | |
| Address | |
| City | |
| State | |
| ZipCode | |
| Status | Active / Inactive |
| Actions | Edit only; no delete |

OLD source: `doctor-management/locations/list/location-list.component.html:1-98`,
`doctor-management/locations/add/location-add.component.html:1-124`

---

## 4. Surface: Document Types

### 4a. OLD List

```
+----------------------------------------------------------+
| [H2] Document Type    [Search]  [Reset]     [Add +]     |
+----------------------------------------------------------+
| Document Type | Status | Edit + Delete                   |
+----------------------------------------------------------+
```

### 4b. Document Type Fields (Add / Edit)

| Field | Type | Notes |
|---|---|---|
| Document Type Name | text | Required |
| Status | radio | Active (1) / Inactive (2) |

Both Add and Edit actions open an **inline form** in OLD (not a modal or route; the
`showDocumentTypeComponent()` / `onEdit()` handlers load the form in-place).

- Supports Delete (trash icon) -- only document type list has delete enabled.
- Route guard: `applicationModuleId` from `appointment-document-types.routing.ts`.

OLD source: `document/appointment-document-types/list/appointment-document-type-list.component.html:1-65`,
`document/appointment-document-types/add/appointment-document-type-add.component.html:1-37`

---

## 5. Surface: Document Library

### 5a. OLD List

```
+----------------------------------------------------------+
| [H2] Documents        [Search]              [Add +]     |
+----------------------------------------------------------+
| Document Name | File Path | Status | Edit + Delete       |
+----------------------------------------------------------+
```

### 5b. Document Fields (Add / Edit)

OLD source only reveals list columns; the add form opens inline via `showDocumentAddComponent()`.
Expected fields based on list columns:

| Field | Type | Notes |
|---|---|---|
| Document Name | text | Required |
| Document File Path | text or file upload | Path to the file on the server |
| Status | radio | Active / Inactive |

**Note:** File Path in the list suggests OLD stored documents as server-side file paths
(not blob storage). In NEW, document files should be stored via ABP blob provider
(Azure Blob / local file provider). See Exception 2.

OLD source: `document-management/documents/list/document-list.component.html:1-50`

---

## 6. Surface: Appointment Types (NEW-only)

No OLD source -- appointment types were defined as static seeded data in OLD
(the `doctor-management/appointment-types/list` stub is just `<h1>AppointmentType</h1>`
with no active top-level route).

NEW generates full ABP CRUD with:

| Field | Type | Notes |
|---|---|---|
| Name | text | Required |
| Description | text | Optional |

The NEW list shows Name + Description columns; filter on Name; bulk delete; Actions dropdown (Edit / Delete).

This is purely NEW functionality -- no strict-parity requirement. Verify the field set
matches what the appointment booking form (drop down) and location assignment expect.

NEW source: `angular/src/app/appointment-types/appointment-type/components/appointment-type.component.html`

---

## 7. Surface: Appointment Statuses, Languages, States (NEW-only)

These three entities have no OLD admin surfaces. They were seeded data in OLD:
- **Appointment Statuses**: defined as `AppointmentStatusTypeEnums` in TS code (Pending=1 ... CancellationRequested=13).
- **Languages**: seeded lookup data (English, Spanish, etc.).
- **States**: US state lookup data (CA, TX, NY ...) used as dropdown in location forms.

NEW provides ABP-generated CRUD for each. The CRUD pages follow the same ABP pattern as
Appointment Types (Name + optional Description columns; filter; bulk delete; Actions dropdown).

These pages should be accessible only to IT Admin and should NOT be editable by lower
roles (a status change or state rename would break appointments across the system).

---

## 8. Role Visibility Matrix

| Role | Locations / WCAB | Doc Types | Doc Library | Appt Types | Statuses / Languages / States |
|---|---|---|---|---|---|
| External users | No | No | No | No | No |
| Clinic Staff | No | No | No | No | No |
| Staff Supervisor | Read only (view locations for scheduling) | No | No | No | No |
| IT Admin | Full CRUD | Full CRUD | Full CRUD | Full CRUD | Full CRUD |

**Note:** In OLD, the `applicationModuleId: 9` guard maps to the doctor-management module.
Clinic Staff had no access; Staff Supervisor and IT Admin did. NEW should gate these pages
behind `CaseEvaluation.{Entity}.Create` / `Edit` / `Delete` permissions.

Permission names for NEW:
- `CaseEvaluation.AppointmentTypes.Create / Edit / Delete`
- `CaseEvaluation.AppointmentStatuses.Create / Edit / Delete`
- `CaseEvaluation.AppointmentLanguages.Create / Edit / Delete`
- `CaseEvaluation.Locations.Create / Edit / Delete`
- `CaseEvaluation.WcabOffices.Create / Edit / Delete`

---

## 9. Branding Tokens

| Element | Token |
|---|---|
| Add button | `btn-primary` via `--brand-primary` |
| Edit icon | `--brand-primary` |
| Delete icon | `--status-rejected` (red) |
| Table header | `--text-primary` |
| Status badge Active | `--status-approved` (green) |
| Status badge Inactive | `--text-muted` (grey) |

---

## 10. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Locations + WCAB | Single page with radio toggle | Two separate routes `/locations` + `/wcab-offices` |
| Table | `rx-table` (stored proc pagination) | `ngx-datatable` (ABP standard) |
| Add / Edit form | Inline form or `routerLink` sub-route | ABP modal (`app-{entity}-detail-modal`) |
| Status field | Radio buttons Active/Inactive | ABP `isActive` boolean toggle |
| Bulk operations | Not available in OLD | Checkbox select + bulk delete in NEW |
| Appointment Types admin | No standalone page (stub only) | Full ABP CRUD at `/appointment-types` |
| Statuses admin | Enum-only in OLD | Full ABP CRUD at `/appointment-statuses` |
| Languages admin | Seeded lookup in OLD | Full ABP CRUD at `/appointment-languages` |
| States admin | Seeded lookup in OLD | Full ABP CRUD at `/states` |

---

## 11. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Locations + WCAB on single page | Radio toggle between Location and WCAB Office views on one route | Two separate routes: `/locations` and `/wcab-offices` | ABP Suite generates per-entity CRUD; split is cleaner and matches ABP navigation patterns |
| 2 | Document file storage | `documentFilePath` is a plain text field storing a server path | NEW must use ABP blob provider (local or Azure) + store a reference ID | Framework improvement; file paths are not portable across deploys |
| 3 | No delete for locations | Delete action commented out in OLD location list | NEW should also NOT delete locations with existing appointments; add a guard or soft-delete | Data integrity; deleting a location with historical appointments would break records |
| 4 | Status field type | Radio buttons Active (1) / Inactive (2) with statusId integer | ABP `isActive` boolean; display as toggle or checkbox | Framework mapping; functionally equivalent |
| 5 | Appointment Types no standalone CRUD | No admin page; types were seeded | Full CRUD page in NEW | NEW enhancement; required because doctors/locations need type assignment without DB-level seeding |

---

## 12. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `doctor-management/locations/list/location-list.component.html` | 1-98 | Dual-mode list (Location + WCAB radio toggle, tables, edit link) |
| `doctor-management/locations/add/location-add.component.html` | 1-124 | Add Location form + Add WCAB Office form |
| `doctor-management/locations/locations.routing.ts` | 1-27 | Route guard: `applicationModuleId: 9`; `:type` route pattern |
| `document/appointment-document-types/list/appointment-document-type-list.component.html` | 1-65 | Document Type list (Name, Status, Edit + Delete) |
| `document/appointment-document-types/add/appointment-document-type-add.component.html` | 1-37 | Add Document Type form (Name, Status radio) |
| `document-management/documents/list/document-list.component.html` | 1-50 | Document Library list (Name, File Path, Status, Edit + Delete) |
| `start/app.lazy.routing.ts` | 83-147 | Top-level routes: `/locations`, `/appointment-document-types`, `/documents` |
| `const/appointment-status-type.ts` | 1-16 | Appointment status enum values (Pending=1 ... CancellationRequested=13) |

---

## 13. Verification Checklist

- [ ] IT Admin can access all master-data CRUD pages; Clinic Staff cannot
- [ ] Locations list shows all 8 columns; Edit navigates to edit form
- [ ] Add Location form: all 7 fields present; State populates from state lookup; Save validates required fields
- [ ] WCAB Offices list shows all 6 columns; Edit navigates to edit form
- [ ] Add WCAB Office form: all 5 fields present; Save validates required fields
- [ ] Location and WCAB Office are on separate routes (no radio toggle in NEW -- Exception 1)
- [ ] Locations: no Delete action (or delete is guarded against appointments-in-use)
- [ ] Document Types list shows Name, Status, Edit + Delete
- [ ] Add Document Type: Name + Status radio (Active/Inactive); Save validates Name required
- [ ] Document Type delete works; confirmation dialog before deletion
- [ ] Document Library list shows Name, File Path/Reference, Status, Edit + Delete
- [ ] Appointment Types CRUD: Name + Description fields; Create / Edit / Delete
- [ ] Appointment Statuses CRUD: admin-only; Name field
- [ ] Languages CRUD: admin-only; Name field
- [ ] States CRUD: admin-only; Name + abbreviation field
- [ ] Bulk delete works for entities that support it (Appointment Types, Statuses, Languages, States)
- [ ] Status Active/Inactive persists correctly (Exception 4: ABP isActive maps to display)
