---
feature: appointment-notes
date: 2026-05-04
phase: 2-frontend (NOT YET IMPLEMENTED in NEW; no notes component in angular/src/app/; entry point in OLD was commented out)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointments/info/appointment-info.component.html (73 lines) + .ts (199 lines)
new-feature-path: n/a (notes component does not exist in angular/src/app/)
shell: internal-user-authenticated (modal popup; no dedicated route)
screenshots: pending
---

# Design: Appointment Notes

## Overview

Appointment Notes allow internal users to add, edit, and delete free-text notes on any
appointment. Notes are per-appointment, ordered by date descending, and each user can
only edit or delete their own notes.

In OLD, notes are surfaced through `AppointmentInfoComponent` -- a modal popup.
The entry point in the check-in/check-out page (`NoteRequest()` icon) was
**commented out** in the template and never shipped as a user-accessible feature.
Notes are effectively UNUSED in the current OLD app, but the backend (`api/Notes`)
and data model are fully implemented.

In NEW, **this feature is not yet implemented**. The Notes entity, AppService, and
Angular component do not exist.

**Decisions confirmed by Adrian (2026-05-04):**
1. Notes surfaced as a `MatDialog` popup (NOT embedded tab/section). Same intent as OLD.
2. All internal users can see all notes on any appointment they can access; only the note
   author can edit or delete their own notes.
3. Note edit DOES create a new record (POST with `editNoteId`). This is intentional for
   compliance / audit trail.
4. Notes accessible from two entry points: appointment-view page AND check-in/check-out list.

---

## 1. Route

No dedicated route. Notes are displayed in a modal popup in OLD.

OLD: `RxPopup.show(AppointmentInfoComponent, { appointmentId, patientDetail })`
NEW: `MatDialog.open(AppointmentNotesDialogComponent, { data: { appointmentId } })`

Entry points (two):
1. Appointment-view page -- a "Notes" button/icon opens the dialog.
2. Check-in/check-out list -- the `fa-info` Notes icon (currently commented out in OLD template)
   will be uncommented and wired to `MatDialog.open(AppointmentNotesDialogComponent)`.

---

## 2. Shell

Modal overlay (`MatDialog`) from the internal-user authenticated shell.
Launched from two entry points: appointment-view page and check-in/check-out list.

---

## 3. OLD Notes Popup Layout

```
+--------------------------------------------------+
| Notes                                  [X]       |
+--------------------------------------------------+
| Patient Name  [readonly]  Confirmation# [readonly] |
| Location      [readonly]  Appt Type    [readonly]  |
| Appt Date     [readonly]                           |
+--------------------------------------------------+
| [Comment textarea]    | [Note 1]                  |
| [Save] [Cancel?]      |  AuthorName - RoleName    |
|                       |  CreatedDate              |
|                       |  Comment text             |
|                       |  [Edit] [Delete] (own only) |
|                       | [Note 2] ...              |
|                       | "No Notes Available" (empty)|
+--------------------------------------------------+
```

OLD source: `appointments/info/appointment-info.component.html:1-73`

---

## 4. Note Display Fields

Each note in the right panel shows:

| Field | Notes |
|---|---|
| Author name | `firstName lastName` (from `modifiedFirstName/LastName` if edited, else `firstName/lastName`) |
| Author role | `roleName` (or `modifiedRoleName` if edited) |
| Date | `modifiedDate` if edited, else `createdDate` (Angular `| date:'medium'`) |
| Comment text | `comments` |
| Edit icon | Visible ONLY if `item.parentNoteId == currentUserId` (own notes only) |
| Delete icon | Visible ONLY if `item.parentNoteId == currentUserId` (own notes only) |

Notes are sorted by date descending (most recent first).

"No Notes Available" shown in red when no notes exist for the appointment.

OLD source: `appointments/info/appointment-info.component.html:47-66`

---

## 5. Patient Context Header (Read-Only)

The popup header shows appointment context (read-only fields):

