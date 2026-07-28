# Appointment Portal -> Case Tracker: FINAL API contract

Definitive, buildable contract for the portal -> Case Tracker document integration.

STATUS 2026-07-28: Case Tracker's receiving endpoints (intake, document-update, health) are BUILT +
verified locally to this contract but NOT yet deployed to `192.168.101.35` - their team is holding
deploys so in-progress staff testing is not interrupted (the server itself is up and reachable). So
LIVE TESTING IS HELD until they deploy. The portal OUTBOUND side is now BUILT and merged for
everything except the reconcile GET (§F) and failure visibility (§I2) - see section J for the current
built/remaining split. It ships DISABLED behind the `CaseTrackerPushEnabled` setting.

Decisions folded in (2026-07-23, all FINAL):
- Intake pushes IMMEDIATELY on approval; packets/docs stream in afterwards (§H). No all-3 aggregator.
- Retention + soft-delete guarantee, scoped to appointment documents + packets (§C).
- Appointment-lifecycle re-push on cancel / reschedule / rejection+info-requested, NOT field edits (§E2).
  (SUPERSEDED 2026-07-28 -- field edits ARE pushed; see the revision block below.)
- Reconcile GET returns the FULL appointment payload + documents, authenticated by a portal-issued
  static integration token (§F). (Its original purpose -- carrying field edits by pull -- was
  SUPERSEDED 2026-07-28; it is now a backstop.)
- Re-eval linking: persisted `EvaluationKind` + `previousAppointmentId` + `previousConfirmationNumber`.
  We do NOT send a patient identifier at all - see the note under §A.
- Ordering via `updatedAt` (§G); TLS required (§A, §I).

