---
feature: Case Tracker document + change feed (integration Parts 2 and 3, merged)
date: 2026-07-28
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Keep the Case Tracker current after a case is created: publish staff-accepted documents and generated
packets, propagate deletions, and re-push the appointment whenever its published data changes.

## Context & decisions

Part 1 (PR #393 -> main `6208a0cd`) delivers the outbox, the intake push on approval, the manual
push, and the retry/dead-letter policy. It ships disabled and has never run against a live endpoint.
This part adds everything that happens AFTER the case exists. Parts 2 and 3 are merged because
today's decisions collapsed them: the same enqueue path serves both, and "re-push on any change"
subsumes the narrower cancel/reschedule trigger Part 3 was scoped to.

Resolved decisions (no open questions remain):
- Decision: documents publish ONLY when staff Accept them, because Case Tracker has no use for
  unvetted uploads and it keeps un-reviewed PHI inside the portal. `AppointmentDocumentUploadedEto`
  is therefore NOT a trigger.
- Decision: reject-after-accept is sent as `{ id, deleted: true }` rather than a Rejected status,
  because their staff should stop seeing a document the portal has repudiated.
- Decision: we always send the deletion signal on reject/delete WITHOUT tracking whether that
  document was previously published, because deleting an id the receiver does not hold is a harmless
  no-op and tracking published-state would duplicate the ledger.
- Decision: packets publish as ONE batch once all three kinds are `Generated`, with a timeout path
  for a permanently `Failed` kind, because a partial packet set is not useful and one kind failing
  must not withhold the other two indefinitely.
- Decision: only the IT-Admin delete path retains its blob; a re-upload deletes the superseded blob
  and publishes the new `objectKey`. Rationale: on re-upload a replacement key always exists, so
  nothing we published is left pointing at nothing; on delete there is no replacement, we promised
  their team retention, and ABP already soft-deletes the row so destroying the blob is inconsistent.
- Decision: the portal re-pushes intake on ANY change to published appointment data, reversing the
  earlier "field edits are pull-only" decision, because relying on the receiver's periodic sweep for
  freshness was overhead they objected to and the idempotent enqueue makes re-pushing nearly free.
- Decision: the change trigger watches `Appointment` and `Patient` ONLY. Reasoning: the payload
  contains appointment scalars, tenant, location, appointment type, schedule, patient, doctor,
  storage and documents -- it carries NO attorney, injury, employer or insurance fields, so edits to
  those cannot change what we publish and need no push. `Location` / `Doctor` / `AppointmentType` /
  slot edits DO appear in the payload but are rare admin actions that fan out to every appointment at
  that location, so they are deliberately not pushed; they reach the receiver via reconcile-on-open.
  ACCEPTED RESIDUAL, flagged for Adrian: renaming a clinic will not immediately refresh existing
  cases.
- Decision: ordering relies on 404-as-retryable (already in the contract) rather than a new
  enqueue-time gate, because the ledger already tracks delivery and intake normally lands in seconds.

## All needed context

Wire contract: `docs/integration/case-tracker-api-contract.md`, including the 2026-07-28 revision
block. Document-update body is a BARE JSON ARRAY (not the `{data,meta,errors}` envelope) posted to
`api/intake/appointments/{appointmentId}/documents`.

Reuse from Part 1 (all merged, on `main`):

| Piece | Anchor |
|---|---|
| Shared enqueue path | `Domain/Integration/CaseTracker/CaseTrackerIntakeQueue.cs` -- `EnqueueIntakeAsync`, and `ScheduleDrain` with the `OnCompleted` deferral |
| Ledger + fatal path | `Domain/Integration/CaseTracker/IntegrationOutboxItem.cs` (`MarkFatal`) |
| Idempotency key | `IntegrationOutboxManager.BuildIdempotencyKey(messageType, appointmentId, version)` |
| Drain + status matrix | `IntegrationOutboxDrainService.cs`; `CaseTrackerPushResult.cs` (404 = Retryable) |
| Sweep | `Jobs/CaseTrackerReconciliationJob.cs` (`*/15 * * * *`, `ForEachOfficeAsync` + per-office try/catch) |
| Document entry mapping | `Payload/DocumentEntryMapper.cs` -- `FromDocument`, `FromPacket`, `IsFetchable`, `PacketLabel` |
| Serializer | `IntakePayloadSerializer.cs` (camelCase; nulls kept) |
| Endpoints | `CaseTrackerEndpoints.cs` |
| Message type | `Domain.Shared/Integration/CaseTracker/IntegrationMessageType.cs` -- currently `Intake = 1` only |

Trigger events -- ALL in `Domain.Shared/Notifications/Events`, namespace
`HealthcareSupport.CaseEvaluation.Notifications.Events`, so handlers may live in Domain (unlike Part
1's approval handler):
- `AppointmentDocumentAcceptedEto` -- `AppointmentId`, `AppointmentDocumentId`, `TenantId`, `IsAdHoc`, `IsJointDeclaration`, `AcceptedByUserId`, `OccurredAt`
- `AppointmentDocumentRejectedEto` -- same plus `RejectionNotes`
- `PacketGeneratedEto` -- `AppointmentId`, `TenantId`, `PacketId`, `Kind`, `OccurredAt`
- (`AppointmentDocumentUploadedEto` exists but is deliberately unused here)

Blob delete sites (enumerated; only one changes):

| Site | Action |
|---|---|
| `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:689` | **CHANGE** -- stop deleting the blob; soft-delete the row only |
| `:137` | KEEP -- rollback of a just-saved blob when the entity insert fails; no published reference can exist |
| `:608` | KEEP -- deletes the superseded blob on re-upload; `:617` then sets the new `BlobName` on the SAME row, so the id is stable and the new key is published |
| `UserSignatureAppService.cs:151` | Out of scope (different container) |
| Packets | No delete site exists; `GenerateAppointmentPacketJob.cs:199` writes a fresh `{guid}.pdf` per render, so old packet objects are already retained |

DOCX mislabel sites: `AppointmentDocumentsAppService.cs:811` (hardcoded DOCX MIME in
`GetCombinedForAppointmentAsync`) and `AppointmentPacketsAppService.cs:183`
(`BuildKindFileName` emits `.docx`). Packets have been PDFs since 2026-06-10.

Gotchas: `IBackgroundJobManager.EnqueueAsync` is not UoW-deferred (use `OnCompleted`); Hangfire
workers have no ambient tenant (`_currentTenant.Change`); `ForEachOfficeAsync` aborts the whole run if
a delegate throws; entity-change events (`EntityUpdatedEventData<T>`) fire inside the mutating UoW, so
the enqueue joins that transaction.

## Deviations recorded during build (2026-07-28)

Five, all reported rather than worked around silently:

1. **T2 / T3 signature.** The plan specified `EnqueueDocumentUpdateAsync(..., IReadOnlyList<object>
   entries)` with a single serializer method. Built instead as two typed pairs --
   `SerializeDocumentEntries` / `SerializeDeletionEntries` and `EnqueueDocumentEntriesAsync` /
   `EnqueueDeletionsAsync`. Reason: entries and deletions never mix in one array (accept sends one
   entry, reject/delete send one tombstone, packets send three entries), so `object` bought nothing
   and would have relied on System.Text.Json's polymorphic handling of `object`-declared elements --
   a subtlety with no upside. Both funnel into one private enqueue, so there is still a single path.
2. **Interfaces extracted.** Added `ICaseTrackerDocumentQueue`, `ICaseTrackerIntakeQueue` and
   `IDocumentListResolver` (the latter two over existing Part 1 classes). Reason: the handlers' logic
   is entirely about WHEN to publish, and testing that through three real repositories would have
   tested the repositories. Mirrors Part 1's own `IIntakePayloadBuilder`.
3. **`CaseTrackerPublishPolicy` added (not in the plan).** Every document and change trigger now
   checks that the appointment's intake was actually published before enqueueing. Reason: staff review
   documents during intake, BEFORE approval. Without this guard every pre-approval accept would
   enqueue a document update for a case that does not exist, get 404-retried three times and
   dead-letter -- turning normal intake work into a stream of false alerts. Expressed as a deny list
   (Pending / Rejected / InfoRequested, all reachable only from Pending) so an unrecognised future
   status fails loudly rather than letting cases go silently stale.
4. **T10 derives the extension instead of hardcoding it.** The plan said report `application/pdf` and
   `.pdf`. Built as detection from the stored blob name, matching what
   `AppointmentPacketsAppService.DownloadByKindAsync:157` already does. Reason: that sibling read path
   already detects, so hardcoding would have made the file name disagree with the content type
   resolved from the same blob, and any legacy DOCX row would download mislabelled.
5. **T11 could not be completed as written.** The plan says "MODIFY the Application tests covering
   `DeleteAsync` to assert the blob is retained". No such test exists -- `AppointmentDocumentsAppServiceTests`
   covers only the two upload entry points and explicitly documents that paths needing a seeded
   `AppointmentDocument` + `Appointment` are not covered there, and the EF app-service test file
   references no blob container at all. **The blob-retention half of T6 therefore has NO automated
   test.** The tombstone half is covered (`DocumentRemovalHandlerTests`). See "Known gap" below.

## Known gap (needs Adrian's call)

`DeleteAsync` no longer deleting the blob is verified by code review only. Closing it properly needs a
new EF integration test that seeds an appointment + document + blob and asserts the object survives
the delete -- roughly an hour including the ~9-minute feedback loop per iteration, and it is the
riskiest change in this part because it alters existing behaviour. Options: write it as a follow-up, or
accept review-only verification given deletion is rare and IT-Admin-only.

## Tasks (implementation blueprint)

### T1 - Add the document-update message type
- what: MODIFY `Domain.Shared/Integration/CaseTracker/IntegrationMessageType.cs` adding
  `DocumentUpdate = 2`; MODIFY `Domain/Integration/CaseTracker/CaseTrackerEndpoints.cs` adding
  `DocumentUpdate(Guid appointmentId)` returning `api/intake/appointments/{id}/documents`.
- pattern: existing `Intake = 1` and `CaseTrackerEndpoints.Intake`
- approach: code
- acceptance: The system shall expose `IntegrationMessageType.DocumentUpdate` and build the
  per-appointment document-update path without a database migration.

### T2 - Serialize a bare document-update array
- what: MODIFY `Domain/Integration/CaseTracker/IntakePayloadSerializer.cs` adding
  `SerializeDocuments(IReadOnlyList<IntakeDocumentEntry>)` that emits a BARE JSON array using the same
  camelCase options; CREATE `Payload/DocumentDeletionEntry.cs` for the `{ id, deleted, updatedAt }`
  removal shape.
- pattern: existing `Serialize(IntakeEnvelope)`
- approach: tdd
- acceptance: WHEN serializing document entries, THE SYSTEM SHALL emit a top-level JSON array (not an
  object) with camelCase keys. WHEN serializing a deletion, THE SYSTEM SHALL emit only `id`,
  `deleted` and `updatedAt`.

### T3 - Document-update enqueue path
- what: CREATE `Domain/Integration/CaseTracker/CaseTrackerDocumentQueue.cs` with
  `EnqueueDocumentUpdateAsync(Guid appointmentId, Guid? tenantId, IReadOnlyList<object> entries)`:
  serializes the array, builds an idempotency key from
  `(DocumentUpdate, appointmentId, <hash of entry ids + their updatedAt>)`, enqueues one outbox row,
  and defers the drain via `OnCompleted`.
- pattern: `CaseTrackerIntakeQueue.cs` (copy `ScheduleDrain` verbatim, including the
  `ObjectDisposedException` guard)
- approach: tdd
- acceptance: WHEN the same set of entries is enqueued twice, THE SYSTEM SHALL collapse to one outbox
  row. WHEN a different entry set is enqueued for the same appointment, THE SYSTEM SHALL create a
  second row. THE SYSTEM SHALL defer the drain enqueue until the surrounding transaction commits.

### T4 - Publish a document when staff accept it
- what: CREATE `Domain/Integration/CaseTracker/Handlers/DocumentAcceptedHandler.cs` --
  `ILocalEventHandler<AppointmentDocumentAcceptedEto>`; load the document, map it via
  `DocumentEntryMapper.FromDocument` (resolving its type label), and enqueue a single-entry
  document-update. Skip and log if the row is not fetchable.
- pattern: `Application/Integration/CaseTracker/Handlers/CaseTrackerIntakeOnApprovedHandler.cs`
  (try/catch so an integration failure never fails the staff action)
- approach: tdd
- acceptance: WHEN staff accept a document, THE SYSTEM SHALL enqueue exactly one document-update
  containing that document with status `Accepted` and a fully-qualified `objectKey`. IF the accept
  handler throws, THEN THE SYSTEM SHALL leave the acceptance itself successful.

### T5 - Propagate reject-after-accept as a deletion
- what: CREATE `Domain/Integration/CaseTracker/Handlers/DocumentRejectedHandler.cs` --
  `ILocalEventHandler<AppointmentDocumentRejectedEto>`; enqueue a deletion entry
  `{ id, deleted: true, updatedAt }`.
- pattern: T4
- approach: tdd
- acceptance: WHEN staff reject a document, THE SYSTEM SHALL enqueue a deletion entry for that
  document id. THE SYSTEM SHALL NOT check whether the document was previously published.

### T6 - Emit and propagate a document deletion, and stop destroying the blob
- what: CREATE `Domain.Shared/Notifications/Events/AppointmentDocumentDeletedEto.cs`
  (`AppointmentId`, `AppointmentDocumentId`, `TenantId`, `DeletedByUserId`, `OccurredAt`); MODIFY
  `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` `DeleteAsync` (`:674-696`) to
  publish it and to REMOVE the `_blobContainer.DeleteAsync(entity.BlobName)` call at `:689` (row
  soft-delete only); CREATE
  `Domain/Integration/CaseTracker/Handlers/DocumentDeletedHandler.cs` enqueuing the same deletion
  entry shape as T5.
- pattern: the Accept/Reject publishes at `:716` / `:748` for the event shape
- approach: tdd
- acceptance: WHEN a document is deleted, THE SYSTEM SHALL soft-delete the row, RETAIN the MinIO
  object, publish `AppointmentDocumentDeletedEto`, and enqueue a `deleted: true` entry. THE SYSTEM
  SHALL NOT call `DeleteAsync` on the appointment-documents blob container from the delete path.

### T7 - Publish packets as one complete batch
- what: CREATE `Domain/Integration/CaseTracker/Handlers/PacketsCompleteHandler.cs` --
  `ILocalEventHandler<PacketGeneratedEto>`; query the appointment's packets and, only when all three
  `PacketKind` values are `Generated`, enqueue ONE document-update containing all three mapped via
  `DocumentEntryMapper.FromPacket`. Stateless: the packet rows are the state.
- pattern: `GenerateAppointmentPacketJob.cs:123-130` for the canonical set of three kinds
- approach: tdd
- acceptance: WHEN the first or second packet completes, THE SYSTEM SHALL enqueue nothing. WHEN the
  third completes, THE SYSTEM SHALL enqueue one document-update containing exactly three packet
  entries. WHEN a fourth `PacketGeneratedEto` arrives for an already-complete set, THE SYSTEM SHALL
  collapse onto the existing outbox row rather than sending a duplicate.

### T8 - Release packets when one kind is permanently failed
- what: MODIFY `Domain/Integration/CaseTracker/Jobs/CaseTrackerReconciliationJob.cs` to also find
  approved appointments whose packets are neither all-`Generated` nor recently changed (threshold
  const `PacketReleaseAfterMinutes = 30`) and enqueue a document-update with whatever kinds ARE
  `Generated`.
- pattern: existing `ForEachOfficeAsync` body with per-office try/catch
- approach: test-after
- acceptance: WHILE one packet kind remains `Failed` past the threshold, THE SYSTEM SHALL publish the
  `Generated` kinds rather than withholding them indefinitely. WHEN all three are `Generated`, THE
  SYSTEM SHALL leave the release path idle.

### T9 - Re-push the appointment on any published-data change
- what: CREATE `Domain/Integration/CaseTracker/Handlers/AppointmentChangedHandler.cs` handling
  `ILocalEventHandler<EntityUpdatedEventData<Appointment>>` and
  `ILocalEventHandler<EntityUpdatedEventData<Patient>>`. For an Appointment, re-push intake when its
  status is `Approved` or a post-approval state. For a Patient, re-push each of that patient's
  approved appointments. Reuses `CaseTrackerIntakeQueue.EnqueueIntakeAsync`, whose `updatedAt`-keyed
  idempotency collapses no-op saves.
- pattern: `CaseTrackerIntakeQueue.EnqueueIntakeAsync`
- approach: tdd
- acceptance: WHEN an approved appointment is edited, THE SYSTEM SHALL enqueue an intake re-push
  carrying the new `updatedAt`. WHEN a patient record is edited, THE SYSTEM SHALL enqueue a re-push
  for each of that patient's approved appointments. WHEN a save changes nothing, THE SYSTEM SHALL
  collapse onto the existing outbox row. WHILE an appointment is still `Pending`, THE SYSTEM SHALL
  enqueue nothing.

### T10 - Fix the packet DOCX mislabel
- what: MODIFY `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:811` to report
  `application/pdf` for packets; MODIFY `Application/AppointmentDocuments/AppointmentPacketsAppService.cs:183`
  (`BuildKindFileName`) to emit `.pdf`.
- pattern: `Domain/AppointmentDocuments/PacketAttachmentProvider.cs:102`, which already emits `.pdf`
- approach: test-after
- acceptance: WHEN a packet is listed or downloaded, THE SYSTEM SHALL report content type
  `application/pdf` and a `.pdf` file name.

### T11 - Tests
- what: CREATE under `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Integration/CaseTracker/`:
  `DocumentUpdateSerializerTests`, `CaseTrackerDocumentQueueTests`, `DocumentAcceptedHandlerTests`,
  `DocumentRejectedHandlerTests`, `DocumentDeletedHandlerTests`, `PacketsCompleteHandlerTests`,
  `AppointmentChangedHandlerTests`; MODIFY the Application tests covering `DeleteAsync` to assert the
  blob is retained. Synthetic data only; avoid all-numeric GUID segments (the PHI scanner reads 8+
  consecutive digits as an MRN).
- pattern: `IntegrationOutboxDrainServiceTests.cs` List-backed repository harness
- approach: tdd
- acceptance: The system shall cover the accept, reject, delete, packet-completeness and
  appointment-change paths, the bare-array serialization, and blob retention on delete.

### T12 - Commit the revised contract
- what: `git add docs/integration/case-tracker-api-contract.md` (already revised with the 2026-07-28
  block) plus this plan.
- approach: code
- acceptance: The system shall include the revised contract in this PR.

## Validation loop

From the repo root, in order:

```bash
dotnet format HealthcareSupport.CaseEvaluation.slnx --verify-no-changes
```
```bash
dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
```
```bash
dotnet test HealthcareSupport.CaseEvaluation.slnx
```
```bash
python .claude/scripts/verify_structure.py
```

Done-bar: all four green (the structure check must report 0 FAIL -- it caught a missing feature
`CLAUDE.md` in Part 1), and no test fixture contains real-looking patient data.

## Risk / rollback

Blast radius: wider than Part 1, because this one CHANGES existing behaviour rather than only adding.
Three things to watch:
- `DeleteAsync` no longer removes blobs, so storage grows on deletion instead of shrinking. Intended,
  and deletions are rare and IT-Admin-only.
- New handlers run inside the unit of work of staff actions (accept, reject, delete, edit). Each is
  wrapped so an integration failure cannot fail the staff action, and each only enqueues -- no HTTP
  happens inline.
- The packet-completeness query runs on every `PacketGeneratedEto`, i.e. three times per approval.
  It is a single indexed read per call.

Still gated: nothing reaches the Case Tracker while `CaseTrackerPushEnabled` is false, so this can
merge safely before their endpoints are deployed.

Rollback: revert the PR. No migration is involved (the `DocumentUpdate` enum value needs none), so
rollback is code-only. Blobs retained while the change was live simply remain -- harmless.