| Field | Source |
|---|---|
| Patient Name | `patientDetail.name` |
| Confirmation # | `patientDetail.confirmationNumber` |
| Location | `patientDetail.locationName` |
| Appointment Type | `patientDetail.appointmentTypeName` |
| Appointment Date | `patientDetail.appointmentDateTime` |

OLD: context passed as `@Input() patientDetail: PatientDetailViewModel`.
NEW: if notes are embedded in appointment-view page, this context header is redundant
-- the page already shows the appointment. Header only needed if notes are a standalone popup.

---

## 6. Add Note

| Field | Type | Validation |
|---|---|---|
| Comment | textarea | Required |

- Save button disabled until `comments` is valid (non-empty).
- `addNote()` determines `parentNoteId` from existing notes for this appointment
  (see Section 7 on the `parentNoteId` confusing naming).
- `POST /api/Notes` with body: `{ comments, statusId: 1, appointmentId, createdById, createdDate, parentNoteId, isLatest: true, editNoteId: 0 }`
- Success toast: "Note added successfully"
- Reloads notes list after add.

OLD source: `appointments/info/appointment-info.component.ts:84-117`

---

## 7. Edit Note

Edit loads the comment into the textarea. Save triggers `editNote()`:
- Creates a NEW Note record (POST, not PUT) with:
  - `editNoteId` = original note's `noteId`
  - `isLatest = true`
  - `modifiedById` = current user
  - `modifiedDate` = now
  - `createdById` = original note's `createdById` (preserved)
  - `createdDate` = original note's `createdDate` (preserved)

This pattern tracks note history -- each edit creates a new record referencing the
original via `editNoteId`. The `isLatest` flag marks the current version.

**Adrian confirmed (2026-05-04): this is intentional for compliance.** Replicate exactly
in NEW: edit POST creates a new record with `editNoteId` set, `isLatest=true`, and original
`createdById`/`createdDate` preserved. The previous version's `isLatest` is set to `false`.
See Exception 1.

OLD source: `appointments/info/appointment-info.component.ts:146-180`

---

## 8. Delete Note

- Confirmation dialog: "this note"
- `DELETE /api/Notes/{noteId}`
- Success toast: "Note deleted successfully"
- Reloads notes list.

---

## 9. `parentNoteId` Naming Confusion

In OLD, `Note.parentNoteId` stores the **current user's ID** (not a note parent/thread ID).
The name is misleading. It is used for the "own notes only" edit/delete check:
```
*ngIf="noteFormGroup.value.parentNoteId == item.createdById"
```

In NEW, this field should be renamed to `responsibleUserId` or `ownerUserId` to match
its actual purpose, and the logic should use `note.createdById == currentUserId` instead.
See Exception 2.

---

## 10. API

| Operation | OLD | NEW |
|---|---|---|
| Load notes for appointment | `lookup([NoteLookups.noteLookUps])` then filter client-side by `appointmentId` | `GET /api/app/notes?appointmentId={id}` (server-side filter) |
| Add note | `POST api/Notes` | `POST /api/app/notes` |
| Edit note (creates new record) | `POST api/Notes` with `editNoteId` set | `POST /api/app/notes` with `editNoteId` (if history kept) or `PUT /api/app/notes/{id}` (if simplified) |
| Delete note | `DELETE api/Notes/{id}` | `DELETE /api/app/notes/{id}` |

**Client-side filter bug:** In OLD, `lookup([NoteLookups.noteLookUps])` fetches ALL notes
for ALL appointments and filters in memory. This is a PHI exposure risk -- an internal
user's browser receives notes for all appointments. NEW must filter server-side.
See Exception 3.

---

## 11. Note Data Fields

| Field | Type | Notes |
|---|---|---|
| NoteId | int PK | |
| Comments | string | Required |
| AppointmentId | int FK | |
| ParentNoteId | int | Misleadingly named; stores owner UserId (see Section 9) |
| CreatedById | int FK | |
| CreatedDate | datetime | |
| ModifiedById | int? | Set on edit |
| ModifiedDate | datetime? | Set on edit |
| IsLatest | bool | True = current version; older edit versions have IsLatest=false |
| EditNoteId | int | 0 = new note; N = ID of note being edited |
| StatusId | int | 1 = Active |

