---
feature: external-user-appointment-package-documents
date: 2026-05-04
phase: 2-frontend (backend UploadPackageDocumentAsync + auto-queue done; Angular AppointmentDocumentsComponent handles display)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointment-documents/
old-components:
  - list/appointment-document-list.component.ts + .html (structured package doc list, 139/89 lines)
  - add/appointment-document-add.component.ts + .html (upload per document slot)
new-feature-path: angular/src/app/appointment-documents/appointment-documents.component.ts + angular/src/app/appointment-packet/appointment-packet.component.html
shell: external-user-authenticated (top-bar; embedded in appointment-view page)
screenshots: pending
---

# Design: External User -- Appointment Package Documents

## Overview

Package documents are the **required** documents the external user must upload as part
of their appointment. When an appointment is approved, the staff-assigned document package
(set of required forms/docs tied to the appointment type) is automatically queued against
the appointment. Each document in the package gets a `Pending` status and a due date.

External users upload each required document via the appointment-view page. The same
embedded `AppointmentDocumentsComponent` used for document review (see
`clinic-staff-document-review-design.md`) displays the document list and handles uploads.
A second embedded component, `AppointmentPacketComponent`, shows the assembled PDF
packet status after all required documents are uploaded and approved.

**Backend fully implemented.** UI already exists in `AppointmentDocumentsComponent`.
This doc covers the UI contract, upload gates, and role visibility.

---

## 1. Route

No dedicated route. The document upload UI is embedded at `/appointments/view/:id` as:

```html
<app-appointment-documents [appointmentId]="id" />
<app-appointment-packet [appointmentId]="id" />
```

Both components are embedded below the Custom Fields section in the appointment-view page.

---

## 2. Shell

External-user authenticated shell when the external user is uploading.
Internal-user authenticated shell when staff are reviewing (same embedded component,
different action button visibility based on `canApprove` permission).

---

## 3. OLD Document List (Package Documents)

In OLD, external users accessed package documents via a separate full page at
`/appointment-documents/:appointmentId`:

```
+-------------------------------------------------------+
| [H2] Appointment Documents        [Back]             |
+-------------------------------------------------------+
| [Per-document rows from the assigned package]        |
| Document Name | Required/Optional | Status | Due Date |
| [Upload button per row]                              |
+-------------------------------------------------------+
| [Packet section -- below document list]              |
| "Doctor Packet" -- download link (if generated)      |
+-------------------------------------------------------+
```

Each row represents a required document from the package template. External user uploads
a file against each slot. Statuses: Pending / Uploaded / Accepted / Rejected.

OLD source: `appointment-documents/list/appointment-document-list.component.html:1-89`

---

## 4. NEW Document List + Upload

The `AppointmentDocumentsComponent` displays all documents (package + ad-hoc) in one
unified list. Package documents are distinguished by having `IsAdHoc = false` and a
linked `DocumentPackageId`.

```
+-------------------------------------------------------+
| Appointment Documents                                |
+-------------------------------------------------------+
| [Upload section]                                     |
|   [Choose File]  {fileName}   Document Name [text]  |
|   [Upload]                                           |
+-------------------------------------------------------+
| [Document list -- package docs + ad-hoc mixed]       |
| [Badge] Document Name                                |
| Uploaded by: {name}  Date: {date}                    |
| [If Rejected: reason text in red]                    |
| [Download]  [Approve*]  [Reject*]  [Delete]          |
| (* staff only, gated by canApprove permission)        |
+-------------------------------------------------------+
```

Status badges: Pending (yellow), Uploaded (yellow), Approved (green), Rejected (red).

NEW source: `appointment-documents/appointment-documents.component.html:1-164`

---

## 5. Upload Gates (Package Documents)

An external user can upload a package document **only if**:

1. Appointment status is **Approved** or **RescheduleRequested**.
2. Current date is **before the document's DueDate**.
3. The document is **not already Accepted** (cannot replace an accepted document).

If any gate fails, the upload is rejected by the backend with an appropriate error message.
The frontend shows a toast error on upload failure.

Backend gate: `DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate()` +
`DocumentUploadGate.EnsureNotImmutable()`.

Parity audit: `docs/parity/external-user-appointment-package-documents.md`

---

## 6. Doctor Packet Component

Below the document list, the `AppointmentPacketComponent` shows the assembled PDF packet:

```
+-------------------------------------------------------+
| [H4] Doctor Packet                    [Refresh icon] |
+-------------------------------------------------------+
| [No packet yet]                                      |
| "No packet has been generated yet. The packet is     |
|  generated automatically when the appointment is     |
|  approved."                                          |
| [Generate Packet] (if canRegenerate)                 |
+-------------------------------------------------------+
| [Packet exists -- status: Generated]                 |
| [PDF icon]  Appointment Packet  [Generated badge]    |
|   Generated {date}                                   |
|   [Download]  [Regenerate] (if canRegenerate)        |
+-------------------------------------------------------+
| [Packet exists -- status: Generating]                |
| [PDF icon]  Appointment Packet  [Generating badge]   |
|   "Generation in progress. This page will refresh    |
|    automatically."  [Spinner]                        |
+-------------------------------------------------------+
| [Packet exists -- status: Failed]                    |
| [PDF icon]  Appointment Packet  [Failed badge]       |
|   "Generation failed. Re-upload corrupt source       |
|    documents and click Regenerate."                  |
|   [Error message text]                               |
|   [Regenerate] (if canRegenerate)                    |
+-------------------------------------------------------+
```