REVISION 2026-07-28 (supersedes where it conflicts with the text below):
- **Part 1 is MERGED** (PR #393 -> main `6208a0cd`): the outbound push, outbox, intake trigger and
  manual push all exist. Still DISABLED by default; never tested live.
- **Parts 2 + 3 are MERGED** (PR #395 -> main `8a1568eb`): the document-update feed (accept /
  reject / delete / packet batch), the blob-retention change, the stalled-packet release, and the
  re-push-on-any-change trigger. Everything below marked "SUPERSEDED 2026-07-28" is now the BUILT
  behaviour, not a plan. Still disabled by default; still never tested live.
- **The portal now pushes on ANY change to an approved appointment**, not just cancel/reschedule.
  Field edits (patient / attorney / injury) are PUSHED, reversing the earlier
  "field edits are pull-only" decision. Consequence: Case Tracker does NOT need a periodic sweep
  for freshness; the reconcile GET is a BACKSTOP for a dead-lettered push, not the delivery path.
- **Uploaded documents publish only when staff ACCEPT them.** Upload no longer triggers a push, so
  Case Tracker never receives `Pending` or `Uploaded` rows -- only staff-vetted documents.
- **Reject-after-Accept is sent as `{ id, deleted: true }`**, not as a status change.
- **Packets publish as one batch once all three kinds are `Generated`** (with a timeout path if one
  kind stays `Failed`), rather than one message per packet.
- **Retention narrowed**: only the IT-Admin DELETE path retains its blob. A re-upload deletes the
  superseded blob and publishes the new `objectKey` (there is always a replacement, so nothing we
  published can be left pointing at nothing).
- Delivery: FAIL-FAST retry (few attempts, then dead-letter) + email alert + an admin dead-letter
  screen with retry, plus a manual "Push to Case Tracker" action (§I, §I2).

Grounding: every field value maps to real portal source (branch `main` @ `100a617c`), cited inline;
MinIO facts verified live. The JSON key NAMES/envelope were locked here first and the portal emitter
was then built to match; as of `8a1568eb` the emitter exists and this document describes what it
actually sends.

Serialization conventions:
- GUIDs: canonical dashed strings ("D" format).
- Timestamps: ISO-8601 UTC with `Z` (e.g. `2026-07-22T18:30:00Z`).
- Enums: serialized as their NAME string. Case Tracker stores enum-like fields verbatim (any casing).
- Nullability: "No" = always present; "Yes" = may be null or omitted. Unknown fields are ignored by
  Case Tracker (extra fields will not 400).

---

## A. Intake payload

`POST {base}/api/intake/appointments`, header `Content-Type: application/json` (REQUIRED; missing or
incompatible -> 415), body `{ "data": { ... }, "meta": { ... }, "errors": [] }`. Case Tracker reads
`data`. Only `appointmentId` is hard-required; every other field is optional on their side.

TLS REQUIRED: the payload is ePHI, so the endpoint MUST be HTTPS (or mTLS) before real data flows.
The `http://192.168.101.35:7272/evaluators-api-service` base seen today is the frozen pre-integration
build; the deployed integration base must be `https://...` (`:7272` = API, `:80` = frontend).

### `data` (top level)

| Key | Type | Format | Nullable | Source |
|---|---|---|---|---|
| `appointmentId` | string | GUID | No | `Appointment.Id` (`Domain/Appointments/Appointment.cs:21`) |
| `confirmationNumber` | string | `A`+5 digits, e.g. `A00065` | No (per-office, mutable - NOT a key) | `Appointment.RequestConfirmationNumber` (`Appointment.cs:33`); format `AppointmentBookingValidators.cs:29` |
| `status` | string | enum name; at intake `"Approved"` (later re-pushes may carry cancel/reschedule states, §E2) | No | `Appointment.AppointmentStatus` (`Appointment.cs:42`); `AppointmentStatusType` (`Domain.Shared/Enums/AppointmentStatusType.cs`) |
| `approvedAtUtc` | string | ISO-8601 UTC `Z` | No at intake | `Appointment.AppointmentApproveDate` (`Appointment.cs:40`); `= DateTime.UtcNow` (`AppointmentManager.cs:344`) |
| `submittedAtUtc` | string | ISO-8601 UTC `Z` | No | `Appointment.CreationTime` (ABP `FullAuditedAggregateRoot`, UTC) |
| `updatedAt` | string | ISO-8601 UTC `Z` | No | appointment `LastModificationTime ?? CreationTime`. Monotonic per appointment; Case Tracker's skip-if-older guard (they are last-write-wins, no version column). |
| `evaluationKind` | string | `"EVAL"` or `"RE_EVAL"` | No | NEW persisted column `Appointment.EvaluationKind` (DECIDED 2026-07-23), set at booking from the lifecycle flow (`AppointmentLifecycleFlow.Reval` -> `RE_EVAL`, else `EVAL`; `AppointmentsAppService.cs:686`). Persisted rather than derived from `OriginalAppointmentId` because that field is still documented as a reschedule-chain link, so a future change could silently mislabel; the dual-context EF migration is happening anyway for the outbox table, so this rides along. Backfill: all existing rows = `EVAL` (verified: 0 re-evals exist in production). |
| `previousAppointmentId` | string | GUID | Yes (present only on re-eval) | `Appointment.OriginalAppointmentId`, set to the source appointment on a reval (`AppointmentsAppService.cs:882-884`). THE machine link from a re-eval to its original; null for first evaluations. |
| `previousConfirmationNumber` | string | `A`+5 digits | Yes (present only on re-eval) | The SOURCE appointment's `RequestConfirmationNumber`. Human-readable aid for Case Tracker staff only - NOT a key (a re-eval gets its OWN fresh confirmation number, and the value is per-office sequential so it repeats across offices). Match on `previousAppointmentId`. |

STATUS VALUES CASE TRACKER CAN RECEIVE (verified 2026-07-27 against the state machine
`AppointmentManager.BuildMachine:380-419` and the change-request approval paths). A case only exists
from `Approved` onward, so this is the reachable set from there:

| `status` string | When | Notes |
|---|---|---|
| `Approved` | intake, AND after a reschedule is finalized | see the reschedule trap in §E2 |
| `RescheduleRequested` | a reschedule was requested, awaiting staff decision | date/time still the OLD slot |
| `CancellationRequested` | a cancellation was requested, awaiting staff decision | not yet cancelled |
| `CancelledNoBill` | cancellation approved (terminal) | also set with NO staff action by `JointDeclarationAutoCancelJob:159` (AME joint-declaration cutoff auto-cancel) |
| `CancelledLate` | cancellation approved, late/billable (terminal) | |

NEVER sent (do not build logic for these):
- `Rejected`, `InfoRequested` - reachable ONLY from `Pending` (i.e. BEFORE approval;
  `BuildMachine:386-396`). Since the portal pushes only at/after approval, an already-approved
  appointment can never become Rejected or InfoRequested. (This CORRECTS an earlier statement that
  rejection / info-requested would re-push.)
- `Pending` - pre-approval only.
- `NoShow`, `CheckedIn`, `CheckedOut`, `Billed` - present in the state machine but have NO API
  surface today (verified: no app-service exposes those triggers), so unreachable. Tolerate the
  string if the day-of-exam flow ships later; do not rely on them now.
- `RescheduledNoBill`, `RescheduledLate` - NOT set on the appointment by the current in-place
  reschedule; that outcome is recorded on the change-request row. Rows predating the 2026-07-01
  redesign may still carry them, so tolerate but do not rely.

Recommendation for Case Tracker: store the status string verbatim and treat only `CancelledNoBill` /
`CancelledLate` as terminal-cancelled; treat `*Requested` as "change pending, still active".

NO PATIENT IDENTIFIER IS SENT (DECIDED 2026-07-23). The portal is database-per-office, so the same
human booking at two offices produces two unrelated `Patient` rows with different GUIDs - the portal
has no cross-office patient identity. CalMed additionally mints a NEW patient ID per claim, so the
portal's internal id matches neither CalMed's nor Case Tracker's grain and could be mistaken for an
authoritative key. Patient identity and patient-folder routing are owned entirely by CalMed +
Case Tracker (their staff enter the Cal-Med ID). The portal guarantees exactly one linking fact:
within an office, which re-eval belongs to which original appointment (`previousAppointmentId`).
Consequence for Case Tracker: nothing the portal sends can unify a patient ACROSS offices or claims -
only the Cal-Med ID they enter can do that.

### `data.tenant`

| Key | Type | Nullable | Source |
|---|---|---|---|
| `tenantId` | string (GUID) | No | `Appointment.TenantId` (`Appointment.cs:23`) |
| `facilityId` | string, max 50 | Can be `""` (legacy rows) | `Location.FacilityId` (`Domain/Locations/Location.cs:38-39`) via `Appointment.LocationId`; the clinic's external id |
| `officeName` | string | Effectively no | SaaS `Tenant.Name` (Volo.Saas via `ITenantStore`; `DoctorProfileDataSeedContributor.cs:68`) |

### `data.location` (the clinic for this appointment)

| Key | Type | Format | Nullable | Source |
|---|---|---|---|---|
| `name` | string | max 50 | No | `Location.Name` (`Location.cs:23`) |
| `address` | string | max 100 | Yes | `Location.Address` (`Location.cs:26`) |
| `city` | string | max 50 | Yes | `Location.City` (`Location.cs:29`) |
| `zipCode` | string | max 15 | Yes | `Location.ZipCode` (`Location.cs:32`) |

### `data.appointmentType` + `panelNumber`

| Key | Type | Nullable | Source |
|---|---|---|---|
| `appointmentType.id` | string (GUID) | No | `Appointment.AppointmentTypeId` (`Appointment.cs:48`) |
| `appointmentType.name` | string (free text) | No | `AppointmentType.Name` (`AppointmentType.cs:21`); seeded AME/IME/PQME, admin-editable - branch on `id` |
| `panelNumber` | string, max 50 | Yes | `Appointment.PanelNumber` (`Appointment.cs:26`) |

### Schedule

| Key | Type | Format | Nullable | Source |
|---|---|---|---|---|
| `appointmentDateLocal` | string | `yyyy-MM-dd` | No | `DoctorAvailability.AvailableDate` (`DoctorAvailability.cs:18`) via `Appointment.DoctorAvailabilityId` |
| `appointmentTimeLocal` | string | `HH:mm` (24h) | No | `DoctorAvailability.FromTime` (`DoctorAvailability.cs:20`, `TimeOnly`) |
| `timeZone` | string | IANA, `America/Los_Angeles` | No | CONSTANT the emitter sets - no stored offset. |
| `durationMinutes` | int | minutes | No | DERIVED = `ToTime - FromTime` (`DoctorAvailability.cs:20-22`). No stored duration. |

### `data.patient`

| Key | Type | Nullable | Source |
|---|---|---|---|
| `firstName` | string | No | `Patient.FirstName` (`Patient.cs:29`) |
| `middleName` | string | Yes | `Patient.MiddleName` (`Patient.cs:35`) |
| `lastName` | string | No | `Patient.LastName` (`Patient.cs:32`) |
| `email` | string | No | `Patient.Email` (`Patient.cs:38`) |
| `phoneNumber` | string | Yes | `Patient.PhoneNumber` (`Patient.cs:45`) |
| `phoneNumberType` | string | No | enum name `Home` or `Work` (`Patient.cs:62`; `Work=28`, `Home=29`) |
| `cellPhoneNumber` | string | Yes | `Patient.CellPhoneNumber` (`Patient.cs:60`) |
| `dateOfBirth` | string | `yyyy-MM-dd` (date only) | No | `Patient.DateOfBirth` (`Patient.cs:42`, `DateTime`, not null). ADDED 2026-07-27 at Case Tracker's request - for STAFF EYEBALL CROSS-CHECK only (name + DOB, to catch a mistyped Cal-Med ID before it creates an orphan folder). Explicitly NOT a key and not for automated matching. |

### `data.doctor`

| Key | Type | Nullable | Source |
|---|---|---|---|
| `firstName` | string | Can be `""` | `Doctor.FirstName` (`Doctor.cs:19`) |
| `lastName` | string | No | `Doctor.LastName` (`Doctor.cs:22`) |

Office's single Doctor row (tenant = doctor; `IX_AppEntity_Doctors_TenantId_Unique`). Not an
appointment field.

### `data.storage`

| Key | Value | Source |
|---|---|---|
| `bucket` | `"case-evaluation-documents"` | verified live on the portal MinIO |

Only `bucket` is sent. Case Tracker takes endpoint, region, credentials from ITS OWN config.

---

## B. `documents[]` entry

| Key | Type | Nullable | Source / notes |
|---|---|---|---|
| `id` | string (GUID) | No | `AppointmentDocument.Id` or `AppointmentPacket.Id`. Per-file dedup key. Every entry MUST have one. |
| `source` | string | No | `"document"` (uploaded) or `"packet"` (generated). |
| `kind` | string | packets only (null for docs) | `PacketKind`: `Patient` / `Doctor` / `AttorneyClaimExaminer` |
| `documentName` | string | No | docs: `AppointmentDocument.DocumentName`. packets: emitter label (synthesized). |
| `fileName` | string | No | docs: `AppointmentDocument.FileName` (original). packets: SYNTHESIZED, always `.pdf`. |
| `contentType` | string | Yes | docs: `AppointmentDocument.ContentType` (nullable) - `application/pdf`, `image/jpeg`, `image/png` (§D). packets: ALWAYS `application/pdf`. |
| `fileSize` | number (int64 bytes) | packets: YES (null) | docs: `AppointmentDocument.FileSize`. packets: NOT STORED - emitter stats MinIO or sends null. |
| `status` | string | No | docs: `Uploaded` / `Accepted` / `Rejected` / `Pending`. packets: `Generating` / `Generated` / `Failed`. |
| `objectKey` | string | No (omitted entries have none) | fully-qualified within `bucket` = `tenants/{tenantId}/` + row `BlobName`. OPAQUE - use verbatim. |
| `createdAtUtc` | string (ISO-8601 UTC) | No | docs: `CreationTime`. packets: `AppointmentPacket.GeneratedAt`. |
| `documentType` | string | uploaded docs only, Yes | DERIVED from `AppointmentDocumentTypeId` -> name, else `OtherDocumentTypeName`, else null. |
| `updatedAt` | string (ISO-8601 UTC) | No | the row's `LastModificationTime ?? CreationTime`. Per-document skip-if-older guard. Also on deletion entries. |

Confirmed: status enums as above (stored verbatim); non-fetchable entries OMITTED (`Pending` docs
carry `(pending-upload)` and have no object; packets not `Generated` have no object); `objectKey`
opaque; packets are ALWAYS `application/pdf` (two portal read paths mislabel them DOCX -
`AppointmentDocumentsAppService.cs:811`, `AppointmentPacketsAppService.cs:183` - portal bugs the
emitter avoids); `fileSize` may be null for packets.

---

## C. Deletions + retention (DECIDED 2026-07-23)

Retention guarantee (portal commitment): the portal does NOT purge document blobs, and document
deletion becomes SOFT-DELETE ONLY - the row is hidden (ABP `ISoftDelete`) but the MinIO object is
RETAINED. So Case Tracker's read-in-place references (including a re-eval folder pointing at the
original evaluation's documents) survive the retention window (>= 18 months). This is a CHANGE from
today's `DeleteAsync` (`AppointmentDocumentsAppService.cs:689`), which currently HARD-deletes the
blob; that physical delete is being removed.

SCOPE (DECIDED 2026-07-23): the no-hard-delete guarantee covers ONLY the containers Case Tracker
references - `AppointmentDocumentsContainer` and `AppointmentPacketsContainer`. Other containers
(office logos, user signatures, etc.) keep deleting on replace, where retaining superseded copies has
no value. Packets are included because a regenerated packet gets a NEW `objectKey`, so an old Case
Tracker reference would break if the previous object were removed.

Delete propagation (still needed; TO BE BUILT):
- When a document is deleted on the portal, the document-update feed (§E) carries a removal entry
  `{ "id": "<guid>", "deleted": true, "updatedAt": "<iso-utc>" }` so Case Tracker drops it from the
  active view. The blob is retained on the portal side regardless.
- Shared-reference semantic: because Case Tracker references (does not copy), a delete of an original
  document propagates to EVERY case/folder that references that object id, including a re-eval folder.
  A case needing a snapshot frozen against later deletes would need its own copy; under this model it
  is reference-only.
- Backstop: the reconcile list (§F) omits soft-deleted docs.

STATUS: BUILT (PR #395 -> main `8a1568eb`). `DeleteAsync` soft-deletes the row and retains the blob,
publishes `AppointmentDocumentDeletedEto`, and the handler propagates `{id, deleted:true}`. NOTE the
narrowing: retention applies to the IT-Admin DELETE path. A re-upload still deletes the superseded
blob, because it always writes a replacement key onto the SAME row, so nothing published is left
pointing at nothing.

---

## D. File types and content types

Portal uploads are TYPE-RESTRICTED (server-side): `.pdf`, `.jpg`, `.jpeg`, `.png` only
(`AppointmentDocumentsAppService.cs:921`; Angular `accept` mirrors it), 10 MB cap
(`AppointmentDocumentConsts.MaxFileSizeBytes`; `AppointmentDocumentManager.CreateAsync:44`). So an
uploaded doc's `contentType` is `application/pdf` / `image/jpeg` / `image/png` (client-supplied,
may be null - trust the `fileName` extension). Generated packets are always `application/pdf`.

---

## E. Document-update endpoint (post-approval deltas)

- URL: `POST {base}/api/intake/appointments/{appointmentId}/documents` (same `{base}`, TLS),
  `Content-Type: application/json`.
- Body: a BARE JSON array `[ {...} ]` (NOT wrapped) of `documents[]` entries (§B) for upsert, plus
  removal entries (`{ id, deleted: true, updatedAt }`, §C). Upsert by `id`; delete by `id` on
  `deleted:true`. Each entry MUST carry an `id` (id-less entries skipped, not a 400).
- Ordering / 404: `{appointmentId}` must already be a known intake or the call returns 404. So the
  outbox MUST deliver the intake before any doc-update for that appointment, and treat a 404 here as
  RETRYABLE (intake not processed yet), distinct from a fatal 401.
- Partial re-push is safe: upserts the listed entries, does NOT drop others; removal only via `deleted:true`.
- Triggering events (portal domain events EXIST; the push wiring does NOT - §J):
  - upload -> `AppointmentDocumentUploadedEto` (`AppointmentDocumentsAppService.cs:383,431,514,557`)
  - accept/reject -> `AppointmentDocumentAcceptedEto` (`:716`) / `AppointmentDocumentRejectedEto` (`:748`)
  - packet generated -> `PacketGeneratedEto` (`GenerateAppointmentPacketJob.cs:221`)
  - delete -> a NEW delete event (does not exist yet; §C/§J)
- A `Pending` doc becoming `Uploaded`, or a packet reaching `Generated`, first appears here with a
  real `objectKey` - your signal it is fetchable.

---

## E2. Appointment-update channel (DECIDED 2026-07-23)

Documents are not the only thing that changes after a case is created - the appointment itself can
change. The portal RE-PUSHES intake (`POST /api/intake/appointments`, same envelope, same
`appointmentId`) carrying the updated `status`, schedule, and `updatedAt`.

Trigger set (DECIDED 2026-07-23, CORRECTED 2026-07-27 after verifying the state machine) - PUSHED:
- Cancellation, both stages: `CancellationRequested` (awaiting decision) then `CancelledNoBill` /
  `CancelledLate` (terminal). Critical: a cancelled appointment must not sit in Case Tracker as an
  active case. Note a cancellation can also occur with NO staff action via
  `JointDeclarationAutoCancelJob:159`.
- Reschedule, both stages: `RescheduleRequested` (old date/time) then back to `Approved` with the NEW
  date/time. The portal moves the SAME appointment in place rather than cloning a row
  (`AppointmentChangeRequestsAppService.Approval.cs:218-236`), so the `appointmentId` is unchanged and
  this UPDATES the existing case - it never creates a second one.
- REMOVED from this list: "rejection or info-requested after approval" - VERIFIED IMPOSSIBLE.
  `Rejected` and `InfoRequested` are reachable only from `Pending` (`AppointmentManager.BuildMachine:386-396`),
  i.e. before approval, and the portal only pushes at/after approval.

RESCHEDULE TRAP (important for the receiver): when a reschedule is finalized the appointment status
returns to `Approved` - it does NOT become a "Rescheduled" status
(`RescheduleInPlacePolicy.ResolveFinalizedStatus`; the RescheduledNoBill/Late outcome goes on the
change-request row, not the appointment). So the signal that an appointment MOVED is the changed
`appointmentDateLocal` / `appointmentTimeLocal` + `updatedAt`, NOT a status change. A receiver that
keys "case moved" off a status transition will miss every reschedule.

SUPERSEDED 2026-07-28 -- field edits ARE now pushed. The trigger is ANY change to an approved
appointment, including patient / attorney / injury field edits. Re-pushing costs almost nothing
because it reuses the single idempotent `EnqueueIntakeAsync` path: the idempotency key is versioned by
the appointment's `updatedAt`, so a save that changed nothing collapses onto the existing outbox row
instead of sending a duplicate. Consequence for the receiver: no periodic sweep is needed to stay
fresh, and the reconcile GET (§F) is a backstop rather than the delivery path for edits.

Receiver requirement: Case Tracker must let a re-push CHANGE a case's `status` (e.g. to a cancelled /
rescheduled state), not only accept the first `Approved`. `updatedAt` guards ordering as for documents.