---

## 12. Role Visibility

| Role | Can add notes | Can edit/delete | Can view |
|---|---|---|---|
| External users | No | No | No |
| Clinic Staff | Yes | Own notes only | All notes for assigned appointments |
| Staff Supervisor | Yes | Own notes only | All notes for all appointments |
| IT Admin | Yes | Own notes only | All notes for all appointments |

---

## 13. Branding Tokens

| Element | Token |
|---|---|
| Modal header | `--brand-primary` background |
| Save button | `btn-primary` via `--brand-primary` |
| Edit icon | `--brand-primary` |
| Delete icon | `--status-rejected` (red) |
| "No Notes Available" | `--status-rejected` (red) |
| Comment timestamp | `--text-muted` (grey) |
| Author name | `--brand-primary` |

---

## 14. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Entry point | Check-in page Notes icon (COMMENTED OUT in OLD -- never shipped) | `MatDialog.open(AppointmentNotesDialogComponent)` from two entry points: appointment-view page + check-in list |
| Notes fetch | Client-side filter of all notes | Server-side `GET /api/app/notes?appointmentId={id}` |
| Popup | `RxPopup.show(AppointmentInfoComponent)` | `MatDialog.open(AppointmentNotesDialogComponent)` using latest Angular 20 + Material packages |
| Edit pattern | POST creates new record (history audit trail) | POST creates new record (compliance requirement; confirmed by Adrian) |

---

## 15. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Edit creates new record | `POST api/Notes` with `editNoteId` -- edit history audit trail | NEW replicates: POST creates new record with `editNoteId`, prior version `isLatest=false` | Intentional for compliance (Adrian confirmed 2026-05-04); preserves full audit trail |
| 2 | `parentNoteId` = userId | Field name misleads; stores note owner's userId | NEW renames to `ownerUserId` or drops in favor of `createdById` for ownership check | Naming correction; functionally equivalent |
| 3 | Client-side filter | Loads ALL notes for ALL appointments, filters in browser | Server-side filter by `appointmentId` | Security: prevents PHI notes from leaking to browser for all appointments |
| 4 | Notes popup from check-in page | AppointmentInfoComponent -- COMMENTED OUT in OLD template | `MatDialog.open(AppointmentNotesDialogComponent)` from two entry points: appointment-view page AND check-in/check-out list (fa-info icon wired in NEW) | Entry points confirmed by Adrian; fa-info icon deliberately commented out in OLD but the backend is fully implemented |

---

## 16. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `appointments/info/appointment-info.component.html` | 1-73 | Notes popup: context header, comment textarea, notes list with edit/delete |
| `appointments/info/appointment-info.component.ts` | 1-199 | `addNote()`, `editNote()`, `onDelete()`, sort logic, popup close |
| `note/notes/notes.service.ts` | 1-95 | `POST api/Notes`, `DELETE api/Notes/{id}` |
| `appointments/list/appointment-list.component.ts` | 97-112 | `NoteRequest()` popup launch (commented out in template) |

---

## 17. Verification Checklist

*(Pending implementation)*

- [ ] Notes dialog (`MatDialog`) accessible from appointment-view page (Notes button)
- [ ] Notes dialog accessible from check-in/check-out list (fa-info icon in Action column)
- [ ] Notes list shows for correct appointment only (server-side filter)
- [ ] Each note shows: author name, author role, date, comment text
- [ ] Notes sorted by date descending (most recent first)
- [ ] "No Notes Available" shown when no notes exist
- [ ] Comment textarea required; Save disabled when empty
- [ ] Add note: POST creates record; success toast shown; list reloads
- [ ] Edit icon visible only for notes created by current user
- [ ] Edit loads comment into textarea; Save creates new audit record (POST with `editNoteId`); prior version `isLatest=false`
- [ ] Delete icon visible only for notes created by current user
- [ ] Delete shows confirmation dialog; DELETE removes record; success toast; list reloads
- [ ] External users cannot access notes
- [ ] Clinic Staff can only see notes on their assigned appointments
- [ ] Staff Supervisor / IT Admin see all appointment notes
