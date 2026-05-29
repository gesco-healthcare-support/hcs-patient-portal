---
feature: document-upload-at-request
date: 2026-05-29
status: in-progress
base-branch: main
related-issues: []
sequence: 4 of 6 (backend; independent of the booking-form cluster)
branch: feat/document-upload-at-request
---

## Goal

Let external users upload documents as soon as an appointment is REQUESTED
(status `Pending`), not only after it is `Approved`:
- (A) Open the three gated upload paths (package-doc, joint-declaration,
  anonymous verification-code) at `Pending`.
- (B) Create the per-appointment "package document" rows at SUBMISSION time so a
  Pending appointment has the required-document rows to upload against.
- (C) Keep the due-date gate. (D) Leave the anonymous verification-code email
  timing unchanged.

## Context

Adrian directive: "Users need to upload a lot of documents to get the
appointment approved, so it doesn't make sense they can only upload after
approval." All three gated paths open at request time; package rows exist from
submission.

### Verified current behavior (code map + LIVE UI, 2026-05-29)

**The gate (server-side only).** `DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate`
(`src/.../Application/AppointmentDocuments/DocumentUploadGate.cs:55-60`) allows
only `Approved` (2) and `RescheduleRequested` (12):
```
if (status != AppointmentStatusType.Approved &&
    status != AppointmentStatusType.RescheduleRequested)
    throw new BusinessException(CaseEvaluationDomainErrorCodes.DocumentUploadAfterApproval);
```
- Error code `CaseEvaluation:Document.UploadAfterApproval`; en.json message
  "Please upload documents after appointment is approved." It is a
  `BusinessException` mapped to ABP's error envelope -- **no server log line is
  emitted** for this case (the gate is a pure static helper with no ILogger).
- A separate due-date check (`:61-65`) throws `DocumentUploadAfterDueDate`
  ("You can not upload document after specified due date.") -- kept as-is.

**Upload paths** (`AppointmentDocumentsAppService.cs`):

| Method | Permission | Gated? |
|---|---|---|
| `UploadStreamAsync` (ad-hoc) | `AppointmentDocuments.Create` | No (intentionally gate-free) |
| `UploadPackageDocumentAsync` | `AppointmentDocuments.Create` | Yes (`:261`) |
| `UploadJointDeclarationAsync` | `AppointmentDocuments.Create` | Yes (`:321`) |
| `UploadByVerificationCodeAsync` | `[AllowAnonymous]` | Yes (`:387`) |

The single gate change opens all three gated paths at once; ad-hoc already works
at any status.

**Package-doc rows are created at approval.** `PackageDocumentQueueHandler`
(`src/.../Application/Notifications/Handlers/PackageDocumentQueueHandler.cs:41`)
subscribes to `AppointmentApprovedEto` and inserts one `AppointmentDocument`
(`Status=Pending`, fresh `VerificationCode`, placeholder blob) per active
`PackageDetail` for the appointment type. `CreateQueuedAsync` does a plain
`InsertAsync` with no uniqueness guard.

**Submission event lacks the type.** `AppointmentSubmittedEto`
(`src/.../Domain.Shared/Appointments/AppointmentSubmittedEto.cs`) carries
AppointmentId, TenantId, BookerUserId, PatientId, RequestConfirmationNumber,
AppointmentDate, SubmittedAt -- but NOT `AppointmentTypeId`. It is published in
`AppointmentsAppService.CreateAsync` (~`:830-837`), where `appointment` (with
`AppointmentTypeId`) is in scope.

**Angular has no status gate.** `appointment-documents.component` renders the
upload UI unconditionally; the only block is server-side. No Angular change is
needed.

### Live UI observation (A00004, Pending QME)

The Pending appointment's view shows:
- a "Documents" ad-hoc upload form (works at Pending -- gateless) with "No
  documents uploaded yet";
- "Appointment Packets: No packets have been generated yet. Packets are
  generated automatically when the appointment is approved.";
- NO required/package-document list -- those rows only appear after approval.

So today a Pending appointment exposes only ad-hoc upload; the structured
required-document flow is unavailable until approval -- the gap F3 closes.

(Note: a stale browser-cache lazy-chunk MIME error was seen when first opening
the page after the earlier Angular rebuild -- an environment artifact, fixed by
a hard reload, NOT an F3 issue.)