STATUS: BUILT (PR #395 -> main `8a1568eb`). `AppointmentChangedHandler` watches `Appointment` and
`Patient` updates. NOT watched, deliberately: `Location`, `Doctor` and `AppointmentType` edits. They
appear in the payload but are rare admin actions that would fan out to every appointment at a
location, so they reach you via reconcile-on-open instead. Practical consequence: renaming a clinic
will not immediately refresh cases you already hold.

---

## F. Reconcile GET (portal-exposed backstop)

SCOPE CHANGE (DECIDED 2026-07-23): the reconcile endpoint returns the FULL appointment payload, not
just documents.

Its ORIGINAL rationale -- that field edits were not pushed, so the pull was how they reached Case
Tracker -- was SUPERSEDED 2026-07-28. Field edits are now pushed (§E2), so this endpoint is a
BACKSTOP: it recovers a push that dead-lettered, and it is the clean way to refresh on case-open. The
full-payload shape is kept regardless, because the portal reuses one payload builder for both and a
documents-only variant would cost the same to build while giving you less.

- URL: `GET {portalBase}/api/integration/appointments/{appointmentId}` ->
  `{ "data": { <the complete §A intake payload, including the documents[] array> }, "meta": {...}, "errors": [] }`.
  Byte-identical in shape to what the push sends, so Case Tracker can reuse the SAME deserializer and
  upsert path for both push and pull. (The portal reuses the same payload builder, so exposing the
  full payload costs essentially nothing over a documents-only version.)
- Returns CURRENT truth: latest appointment/patient/attorney/location fields + the fetchable document
  list (all packet kinds when generated; excludes soft-deleted and non-fetchable entries).
- Recommended Case Tracker usage (REVISED 2026-07-28): call it on case-open, and otherwise only as a
  BACKSTOP. A periodic sweep is no longer needed for freshness now that every appointment change is
  pushed; its remaining value is recovering a push that dead-lettered. An hourly sweep is harmless if
  you want one (each call is ~8-10 indexed reads on one office database, and there is no rate limit),
  but it is no longer load-bearing.

Answers to the receiver's reconcile questions (2026-07-28):
- **Same clock as the push?** Yes, and stronger than that: reconcile runs the SAME
  `IIntakePayloadBuilder` over the same columns, so `updatedAt` is the same value from the same source
  (`LastModificationTime ?? CreationTime`, formatted by one helper). Reconcile can therefore never
  return an OLDER stamp than a push for the same data -- it can only be equal or newer, because a push
  carries a snapshot rendered at enqueue time while reconcile always reads live. A skip-if-older guard
  is safe. Do NOT compare `meta.timestamp`: that is when the response was generated, not a data
  version.
- **Cancelled appointment?** `200` with the current data, carrying the cancelled status
  (`CancelledNoBill` / `CancelledLate`). Cancellations are exactly what reconcile should be able to
  recover, so they are never hidden behind a 404. `404` means only "no such appointment here" -- treat
  that as terminal and stop sweeping it. (Rejected never occurs: rejection is only reachable
  pre-approval, so it can never apply to an appointment you hold.)
- **Are deleted documents listed?** No -- and the array is GUARANTEED to be the complete, current,
  never-paginated set for that appointment. So absence is authoritative: anything you hold that is
  absent should be dropped. That is semantically right, because absence covers deleted,
  rejected-after-accept and never-accepted alike, and all three mean "do not show this".
- **Can `objectKey` change for a stable `id`?** YES, for both kinds. A packet regeneration reuses its
  row (composite unique on tenant + appointment + kind) and writes a new blob; a document re-upload
  mutates the same row (`document.BlobName = newBlobName`). Treat `id` as stable and `objectKey` as
  MUTABLE -- always overwrite the stored key on upsert, never key off it.
- Auth (DECIDED 2026-07-23): a portal-issued STATIC integration token in a header
  (`X-Integration-Token`), constant-time compared, stored as a secret on both sides - the mirror image
  of their `X-Intake-Token`. Chosen over OpenIddict client-credentials (which the portal already
  supports) because it is symmetric with their inbound design and needs no token-fetch/refresh cycle
  on their side. Portal issues the token out of band.
- STATUS: not built (the internal `GetCombinedForAppointmentAsync` is not reusable as-is).
- Distinct from Case Tracker's own `GET /api/intake/health` (§I), which we call to check
  connectivity to THEM.

---

## G. Keys and idempotency

- Case dedup key: `appointmentId` = `Appointment.Id` (stable, globally unique GUID). Upsert on it.
- Per-file dedup key: `id` = `AppointmentDocument.Id` / `AppointmentPacket.Id`.
- `objectKey` is MUTABLE for a stable `id`, in BOTH directions (confirmed 2026-07-28):
  - a packet regeneration reuses its row (composite unique `(TenantId, AppointmentId, Kind)`) and
    writes a new blob;
  - a document re-upload mutates the same row (`AppointmentDocumentsAppService.cs:617`,
    `document.BlobName = newBlobName`) and the superseded blob is deleted.
  Always upsert by `id` and OVERWRITE the stored `objectKey`. Never treat the key as an identity.
- Ordering guard (AGREED): the portal sends `updatedAt` per appointment and per document (incl.
  deletions); Case Tracker skips stale writes. Correctness is independent of delivery order.

---

## H. Timing

DECIDED 2026-07-23: the intake pushes IMMEDIATELY on appointment approval. It does NOT wait for
packets.

- Trigger: the approval transition (`AppointmentApprovedEto` / `AppointmentStatusChangedEto`
  `ToStatus=Approved`). The intake typically carries ZERO packets, because packet rendering is an
  async Hangfire job that finishes seconds-to-minutes later.
- Packets then arrive individually through the document-update feed (§E) as each `PacketGeneratedEto`
  fires; uploaded documents likewise as they are uploaded/accepted.
- Case Tracker already upserts on `appointmentId` and accepts 0-3 packets per message, so an initially
  packet-less case is expected and valid on their side (their staff complete it before it becomes a
  case).
- Rationale for dropping the previous "wait for all 3 packets + Failed-timeout" design: it required a
  stateful aggregator (track 3 kinds, detect completion, arm a timer, handle a permanently `Failed`
  kind) that bought Case Tracker nothing they need. Immediate push creates the case sooner with
  strictly less machinery and fewer failure modes.
- Appointment changes re-push intake (§E2); field edits are pulled (§F).

---

## I. Auth, response, and outbox status handling

- Transport: HTTPS/TLS REQUIRED before real PHI (mTLS acceptable). Do NOT POST PHI over plain http.
- Auth: header `X-Intake-Token`, raw token OR `Bearer <token>` (they strip `Bearer `), constant-time
  compared. Otherwise open server-to-server: CSRF disabled, stateless, intake paths in
  `web.ignoring()`. Portal sends the RAW token, stored as a secret. Token shared out of band at deploy.
- `Content-Type: application/json` REQUIRED (else 415).
- Response: 2xx no body on accept; no case id returned (portal keys on `appointmentId`).
- Outbox status handling (portal): 2xx -> done; 401 -> FATAL, no retry (dead-letter + alert);
  400/415 -> fatal (build bug); 404 on doc-update -> RETRYABLE (intake not processed yet); 5xx /
  timeout / connection -> retryable with backoff.
- Retry policy (DECIDED 2026-07-23) - FAIL FAST: a small number of attempts (default 3, short backoff,
  dead-lettered within roughly 20 minutes), then terminal + alert a human. Rationale: appointment and
  case timelines are legally significant, so a case must never sit silently in a retry queue for hours -
  it is better to notify staff early so they can inspect the problem or use the manual push. Attempt
  count and backoff are config-tunable. Deliberately NOT a long (24h) window.
- Health check (no side effects): `GET {base}/api/intake/health` -> no token `200 {tokenProvided:false}`,
  valid `200 {tokenValid:true}`, invalid `401`.

---

## I2. Failure visibility (DECIDED 2026-07-23)

A permanently failed push means a case silently never reaches Case Tracker - the worst failure mode of
this integration, because nothing in either UI would show it. Combined with the fail-fast retry policy
(§I), a failure surfaces to a human within minutes. Phase 1 includes ALL of:
- Email alert to internal staff when an outbox row reaches terminal `Failed` (reuses the portal's
  existing email infrastructure; note `OutboxDrainJob` today only logs counts, so this is new).
- An admin dead-letter SCREEN (net-new Angular; no outbox UI exists today) listing failed pushes with
  the appointment, message type, target, attempt count, and last error, plus a MANUAL RETRY action.
- A manual "Push to Case Tracker" action on an appointment, reusing the same code path - covers
  recovery, pre-enablement appointments, and any case that dead-lettered.
- Alerts and screens must never include PHI in the notification body - reference the appointment by
  `appointmentId` / confirmation number only.

---

## J. Built vs. not-built

Case Tracker side: intake + document-update + health BUILT + verified locally; NOT deployed to
`192.168.101.35` yet. Live testing held until their deploy. Network path portal -> `.35` is open
(`:7272`, `:80` reachable).

Portal side - BUILT and merged (as of `8a1568eb`, 2026-07-28). All of it ships DISABLED behind the
`CaseTrackerPushEnabled` setting, and NONE of it has been exercised against a live Case Tracker:
- Outbound push client + a NEW transactional outbox entity (`IntegrationOutboxItem`). PR #393.
  `NotificationOutboxItem` could not be reused - its fields are email-shaped - but its mechanics were
  mirrored: per-tenant, written in the same transaction as the state change, lease/visibility-timeout
  claim (`TryClaim`), idempotent `MarkSent`, `MarkFailed` with backoff, terminal dead-letter via
  `MarkFatal`, `IdempotencyKey`, `MaxAttempts`, and the §I status matrix.
- Intake trigger firing on approval, no aggregator (§H). PR #393.
- Persisted `Appointment.EvaluationKind`, in a dual-context EF migration (host + Tenant sets), all
  existing rows defaulting to `EVAL` (§A). PR #393.
- Payload builder for `evaluationKind`, `previousAppointmentId`, `previousConfirmationNumber`,
  `updatedAt`, and the fully-qualified `objectKey`s, with NO patient identifier (§A). PR #393.
- Manual "Push to Case Tracker" action (§I2). PR #393.
- Document-update feed (§E): `IntegrationMessageType.DocumentUpdate`, the bare-array body, and one
  enqueue path shared by every trigger. PR #395.
- Event -> push wiring for documents. Accept publishes the document; reject and delete publish
  `{id, deleted:true}` tombstones (§C); packets publish as ONE batch once all kinds are `Generated`,
  with a 30-minute release for a set stalled by a permanently failed kind. PR #395.
  NOTE the narrowing versus the original plan: UPLOAD does not trigger a push -- only staff ACCEPT.
- DELETE domain event (`AppointmentDocumentDeletedEto`) + retention change: `DeleteAsync` no longer
  removes the blob, so a key you hold stays fetchable (§C). PR #395.
- Re-push on ANY change to a published appointment (§E2), superseding the narrower
  cancel/reschedule-only trigger. PR #395.
- Packet-mislabeled-as-DOCX fix (§B): filename and content type are both derived from the stored
  blob. PR #395.

Portal side - still NOT built:
- Reconcile GET returning the FULL appointment payload (§F), gated by a static `X-Integration-Token`
  header check. This is the only remaining protocol surface, and the payload builder it needs already
  exists; the new work is the token auth mechanism, since the portal has no API-key pattern today.
- Failure visibility (§I2): terminal-failure email alert and the admin dead-letter screen with retry.
  The fail-fast retry policy and the manual push action they complement are already built. Today a
  dead-lettered push is visible only in the logs.
- TLS/mTLS on the transport (coordinate with Case Tracker).
- Infra: expose MinIO to the CT VM over TLS; mint a scoped key (read-only on
  `case-evaluation-documents`, read/write + delete on a new `case-tracker-documents`); create that
  bucket. (Capacity fine: MinIO ~3.3 MB; disk 28% after build-cache reclaim.) NOTE: TLS needs an
  internal DNS hostname + a certificate - a cert cannot be issued for a bare IP, so
  `https://192.168.101.37:9000` is not viable. Same constraint Case Tracker hit for their own HTTPS
  base; coordinate one internal DNS naming approach with IT for both endpoints.
- Data prerequisite: `Location.FacilityId` is still EMPTY on both production clinics.

Known gap in what IS built: the blob-retention change has no automated test. The tombstone half is
covered; that `DeleteAsync` leaves the object in place is verified by code review only.

Already EXISTS (reused): all field VALUES incl. `LastModificationTime`; upload/accept/reject/
packet-generated events; byte retrieval by key; the MinIO store + ability to mint scoped users.

---

## Coordination

Portal provides (pending): MinIO endpoint reachable from `192.168.101.35` over TLS; the
`case-tracker-documents` bucket; a scoped MinIO key (read-only on `case-evaluation-documents`,
read/write + delete on `case-tracker-documents`, out of band); the reconcile GET URL + auth.

Case Tracker provides (confirmed endpoints): intake `POST /api/intake/appointments`; doc-update
`POST /api/intake/appointments/{appointmentId}/documents` (bare array); health `GET /api/intake/health`;
base at deploy `https://192.168.101.35:7272/evaluators-api-service` (TLS required); the `X-Intake-Token`
out of band at deploy.

Agreed decisions (2026-07-23), all FINAL:
1. Documents are read IN PLACE from the shared portal MinIO - never copied app-to-app.
2. The portal guarantees retention: no purge, and soft-delete-only (blob retained) for appointment
   documents + packets. So Case Tracker does NOT need a copy-into-own-bucket safety net for the
   18-month re-eval window.
3. Intake pushes immediately on approval (0 packets expected initially); packets/docs follow via the
   update feed.
4. REVISED 2026-07-28: the portal re-pushes on ANY change to a published appointment, field edits
   included, so no periodic pull is needed for freshness. (Was: lifecycle-only, with field edits
   pulled. "Rejection+info-requested" was also dropped as impossible - both are reachable only
   pre-approval.) Excluded from the trigger: `Location`, `Doctor` and `AppointmentType` edits.
5. Reconcile GET returns the full appointment payload + documents (same shape as the push), now as a
   BACKSTOP for a dead-lettered push and for refreshing on case-open.
6. Ordering: `updatedAt` per appointment + per document; Case Tracker skips stale writes.
7. Re-eval linking: persisted `EvaluationKind` + `previousAppointmentId` (machine link) +
   `previousConfirmationNumber` (human aid). NO patient identifier is sent - patient identity and
   folder routing belong to CalMed + Case Tracker.
8. TLS required before real PHI flows. Delivery fails fast (few attempts, then dead-letter) and raises
   an email alert + an admin dead-letter screen with retry, plus a manual push action.
9. Reconcile GET is authenticated by a portal-issued static `X-Integration-Token`.
10. Go-live: only appointments approved after enablement push automatically; the manual push action
    covers anything earlier. `Location.FacilityId` must be populated first (blank on both production
    clinics today; the app already enforces it for new/edited locations).
11. Live testing held until Case Tracker deploys the endpoints to `.35`.
