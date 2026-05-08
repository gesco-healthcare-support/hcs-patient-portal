---
feature: external-user-appointment-ad-hoc-documents
date: 2026-05-04
phase: 2-frontend (backend UploadStreamAsync with IsAdHoc=true done; same AppointmentDocumentsComponent handles display)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointment-new-documents/
old-components:
  - list/appointment-new-document-list.component.ts + .html (ad-hoc document list)
  - add/appointment-new-document-add.component.ts + .html (upload form)
new-feature-path: angular/src/app/appointment-documents/appointment-documents.component.ts + .html (same component as package docs)
shell: external-user-authenticated (top-bar; embedded in appointment-view page)
screenshots: pending
---

# Design: External User -- Appointment Ad-Hoc Documents

## Overview

Ad-hoc documents are **unstructured uploads** not tied to a specific package document
slot. External users upload any supporting document at any point (no appointment-status
gate, no due-date gate). Examples: additional medical records, prior test results,
physician letters.

In OLD, ad-hoc documents use a completely separate component and page
(`appointment-new-documents`) from the structured package documents
(`appointment-documents`). In NEW, both types are unified in the same
`AppointmentDocumentsComponent` with `IsAdHoc = true` as the distinguishing flag.

This doc covers the ad-hoc-specific upload rules. The shared upload chrome is fully
described in `external-user-appointment-package-documents-design.md`.

---

## 1. Route

No dedicated route. Ad-hoc document upload is embedded at `/appointments/view/:id` as
part of the `<app-appointment-documents>` component -- the same component that handles
package documents.

OLD route: `/appointment-new-documents/:appointmentId` (separate full page, reached via
"Upload Documents" button on the appointment-edit page or list).

---

## 2. OLD Ad-Hoc Document Page

In OLD, the "Upload Documents" button on the appointment-edit page navigated to:
`/appointment-new-documents/:appointmentId`:

```
+-------------------------------------------------------+
| [H2] New Appointment Documents    [Back]             |
+-------------------------------------------------------+
| [Upload section]                                     |
|   File input  [Choose File]                          |
|   Document Name  [text]                              |
|   [Upload]                                           |
+-------------------------------------------------------+
| [Table -- ad-hoc documents]                          |
| Document Name | File Type | Status | Uploaded By |   |
| Uploaded Date | Action (Delete, Re-upload)           |
+-------------------------------------------------------+
```

**Key difference from package docs:** No specific document slot; user uploads whatever
they want. No due-date gate. No package-template requirement.

OLD source: `appointment-new-documents/list/` + `appointment-new-documents/add/`
OLD domain: `AppointmentNewDocumentDomain.cs` (320 lines)

---

## 3. NEW Behavior

In NEW, ad-hoc documents appear in the same `AppointmentDocumentsComponent` list as
package documents. No separate page.

**Upload gate for ad-hoc (minimal):**
- No appointment-status requirement (can upload at any status).
- No due-date gate.
- Max file size: 25MB.
- `IsAdHoc = true` set by `UploadStreamAsync` on the backend.

**Display:** Ad-hoc documents show in the shared document list alongside package documents.
Both types use the same status badge system (Uploaded / Approved / Rejected).

Staff approve/reject ad-hoc documents using the same Approve / Reject buttons as
package documents (see `clinic-staff-document-review-design.md`).

---

## 4. Role Visibility Matrix

| Role | Upload ad-hoc | Download | Approve/Reject |
|---|---|---|---|
| Patient | Yes (any status) | Yes | No |
| Adjuster / Attorney / Claim Examiner | Yes | Yes | No |
| Clinic Staff | No (staff don't upload for patient) | Yes | Yes |
| Staff Supervisor | No | Yes | Yes |
| IT Admin | Yes | Yes | Yes |

---

## 5. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Separate page | Dedicated `/appointment-new-documents/:id` page | Embedded in appointment-view | Architecture improvement |
| 2 | Separate entity | `AppointmentNewDocument` (separate table) | `AppointmentDocument` with `IsAdHoc = true` | Simplification; same query/filter logic; no behavioral regression |
| 3 | File size | ~1MB (OLD bug) | 25MB | Known bug fix |
| 4 | Upload gate | No status gate (upload at any appointment status) | Same: no status gate for ad-hoc | Strict parity maintained |
| 5 | Null ResponsibleUserId bug | OLD threw NRE on `.Value` when `ResponsibleUserId` was null | NEW uses `?.Value` null-safe access | Clear bug fix |

---

## 6. OLD Source Citations

| File | Content |
|---|---|
| `appointment-new-documents/list/appointment-new-document-list.component.html` | Ad-hoc list with delete/re-upload actions |
| `appointment-new-documents/add/appointment-new-document-add.component.ts` | Upload form + file validation |
| `docs/parity/external-user-appointment-ad-hoc-documents.md` | Full parity audit: IsAdHoc flag, no-gate rule, NRE bug fix |

---

## 7. Verification Checklist

- [ ] External user uploads an ad-hoc document at any appointment status (Pending, Approved, Rejected)
- [ ] Ad-hoc document appears in the document list alongside package documents
- [ ] Ad-hoc document has Uploaded status badge immediately after upload
- [ ] No due-date or appointment-status error on ad-hoc upload
- [ ] Staff sees the ad-hoc document and can Approve or Reject it
- [ ] External user receives upload notification email (same as package documents)
- [ ] Delete removes the document from the list
- [ ] Max file size of 25MB enforced; larger files show error toast
