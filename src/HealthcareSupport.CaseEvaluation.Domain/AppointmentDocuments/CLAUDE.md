# AppointmentDocuments -- uploaded files and generated PDF packets

Two aggregate roots, two managers. Blobs for both live in containers declared in
`BlobContainers/` (full list in the Domain layer CLAUDE.md).

## What lives here

| File | Purpose |
|---|---|
| `AppointmentDocument.cs` | Uploaded-file aggregate; `CreateQueued()` static factory |
| `AppointmentDocumentManager.cs` | Upload guard (`CreateAsync`) + queued-row factory (`CreateQueuedAsync`) + replaced-file record (`RecordReplacedFileAsync`) |
| `AppointmentDocumentVersion.cs` | Append-only record of a file an upload replaced; the only place its storage key and metadata survive |
| `AppointmentPacket.cs` | Generated-PDF aggregate; one row per (TenantId, AppointmentId, Kind) |
| `AppointmentPacketManager.cs` | `EnsureGeneratingAsync` / `MarkGeneratedAsync` / `MarkFailedAsync` |
| `Jobs/GenerateAppointmentPacketJob.cs` | Hangfire async job; renders HTML via the WeasyPrint packet-renderer sidecar -> fillable PDF |
| `Handlers/PacketGenerationOnApprovedHandler.cs` | Subscribes to `AppointmentStatusChangedEto`; enqueues job on UoW commit |
| `Templates/` | Token map, context + resolver shared by the HTML renderer (the templates themselves live in the packet-renderer sidecar, not here) |
| `Pdf/WeasyPrintPacketRenderer.cs` | HTTP call to the packet-renderer sidecar (HTML -> fillable PDF); transport failures propagate for Hangfire retry |
| `IPacketAttachmentProvider.cs` | Email-side surface for the packet generator: the email session fetches attachment bytes here and reports back via `NotifySendCompletedAsync`. Also declares `PacketAttachment` (bytes, file name, content type) |
| `PacketAttachmentProvider.cs` | Default implementation. Reads the (appointment, kind) row, streams the blob, and builds the OLD-style filename `{ConfirmationNumber}_{KindName}_{ddMMyyyy_hhmmss}.pdf` |
| `JointDeclarationCutoff.cs` | Pure cutoff predicate for the JDF overdue job (Phase 14, 2026-05-04): an AME appointment with no uploaded JDF is overdue when its due date is within `SystemParameter.JointDeclarationUploadCutoffDays` of today. No DI, no IO, so it is unit-testable away from the job |
| `RequiredDocumentEvaluator.cs` | Works out which required documents are still outstanding. Declares `MissingRequiredDocument` -- one outstanding master `Document` the active package template expects, with its current state. `DocumentId` is internal; the UI shows `Name` |

## Enums (all in `Domain.Shared/AppointmentDocuments/`)

`DocumentStatus` -- `Uploaded=1 / Accepted=2 / Rejected=3 / Pending=4`. Internal-user
uploads land as Accepted; external uploads land as Uploaded pending office review; queued
rows start as Pending.

`PacketKind` -- `Patient=1 / Doctor=2 / AttorneyClaimExaminer=3`. All three are generated
for every appointment type (gate removed 2026-05-29). Doctor kind is generated and stored
but never emailed -- mirrors OLD asymmetry. The legacy file that carried the `DoctorPacket`
email-send logic was AppointmentDocumentDomain.cs, which belongs to OLD and does NOT exist in
this repository; it is written without backticks for that reason, since a backticked path here
is a claim CI verifies.

`PacketGenerationStatus` -- `Generating=1 / Generated=2 / Failed=3`. UI shows spinner /
Download button / error + Regenerate button respectively.

## Conventions

### AppointmentDocument: queued-row path

`CreateQueuedAsync` (manager) calls `AppointmentDocument.CreateQueued()` (static factory),
which sets `Status = Pending`, `VerificationCode = Guid.NewGuid()`, and placeholder
sentinels `"(pending-upload)"` for `BlobName` and `FileName`. On patient upload via
`UploadByVerificationCodeAsync`, the AppService overwrites the placeholder fields and flips
status to Uploaded. Do NOT attempt a blob fetch on any row where `Status == Pending`; the
`BlobName` is a sentinel, not a real blob key.

The `VerificationCode` lets the patient upload through an emailed link without an
authenticated session. Anonymous uploads require an explicit `_currentTenant.Change(tenantId)`
scope -- see the Domain layer CLAUDE.md blob-container section for why.

PR4: that link points at the guard-free SPA page `public/document-upload/:id/:verificationCode`
(Angular `eLayoutType.empty`, no auth guard) which POSTs to the existing `[AllowAnonymous]`
`PublicDocumentUploadController` (per-code 5/hr rate-limited). `IAccountUrlBuilder.BuildPublicDocumentUploadUrlAsync`
composes the tenant-aware link; wiring it into a request email is deferred (the future email
handler calls it). Restores the legacy "Click here to upload" flow.

### AppointmentDocument: a replaced file is kept, never deleted

