---
feature: document-upload-download
date: 2026-05-04
phase: cross-cutting (covered per-context in three dedicated design docs)
status: cross-reference
old-source: see referenced docs below
new-feature-path: angular/src/app/appointment-documents/appointment-documents.component.ts
shell: cross-cutting
screenshots: n/a
---

# Design: Document Upload / Download (Cross-Cutting Primitive)

## This is a cross-cutting primitive, not a standalone feature

Document upload and download functionality is documented in the per-context design
docs where it actually appears. This stub prevents `_status.md` from marking the
row as "not-started" when the work is captured elsewhere.

## Where the behavior is documented

| Context | Design doc | Key details |
|---|---|---|
| Package documents (structured, auto-queued) | `external-user-appointment-package-documents-design.md` | Upload gates, DueDate, Accepted/Rejected, AppointmentPacketComponent |
| Ad-hoc documents | `external-user-appointment-ad-hoc-documents-design.md` | `IsAdHoc=true` flag, no status gate, UploadStreamAsync |
| Joint Declaration Form | `external-user-appointment-joint-declaration-design.md` | AME/AME-REVAL only, attorney upload gate, auto-cancel job |
| Document review (internal) | `clinic-staff-document-review-design.md` | Approve/reject workflow, rejection reason modal |

## NEW unified component

All four upload contexts share a single Angular component:
`angular/src/app/appointment-documents/appointment-documents.component.ts`

Behavior switches via `IsAdHoc` and `IsJointDeclaration` input flags.
API: `POST /api/app/appointments/{id}/documents` (multipart FormData; max 25 MB).

## Packet generation (AppointmentPacketComponent)

Document packet status (Generated / Generating / Failed) is documented in
`external-user-appointment-package-documents-design.md` Section 4.

Source: `angular/src/app/appointment-packet/appointment-packet.component.ts`