**Packet generation trigger:** Automatic on appointment approval. Manual "Generate Packet"
button available to users with `canRegenerate` permission (staff). External users see
the packet status and can Download once generated.

**Polling:** While status is Generating, the component polls every 5 seconds automatically.

**canRegenerate:** True for staff (Clinic Staff / Supervisor / IT Admin) who have
`CaseEvaluation.AppointmentPacket.Regenerate` permission.

NEW source: `appointment-packet/appointment-packet.component.html:1-99`

---

## 7. Verification Code (Email-Link Upload)

External users can also upload documents via an email link (no login required). Each
document gets a unique `VerificationCode` GUID. The email contains a link like:
`/upload?code={verificationCode}`.

This is a backend-driven feature. The frontend endpoint is
`POST /api/public/appointment-documents/upload-by-code` (unauthenticated). The linked
UI surface for this anonymous upload is minimal (just a file input + document name + submit).

---

## 8. Role Visibility Matrix

| Role | See package docs | Upload | Download | Approve/Reject | Packet download |
|---|---|---|---|---|---|
| Patient / Adjuster / Attorney | Yes (own appointment) | Yes (if gates pass) | Yes | No | Yes (once generated) |
| Clinic Staff | Yes | No (staff don't upload for patient) | Yes | Yes | Yes |
| Staff Supervisor | Yes | No | Yes | Yes | Yes |
| IT Admin | Yes | Yes | Yes | Yes | Yes + Regenerate |

---

## 9. Branding Tokens

| Element | Token |
|---|---|
| Section heading | `--text-primary` |
| Upload button | `btn-primary` via `--brand-primary` |
| Status badge -- Pending/Uploaded | `--status-pending` (yellow) |
| Status badge -- Approved | `--status-approved` (green) |
| Status badge -- Rejected | `--status-rejected` (red) |
| Packet status -- Generated | `--status-approved` (green) |
| Packet status -- Generating | `--status-pending` (yellow/blue) |
| Packet status -- Failed | `--status-rejected` (red) |
| Download button | `btn-primary` |
| Regenerate button | `btn-outline-primary` |

---

## 10. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Separate page | Dedicated `/appointment-documents/:id` page | Embedded in appointment-view page | Architecture improvement; keeps documents in appointment context |
| 2 | Package docs separate from ad-hoc | Two separate entity types and separate pages | Unified `AppointmentDocument` with `IsAdHoc` flag; shown in same list | Simplification; both doc types appear together with visual distinction |
| 3 | File size limit | ~1MB (OLD bug) | 25MB | Fix of known bug (see `clinic-staff-document-review-design.md` Exception 1) |
| 4 | Unauthenticated upload via email link | Per-document `VerificationCode` in email | Same behavior; `UploadByVerificationCodeAsync` endpoint | Strict parity maintained |
| 5 | Packet format | Word document (.docx) assembled from templates | PDF assembled from approved documents | Per CLAUDE.md: DOCX -> PDF is an approved deviation (PDFs are immutable) |

---

## 11. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `appointment-documents/list/appointment-document-list.component.html` | 1-89 | Package document list with upload/delete actions |
| `appointment-documents/list/appointment-document-list.component.ts` | 1-139 | List logic, role-based visibility |
| `appointment-documents/add/appointment-document-add.component.ts` | 1-158 | Upload per document slot |
| `docs/parity/external-user-appointment-package-documents.md` | all | Full parity audit: auto-queue, VerificationCode, gates, reminders |

---

## 12. Verification Checklist

- [ ] Package documents are auto-queued when appointment is approved
- [ ] Each required document appears with Pending status on the external user's view page
- [ ] External user uploads a file for each document slot (Choose File + document name + Upload)
- [ ] Upload is rejected if appointment status is not Approved/RescheduleRequested (gate check)
- [ ] Upload is rejected if current date is past the document's DueDate
- [ ] Upload is rejected if the document is already Accepted (immutable)
- [ ] Uploaded document status changes to Uploaded (yellow badge)
- [ ] Staff approves document: status changes to Approved (green badge)
- [ ] Staff rejects document: status changes to Rejected (red badge) + reason shown in red
- [ ] External user receives email notification on approval/rejection
- [ ] Doctor Packet section shows "No packet yet" before appointment approval
- [ ] Packet is auto-generated on appointment approval; Generating spinner appears
- [ ] Packet status shows Generated when complete; Download button appears
- [ ] Packet shows Failed status with error message if generation fails
- [ ] "Regenerate" button visible to staff; "Generate Packet" button visible when no packet exists
- [ ] Packet polling auto-refreshes while status is Generating (every 5 sec)
- [ ] Unauthenticated email-link upload works via VerificationCode URL parameter