Documents are kept seven years, so no upload path deletes the file it replaces. A re-upload
overwrites the document row's file fields in place, so before it does, `RecordReplacedFileAsync`
writes an `AppointmentDocumentVersion` holding the old storage key and metadata (including the
rejection reason the re-upload clears). Without that row the kept object is an orphan nobody can
find. A queued row's placeholder is not a file and is never recorded.

Order in the upload path is load-bearing: store the new file, THEN record the old one, THEN
reassign. The upload scanner refuses inside the store, so a refused file leaves no record and no row
naming a file that was never stored. The record joins the same unit of work, so a failed commit
rolls it back and the compensating delete removes only the new file. Do not reintroduce a delete of
the replaced blob: it used to run before the commit, and a failed commit then lost both files.

### Replacing a rejected document (2026-10-14)

"Upload replacement" on a rejected row calls `ReplaceRejectedDocumentAsync`
(`POST api/app/appointments/{aid}/documents/{id}/replace`). It replaces the file IN PLACE, as OLD
did (parity-v2 E6), for any kind of row (ad-hoc, package, joint declaration), so the row keeps its
`SourceDocumentId` and a replaced required document counts once accepted. It goes through the same
`OverwriteUploadedFileAsync` (version row, scanner in the store), the read guard and the status gate.
`DocumentUploadGate.EnsureRejected` keeps it to Rejected rows; `EnsurePackageRow` now keeps the
package path to package rows. The status gate also admits InfoRequested, because a staff send-back
can flag "documents" to ask for exactly this. Before this, the UI's only fix was a NEW upload: no
version row, the rejected row stayed Rejected, and the requirement stayed outstanding.

### Document-type + required-document tracking (PR2/PR3)

`AppointmentDocument` carries three nullable columns from the document-type feature:
`AppointmentDocumentTypeId` (chosen admin category), `OtherDocumentTypeName` (free-text
label when the uploader picks "Other"), and `SourceDocumentId` (the master `Document` a
queued package row was generated from; null for ad-hoc uploads).

The **missing-required-documents** read
(`AppointmentDocumentsAppService.GetMissingRequiredDocumentsAsync`) defines "required" as
the active `PackageDetail`(s) for the appointment's type (union of their active
`DocumentPackage` -> active `Document` rows). A requirement is **satisfied** only when the
appointment has an `AppointmentDocument` whose `SourceDocumentId` matches the master
Document id AND whose `Status == Accepted`; otherwise it is reported in its most-actionable
state (`RequiredDocumentState`: AwaitingReview > Rejected > NotUploaded). The pure matching

+ state logic lives in `RequiredDocumentEvaluator` and is unit-tested; the AppService only
fetches the inputs and wraps them in `MissingRequiredDocumentsResultDto` (required count +
missing list) so the UI can distinguish "no package", "all received", and "outstanding".
The "required docs for a type" resolution is duplicated with `PackageDocumentQueueHandler`
(flagged tech-debt: consolidate when next touched).

### AppointmentPacket: generation lifecycle

`EnsureGeneratingAsync` is idempotent: if a row already exists for
(AppointmentId, Kind) it flips it back to Generating (regenerate path). IMPORTANT: always
call `EnsureGeneratingAsync` before writing the blob so `MarkFailedAsync` has a row to
update if the render or PDF conversion throws.

Packet job enqueue must happen in `OnCompleted` of the approve UoW, not directly inside
it -- Hangfire's `IBackgroundJobManager` enqueues immediately and can dequeue before the
approve commit. `PacketGenerationOnApprovedHandler` already does this; match the pattern
for any new job-enqueue paths.

### PDF replaces DOCX (parity note)

OLD generated `.docx` reports. NEW renders HTML templates (owned by the WeasyPrint
packet-renderer sidecar) to a fillable `.pdf` before persisting; the legacy
DOCX -> Gotenberg path was removed 2026-06-10. The immutability reason: recipients
cannot edit a PDF. Report business logic (data shown, role access, column layout)
still matches OLD exactly.

### Blob containers used

+ `appointment-documents` -- uploaded files.
+ `appointment-packets` -- generated PDF packets.
+ `anonymous-uploads` -- temporary landing zone for unauthenticated patient uploads before
  they are moved to `appointment-documents`.

## Gotchas

+ `PacketGenerationStatus` enum starts at 1 to avoid the `default(int) = 0` trap
  mapping an unset enum to a valid value. Do not add a `0` member.
+ `GenerateAppointmentPacketJob` catches `IOException | InvalidOperationException |
  ArgumentException | AbpDbConcurrencyException` per kind and calls `MarkFailedAsync`
  WITHOUT rethrowing, so one failing kind does not block the others. Transport failures
  from the packet-renderer sidecar are NOT caught -- they propagate and let Hangfire
  retry the whole job.
+ Composite unique index `(TenantId, AppointmentId, Kind)` on `AppointmentPackets` is
  filtered; `EnsureGeneratingAsync` relies on it for safe upsert.
+ `PacketGeneratedEto` publish is deferred to `UoW.OnCompleted` inside the job to avoid
  the email handler querying the packet row before `MarkGeneratedAsync` commits.

## Related

+ docs/business-domain/APPOINTMENT-LIFECYCLE.md
+ docs/parity/_parity-flags.md
