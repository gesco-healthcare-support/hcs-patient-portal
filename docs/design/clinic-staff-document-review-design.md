---
feature: clinic-staff-document-review
date: 2026-05-04
phase: 2-frontend (backend approve/reject endpoints done; Angular AppointmentDocumentsComponent fully implemented)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointment-documents/
old-components:
  - list/appointment-document-list.component.ts + .html (document list, 139/89 lines)
  - edit/appointment-document-edit.component.ts + .html (re-upload modal, 155/40 lines)
  - appointment-documents.service.ts (API layer, 75 lines)
new-feature-path: angular/src/app/appointment-documents/appointment-documents.component.ts + .html (277/164 lines)
shell: internal-user-authenticated (side-nav + top-bar; embedded in appointment-view page)
screenshots: pending
---

# Design: Clinic Staff -- Document Review

## Overview

The document review surface lets Clinic Staff (and Staff Supervisor / IT Admin) view,
accept, and reject documents that external users upload against an appointment.

In OLD, there is **no approve/reject workflow**. Documents are uploaded by external users;
staff can see them in a list and delete them, but there is no accept/reject action.
Document status is set only on upload (`Uploaded`/`Accepted`/`Pending`).

In NEW, a full approve/reject workflow is implemented as a standalone embedded component
(`AppointmentDocumentsComponent`) with dedicated action endpoints. Staff with the
`CaseEvaluation.AppointmentDocuments.Approve` permission see Approve and Reject buttons
on each document row; rejection requires a reason (max 500 chars) captured in a modal.

This component is embedded in the appointment-view page and also used for external-user
document uploads. The same component serves both roles with conditional button visibility.

---

## 1. Routes

| Surface | OLD | NEW |
|---|---|---|
| External user document upload | `/appointment-new-documents/:appointmentId` (separate route) | Embedded `<app-appointment-documents>` on `/appointments/view/:id` |
| Staff document review | Same route + list component | Same embedded component on `/appointments/view/:id` |

No dedicated document-review route in NEW. The document component is embedded inline
within the full appointment-view page, below the custom fields section.

Guards:
- OLD: `canActivate: [PageAccess]` on the document list route.
- NEW: Document view visible to all authenticated users on the appointment. Approve/Reject
  buttons gated by `CaseEvaluation.AppointmentDocuments.Approve` permission (`canApprove`).

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar) when staff are reviewing.
External-user authenticated shell (top-bar only) when external users are uploading.
The component itself is identical; the shell is determined by the parent page.

---

## 3. OLD Document List Page

In OLD, "Upload Documents" navigates to a separate full page at
`/appointment-new-documents/:appointmentId`:

```
+-------------------------------------------------------+
| [H2] Appointment Documents        [Back]             |
+-------------------------------------------------------+
| [Upload section]                                     |
|   File input  [Choose File]                          |
|   Document Name  [text]                              |
|   [Upload]                                           |
+-------------------------------------------------------+
| [Table rx-table]                                     |
| Document Name | File Type | Status | Uploaded By |   |
| Uploaded Date | Action (Delete, Re-upload)           |
+-------------------------------------------------------+
```

**Upload constraints (OLD):**
- File type: `.doc`, `.docx` only (DEFAULT_IMAGE_FILE_EXTENSTION constant = ".doc,.docx,.pdf")
- File size: `file.size >= (1000 * 1024)` → silent abort (~1MB actual cap)
- Encoding: `FileReader.readAsBinaryString` + `btoa()` (base64 string in request body)

**Document statuses (OLD enum):**
- Pending (4)
- Uploaded (1)
- Accepted / Approved (2)
- Rejected (3)
- Deleted

**No approve/reject buttons** in OLD's staff view. Staff can delete documents only.
The `Accepted` status appears to have been intended but was never wired to a UI action.

OLD source: `list/appointment-document-list.component.html:1-89`,
`edit/appointment-document-edit.component.ts:1-155`

---

## 4. NEW Appointment Documents Component

Embedded as `<app-appointment-documents [appointmentId]="id">` in the appointment-view page.

### 4a. Layout

```
+-------------------------------------------------------+
| [H3] Appointment Documents                           |
+-------------------------------------------------------+
| [Upload section]                                     |
|   [Choose File]  {selectedFileName}                  |
|   Document Name  [text input]                        |
|   [Upload]  (spinner while uploading)                |
+-------------------------------------------------------+
| [Document list]                                      |
| For each document:                                   |
|   [Status badge] Document Name                       |
|   Uploaded by: {name}  Date: {date}                  |
|   [If Rejected: rejection reason -- red text]        |
|   [Download]  [Approve]  [Reject]  [Delete]          |
+-------------------------------------------------------+
```

Buttons visible per document:
- **Download**: Always visible (all roles)
- **Approve**: `*ngIf="canApprove && doc.status !== Approved"` (staff only)
- **Reject**: `*ngIf="canApprove && doc.status !== Rejected"` (staff only)
- **Delete**: Always visible (all roles -- external users delete their own)

