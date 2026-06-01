# AppointmentDocuments -- uploaded files and generated PDF packets

Two aggregate roots, two managers. Blobs for both live in containers declared in
`BlobContainers/` (full list in the Domain layer CLAUDE.md).

## What lives here

| File | Purpose |
|---|---|
| `AppointmentDocument.cs` | Uploaded-file aggregate; `CreateQueued()` static factory |
| `AppointmentDocumentManager.cs` | Upload guard (`CreateAsync`) + queued-row factory (`CreateQueuedAsync`) |
| `AppointmentPacket.cs` | Generated-PDF aggregate; one row per (TenantId, AppointmentId, Kind) |
| `AppointmentPacketManager.cs` | `EnsureGeneratingAsync` / `MarkGeneratedAsync` / `MarkFailedAsync` |
| `Jobs/GenerateAppointmentPacketJob.cs` | Hangfire async job; renders DOCX template then Gotenberg -> PDF |
| `Handlers/PacketGenerationOnApprovedHandler.cs` | Subscribes to `AppointmentStatusChangedEto`; enqueues job on UoW commit |
| `Templates/` | Embedded `.docx` templates (PatientPacketNew, DoctorPacket, AttorneyClaimExaminer) |
| `Pdf/GotenbergDocxToPdfConverter.cs` | HTTP sidecar call; transport failures propagate for Hangfire retry |

## Enums (all in `Domain.Shared/AppointmentDocuments/`)

`DocumentStatus` -- `Uploaded=1 / Accepted=2 / Rejected=3 / Pending=4`. Internal-user
uploads land as Accepted; external uploads land as Uploaded pending office review; queued
rows start as Pending.

`PacketKind` -- `Patient=1 / Doctor=2 / AttorneyClaimExaminer=3`. All three are generated
for every appointment type (gate removed 2026-05-29). Doctor kind is generated and stored
but never emailed -- mirrors OLD asymmetry (search `AppointmentDocumentDomain.cs` for
`DoctorPacket` email-send logic).

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

OLD generated `.docx` reports. NEW renders DOCX templates then converts via Gotenberg
(`IDocxToPdfConverter`) before persisting. Blob extension is `.pdf`. The immutability
reason: recipients cannot edit a PDF. Report business logic (data shown, role access,
column layout) still matches OLD exactly.

### Blob containers used

- `appointment-documents` -- uploaded files.
- `appointment-packets` -- generated PDF packets.
- `anonymous-uploads` -- temporary landing zone for unauthenticated patient uploads before
  they are moved to `appointment-documents`.

## Gotchas

- `PacketGenerationStatus` enum starts at 1 to avoid the `default(int) = 0` trap
  mapping an unset enum to a valid value. Do not add a `0` member.
- `GenerateAppointmentPacketJob` catches `IOException | InvalidOperationException |
  ArgumentException | AbpDbConcurrencyException` per kind and calls `MarkFailedAsync`
  WITHOUT rethrowing, so one failing kind does not block the others. Transport failures
  from Gotenberg are NOT caught -- they propagate and let Hangfire retry the whole job.
- Composite unique index `(TenantId, AppointmentId, Kind)` on `AppointmentPackets` is
  filtered; `EnsureGeneratingAsync` relies on it for safe upsert.
- `PacketGeneratedEto` publish is deferred to `UoW.OnCompleted` inside the job to avoid
  the email handler querying the packet row before `MarkGeneratedAsync` commits.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- docs/parity/_parity-flags.md
