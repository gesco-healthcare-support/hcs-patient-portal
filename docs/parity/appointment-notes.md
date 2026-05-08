---
feature: appointment-notes
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\NoteModule\NoteDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Note\NotesController.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Lookups\NoteLookupsController.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\note\notes\
old-docs:
  - data-dictionary-table.md (Notes)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: ClinicStaff / StaffSupervisor / ITAdmin (and possibly external for visible notes)
depends-on:
  - external-user-appointment-request
required-by: []
---

# Appointment notes

## Purpose

Free-text notes attached to appointments. Threaded (parent-child). Used by clinic staff for internal documentation; possibly also visible to external users on shared appointments (TO VERIFY).

**Strict parity with OLD.**

## OLD behavior (binding)

### Schema (`Notes` table)

- `NoteId` (PK)
- `ParentNoteId` (FK to Note for threading)
- `AppointmentId` (FK)
- `Comments` (nvarchar Max)
- `CreatedById, CreatedDate, ModifiedById, ModifiedDate`
- `IsLatest` (bit, nullable) -- flag for the latest revision in an edit chain
- `StatusId` (Active/Delete)
- `EditNoteId` (FK to Note, nullable) -- references the original note when this row is an edit

### Threading + edit chain pattern

Two relationships:

- `ParentNoteId`: reply-to threading. A reply has `ParentNoteId = parent's Id`.
- `EditNoteId`: edit history. When a note is edited, a new row is created with `EditNoteId = original's Id` and the original gets `IsLatest = false`. The new row is `IsLatest = true`.

### Endpoints (per API matrix)

- `GET /api/Notes` -- list
- `GET /api/Notes/{id}`, `POST`, `PUT`, `PATCH`, `DELETE` -- standard CRUD
- `GET /api/NoteLookups` -- lookup for note types (TO VERIFY)

### Critical OLD behaviors

- **Threading via ParentNoteId** -- replies form a tree.
- **Edit history via EditNoteId + IsLatest** -- preserves history, only latest shown by default.
- **Soft delete via StatusId**.
- **Per-appointment scope** -- notes belong to one appointment.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/NoteModule/NoteDomain.cs` (139 lines) | CRUD with edit-chain logic |
| `PatientAppointment.Api/Controllers/Api/Note/NotesController.cs` | API |
| `patientappointment-portal/.../note/notes/...` | UI |

## NEW current state

- TO VERIFY: NEW has `Notes/` folder.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| `Note` entity | OLD | TO VERIFY | **Add `Note : FullAuditedEntity<Guid>, IMultiTenant`** with AppointmentId FK, ParentNoteId FK?, Comments, IsLatest, EditNoteId FK? | B |
| Threading via ParentNoteId | OLD | -- | **Match** | I |
| Edit chain via EditNoteId + IsLatest | OLD | -- | **Match** -- on edit, insert new row + set old.IsLatest=false; new.EditNoteId=old.Id, new.IsLatest=true | I |
| `INotesAppService` | OLD | -- | **Add CRUD methods** + thread/list helpers | B |
| Permissions | OLD: any internal user (TO VERIFY external visibility) | -- | **`CaseEvaluation.Notes.{Default, Create, Edit, Delete}`** -- internal users; verify external visibility on shared appointments | I |
| List filtered by appointment | OLD | -- | **`GetListByAppointmentAsync(Guid appointmentId)`** -- returns IsLatest=true rows in thread order | I |
| Soft delete | OLD `StatusId` | ABP `ISoftDelete` | None | -- |

## Internal dependencies surfaced

- None new.

## Branding/theming touchpoints

- Notes UI (logo, primary color, thread styling).

## Replication notes

### ABP wiring

- `Note` entity with self-referencing FKs (`ParentNoteId`, `EditNoteId`).
- AppService follows standard ABP CRUD pattern.
- Edit chain logic in domain service `NoteManager.UpdateAsync(noteId, newComments)`.

### Verification

1. Internal user adds a note to appointment -> visible in notes list
2. Reply to existing note -> threaded under parent
3. Edit a note -> new row created, old row IsLatest=false; UI shows only latest
4. Delete a note -> soft-deleted; not visible
5. External user views appointment -> sees only notes flagged visible-to-external (if such flag exists -- TO VERIFY)