NEW source: `appointment-documents.component.html:1-164`

### 4b. Status Badges

| Status | Badge color | Display text |
|---|---|---|
| Uploaded | `--status-pending` (yellow) | "Uploaded" |
| Approved | `--status-approved` (green) | "Approved" |
| Rejected | `--status-rejected` (red) | "Rejected" |

When rejected, the rejection reason appears below the document name in red text.

NEW source: `appointment-documents.component.ts:260-269` (status CSS map)

---

## 5. Document Upload Flow (External User)

External users upload from the same embedded component:

1. Click "Choose File" -- file input opens.
2. File name extracted automatically as default document name (editable).
3. **File size limit:** 25MB max (improvement from OLD's ~1MB cap).
4. **No extension restriction** in the NEW UI layer (server validates if needed).
5. Click "Upload": sends `FormData` (file + documentName) via
   `POST /api/app/appointments/{id}/documents`.
6. Success: list refreshes; `documentsChanged` event emits → parent page calls
   `packetPanel.refresh()`.

**Parity exception on file size:** OLD had ~1MB limit (bug: `file.size >= (1000 * 1024)`
instead of `10 * 1024 * 1024`). NEW uses 25MB. See Exception 1.

---

## 6. Approve Flow (Staff)

1. Staff clicks **Approve** button on a document row.
2. No confirmation modal -- single-click action.
3. `POST /api/app/appointments/{id}/documents/{docId}/approve`.
4. Document status badge changes to Approved (green).
5. Approve button disappears (already approved).

NEW source: `appointment-documents.component.ts:199-205`

---

## 7. Reject Flow (Staff)

1. Staff clicks **Reject** button on a document row.
2. Rejection reason modal opens:

```
+---------------------------------------------+
| Reject Document                       [X]   |
+---------------------------------------------+
| Rejection Reason *                          |
| [textarea -- 4 rows, maxlength 500]         |
|   {placeholder}                  X / 500    |
+---------------------------------------------+
| [Cancel]              [Reject]              |
+---------------------------------------------+
```

3. Reject button enabled when `reason.trim().length > 0 && reason.length <= 500`.
4. Submit: `POST /api/app/appointments/{id}/documents/{docId}/reject` with body `{ reason }`.
5. Document status badge changes to Rejected (red).
6. Rejection reason appears below document name in red text.
7. External user sees rejection reason on their appointment-view page.

**State management:**
- `rejectingDoc: AppointmentDocumentDto | null` tracks which doc is in the modal.
- `rejectionReason: string` bound to textarea.
- `isSubmittingReject: boolean` prevents double-submit.
- `closeRejectModal()` resets both to null/empty.

NEW source: `appointment-documents.component.ts:208-247`,
`appointment-documents.component.html:124-163`

---

## 8. API Endpoints

| Action | Method + Path | Body |
|---|---|---|
| List documents | `GET /api/app/appointments/{id}/documents` | -- |
| Upload document | `POST /api/app/appointments/{id}/documents` | FormData (file + documentName) |
| Delete document | `DELETE /api/app/appointments/{id}/documents/{docId}` | -- |
| Approve document | `POST /api/app/appointments/{id}/documents/{docId}/approve` | -- |
| Reject document | `POST /api/app/appointments/{id}/documents/{docId}/reject` | `{ reason: string }` |
| Regenerate packet | `POST /api/app/appointments/{id}/packet/regenerate` | -- |

NEW source: `proxy/appointment-documents/appointment-document.service.ts:12-79`

---

## 9. Document Status Model

```typescript
// models.ts (NEW proxy, do NOT edit)
export enum AppointmentDocumentStatus {
  Uploaded = 1,
  Approved = 2,
  Rejected = 3
}
```

OLD enum also included Pending (4) and Deleted. NEW removes Pending (documents go
directly to Uploaded) and handles deletion via the DELETE endpoint.

---

## 10. Role Visibility Matrix

| Role | Upload | Download | Approve | Reject | Delete |
|---|---|---|---|---|---|
| Patient | Yes (own appointment) | Yes | No | No | Yes (own uploads) |
| Adjuster / Attorney / Claim Examiner | Yes | Yes | No | No | Yes (own uploads) |
| Clinic Staff | No (staff don't upload patient docs) | Yes | Yes | Yes | Yes |
| Staff Supervisor | No | Yes | Yes | Yes | Yes |
| IT Admin | Yes | Yes | Yes | Yes | Yes |

`canApprove` computed from `CaseEvaluation.AppointmentDocuments.Approve` permission grant.

---

## 11. Packet Regeneration

After documents are approved/rejected, the appointment packet (PDF assembly of all
approved documents) may need to be regenerated. The `documentsChanged` output event
triggers `packetPanel.refresh()` in the parent appointment-view component.

Staff can also manually trigger packet regeneration via
`POST /api/app/appointments/{id}/packet/regenerate`.

This is covered in detail in `external-user-appointment-package-documents-design.md`.

---

## 12. Branding Tokens

| Element | Token |
|---|---|
| Upload button | `btn-primary` via `--brand-primary` |
| Approve button | `btn-success` via `--status-approved` |
| Reject button | `btn-danger` via `--status-rejected` |
| Status badge -- Uploaded | `--status-pending` background (yellow) |
| Status badge -- Approved | `--status-approved` background (green) |
| Status badge -- Rejected | `--status-rejected` background (red) |
| Rejection reason text | `--status-rejected` color (red) |
| Rejection reason counter (over limit) | `--status-rejected` color (red) |

Token definitions: `_design-tokens.md`.

---

## 13. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Page location | Separate full page (`/appointment-new-documents/:id`) | Embedded component in appointment-view page |
| Approve/Reject workflow | Not implemented (no approve/reject buttons) | Full approve with single-click + reject with reason modal |
| File size limit | ~1MB (bug: `1000 * 1024` instead of `10 * 1024 * 1024`) | 25MB |
| File encoding | `FileReader.readAsBinaryString` + `btoa()` (base64 in body) | `FormData` multipart upload |
| Extension restriction | `.doc/.docx/.pdf` only | No client-side extension restriction (server validates) |
| API pattern | `PUT /api/Appointments/{id}/AppointmentDocuments/{docId}` (PATCH-style) | RESTful + action routes (`/approve`, `/reject`) |
| Role check | UI conditional in list template | `CaseEvaluation.AppointmentDocuments.Approve` permission |
| Modal framework | `RxPopup` / `RxDialog` (in-house) | Custom CSS backdrop with Angular `@if` |
| Rejection reason storage | Not applicable (no rejection) | Stored on `AppointmentDocument.RejectionReason` |

---

## 14. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | File size limit | ~1MB actual cap (bug: `1000 * 1024`) | 25MB | Old behavior was a known bug (toast said 10MB, actual cap was ~1MB). NEW fixes to practical upload size |
| 2 | File encoding method | Base64 string in request body | FormData multipart | Standard modern upload pattern; no behavioral regression for end user |
| 3 | Extension restriction | `.doc/.docx/.pdf` only (client-side) | No client-side restriction | Server-side validation handles this in NEW; omitting strict extension gate prevents user frustration with valid files |
| 4 | Approve/Reject workflow | Does not exist; documents have an `Accepted` status value in enum but no UI to set it | Full approve/reject with dedicated modals and endpoints | NEW-only feature; critical for the document review flow described in the feature brief |
| 5 | Separate document page | "Upload Documents" navigates away to `/appointment-new-documents/:id` | Inline embedded component; no page navigation | Architecture improvement; documents stay in appointment context |
| 6 | Pending (4) document status | `Pending` status in OLD enum | Removed from NEW; documents go directly to `Uploaded` | Simplification; Pending was never used in any UI action in OLD |

---

## 15. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `list/appointment-document-list.component.html` | 1-89 | Document list table with delete/re-upload actions |
| `list/appointment-document-list.component.ts` | 1-139 | List logic, role-based UI conditional |
| `edit/appointment-document-edit.component.ts` | 1-155 | Re-upload modal: file validation, base64 encoding |
| `appointment-documents.service.ts` | 1-75 | API: GET/POST/PUT/DELETE on `/api/Appointments/{id}/AppointmentDocuments` |
| `domain/appointment-document.domain.ts` | 1-66 | Base class with user type logic |

---

## 16. Verification Checklist

- [ ] Staff navigates to appointment-view page and sees the Appointment Documents section
- [ ] Document list shows all uploaded documents with status badges (Uploaded/Approved/Rejected)
- [ ] Approve and Reject buttons are visible to Clinic Staff / Supervisor / IT Admin
- [ ] Approve and Reject buttons are NOT visible to external users
- [ ] Clicking Approve changes document status badge to Approved (green) immediately
- [ ] Approve button disappears once a document is already Approved
- [ ] Clicking Reject opens the rejection reason modal
- [ ] Reject button in modal is disabled when reason is empty or over 500 chars
- [ ] Character counter shows "X / 500" and turns red when over limit
- [ ] Submitting reject changes status badge to Rejected (red)
- [ ] Rejection reason appears below the document name in red text
- [ ] External user sees the rejection reason on their appointment-view page
- [ ] Download button is visible to all authenticated users
- [ ] Delete button removes the document after confirmation
- [ ] External user uploads a file up to 25MB successfully
- [ ] File name is auto-populated from the selected file (editable before submit)
- [ ] Upload shows spinner while in progress; list refreshes on completion
- [ ] `documentsChanged` event triggers packet panel refresh in parent page
- [ ] Non-permitted roles cannot call approve/reject endpoints (403)