### Parity

OLD also blocked upload until approval (`AppointmentDocumentDomain.cs:90-107`).
Opening at `Pending` is a deliberate deviation from OLD -- record a PARITY-FLAG.

## Approach

1. **Gate (A):** add `AppointmentStatusType.Pending` to the allowed set in
   `DocumentUploadGate.cs:55`. One line; keeps the due-date check.
2. **Package rows at submission (B):**
   - Add `AppointmentTypeId` to `AppointmentSubmittedEto` and pass
     `appointment.AppointmentTypeId` at the `CreateAsync` publish site.
   - Re-point `PackageDocumentQueueHandler` from `AppointmentApprovedEto` to
     `AppointmentSubmittedEto` (same body; reads `AppointmentTypeId` from the new
     ETO).
   - Idempotency: since `CreateQueuedAsync` has no uniqueness guard, ensure rows
     are created exactly once -- add a guard (skip if package rows already exist
     for the appointment) AND remove the approval-time creation so approval can
     never double-insert.
3. **Keep** the due-date gate and the verification-code email schedule
   unchanged; `PacketGenerationOnApprovedHandler` (PDF packets) still runs at
   approval (separate handler -- unaffected).

**Alternatives rejected:**
- Open only some gated paths: Adrian wants all three. Reject.
- Keep creating rows at approval and also at submission: double-insert risk
  (no unique index). Reject -- move creation fully to submission.
- Relax the due-date gate: keep it. Reject.

## Tasks

- T1: Allow upload at `Pending`.
  - approach: tdd
  - files-touched: src/.../Application/AppointmentDocuments/DocumentUploadGate.cs (~55);
    extend test/.../Application.Tests/AppointmentDocuments/DocumentUploadGateUnitTests.cs
  - acceptance: gate permits `Pending`, `Approved`, `RescheduleRequested`;
    rejects other statuses (Rejected, Cancelled...); past-due still blocked.
    New test case for `Pending` added (red before the gate change, green after).

- T2: Create package-document rows at submission time.
  - approach: tdd
  - files-touched:
    - src/.../Domain.Shared/Appointments/AppointmentSubmittedEto.cs (add AppointmentTypeId)
    - src/.../Application/Appointments/AppointmentsAppService.cs (~830: pass AppointmentTypeId)
    - src/.../Application/Notifications/Handlers/PackageDocumentQueueHandler.cs (subscribe AppointmentSubmittedEto; idempotency guard; drop approval-time creation)
  - acceptance: submitting an appointment creates the Pending package-document
    rows immediately (Status=Pending + VerificationCode, tied to the
    appointment); approval does not create or duplicate them. Handler unit test
    covers create-on-submit + the idempotency guard (no duplicates on re-fire).

- T3: Record the OLD-parity deviation.
  - approach: code
  - files-touched: docs/parity/_parity-flags.md (PF-003)
  - acceptance: a row notes upload-at-request as a deliberate deviation from OLD
    (cite AppointmentDocumentDomain.cs:90-107), status needs-test.

## Risk / Rollback

- Blast radius: documents uploadable pre-approval; package rows created earlier
  in the lifecycle. Duplicate-row risk if both submission and approval create
  rows -- mitigated by moving creation fully to submission + the idempotency
  guard. The anonymous path becomes openable at `Pending`, but its
  verification-code email is unchanged (no link is delivered earlier, so nothing
  breaks). Packet PDF generation still happens at approval (separate handler).
- Rollback: revert the PR; gate + queue return to approval-time behavior.

## Verification

Rebuild api + db-migrator (`docker compose up -d --build api db-migrator`), then:
1. Submit a new appointment as an external user -> confirm the Pending
   package-document rows exist immediately (`dbo.AppAppointmentDocuments` rows
   with Status=Pending for the appointment; appointment view shows the
   required-document list while Pending).
2. Upload a package document while the appointment is `Pending` -> succeeds
   (previously blocked).
3. Approve the appointment -> confirm NO duplicate package rows are created.
4. A past-due appointment still blocks upload.
5. xUnit suite green (DocumentUploadGate + queue handler tests).

NOTE: approval (step 3) fires the stakeholder approval emails to real AA/DA/CE
inboxes -- confirm with Adrian before triggering, or verify steps 1-2 + the unit
tests only (which need no approval / no emails).
