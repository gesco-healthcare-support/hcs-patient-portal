# Appointment Portal <-> Case Tracker: document sharing design

Audience: the Case Tracker developer and his coding agent. Goal: enough detail to design the
Case Tracker intake + document endpoints without reading the portal's source or database.

Status: agreed architecture (2026-07-23). Verified against the portal source (branch `main`
@ `100a617c`) and the live deployed servers. No secrets are included; credentials and the final
MinIO endpoint are shared out of band.

Companion doc (full appointment field table, types, nullability, source citations):
`case-tracker-intake-handoff.md`. NOTE: that doc's "file model" section (which suggested each app
keeps its own copy) is SUPERSEDED by this document - we are using a single shared MinIO, described
below.

---

## 0. The decision, in one paragraph

An appointment becomes a case when portal staff approve it. At that point the portal sends the
Case Tracker an intake message containing the appointment data, the office/tenant details, and a
list of that appointment's documents. Documents are NOT copied app-to-app: both systems share ONE
object store - the portal's existing MinIO - and Case Tracker reads the portal's documents in place
by object key, and writes its own case documents into its own bucket in the same MinIO. The
document list is kept fresh over time because users keep uploading files after approval; the portal
pushes incremental updates as that happens.

Key principle: MinIO holds opaque bytes only. All human-meaningful metadata (file name, type,
status, which packet kind) lives in the portal's database and travels to Case Tracker in the push -
never by Case Tracker reading the portal's database or reconstructing storage keys itself.

---

## 1. How the appointment portal stores documents

There are two layers. This split is the most important thing to understand.

### Layer A - the catalog (portal database; source of truth)

Every file is described by a row in the portal's per-office SQL database. There are two kinds:

Uploaded documents (patient/staff-provided files) - one row per file, with these fields:

- `Id` (GUID) - stable, unique, immutable. Use this as the document identity.
- `AppointmentId` (GUID), `TenantId` (GUID)
- `DocumentName` (label), `FileName` (original upload name, e.g. `records.pdf`)
- `BlobName` (the storage key - see Layer B)
- `ContentType` (MIME, may be null), `FileSize` (bytes)
- `Status`: `Uploaded` | `Accepted` | `Rejected` | `Pending`
- classification: an optional document-type name, plus flags `IsAdHoc` / `IsJointDeclaration` /
  `IsPanelStrikeList`
- `Pending` rows are placeholders queued at booking time whose file has NOT been uploaded yet -
  their `BlobName` is the literal string `(pending-upload)` and there is NO object in MinIO for
  them until the patient uploads. Do not try to fetch these.

Generated packets (PDFs the portal renders per appointment) - one row per (appointment, kind):

- `Id` (GUID), `AppointmentId` (GUID), `TenantId` (GUID)
- `Kind`: `Patient` | `Doctor` | `AttorneyClaimExaminer` (all three are generated per appointment)
- `BlobName` (storage key), `Status`: `Generating` | `Generated` | `Failed`
- `GeneratedAt` (UTC). Only `Generated` packets have a fetchable object.

"All files for appointment X" = the uploaded-document rows plus the packet rows for that
`AppointmentId`. That union is the "document list" this design pushes to Case Tracker.

### Layer B - the bytes (MinIO)

- Provider: MinIO (S3-compatible). One bucket for all portal files: `case-evaluation-documents`.
- The `BlobName` on a row is a LOGICAL (relative) key. The real object key inside the bucket has a
  tenant scope prefix added by the portal's framework. Verified layouts on the live server
  (GUIDs shown as placeholders):
  - Uploaded documents:
    `tenants/{tenantId}/{tenantId-nodashes}/{appointmentId-nodashes}/{fileGuid-nodashes}`
    (no file extension)
  - Generated packets:
    `tenants/{tenantId}/{tenantId}/{appointmentId}/packet/{kind}/{fileGuid-nodashes}.pdf`
    where `{kind}` is `patient` | `doctor` | `attorneyclaimexaminer`
  - `{tenantId}` is a GUID; note the tenant id appears twice, and packet paths use dashed GUIDs
    while document paths use no-dash GUIDs. You do NOT need to understand or build these keys -
    the portal sends you the fully-qualified key as an opaque string (section 2). Just use it.
- Object names carry no file name, type, or status. That is why the list in section 2 carries the
  metadata.

---

## 2. What the portal sends to Case Tracker (the push)

### When

- On approval, the portal renders the three packets asynchronously (seconds to a couple of minutes
  after the staff clicks Approve). The intake push fires once the packets are ready (all three
  `Generated`), so the case arrives with its packets present. Uploaded documents may still be
  incomplete at that moment - section 4 covers keeping the list current.
- Idempotency: the portal may re-send. Treat `appointmentId` as the case key and upsert; treat each
  document `id` as its own key and upsert. Re-delivery must not create duplicates.

### Transport

- HTTP POST from the portal (VM `192.168.101.37`) to a Case Tracker intake endpoint (VM
  `192.168.101.35`), same subnet. Auth mechanism is Case Tracker's choice - propose a bearer token
  or mTLS; tell the portal side what to send.
- Body uses the shared Gesco envelope `{ data, meta, errors }`.

### Payload contract (proposed)

```json
{
  "data": {
    "appointmentId": "GUID",
    "confirmationNumber": "A00065",
    "status": "Approved",
    "approvedAtUtc": "2026-07-22T18:30:00Z",
    "submittedAtUtc": "2026-07-20T14:05:00Z",

    "tenant": {
      "tenantId": "GUID",
      "facilityId": "Location.FacilityId string",
      "officeName": "e.g. Dr. Falkinstein"
    },

    "appointmentType": { "id": "GUID", "name": "Panel Qualified Medical Examination (PQME)" },
    "panelNumber": "string or null",
    "appointmentDateLocal": "2026-08-15",
    "appointmentTimeLocal": "09:30",
    "timeZone": "America/Los_Angeles",
    "durationMinutes": 60,

    "patient": {
      "firstName": "string", "middleName": "string or null", "lastName": "string",
      "email": "string", "phoneNumber": "string or null", "phoneNumberType": "Home | Work",
      "cellPhoneNumber": "string or null"
    },
    "doctor": { "firstName": "string", "lastName": "string" },

    "storage": {
      "provider": "minio",
      "endpoint": "https://192.168.101.37:9000",
      "region": "us-east-1",
      "bucket": "case-evaluation-documents"
    },

    "documents": [
      {
        "id": "GUID",
        "source": "packet",
        "kind": "Patient",
        "documentName": "Patient Packet",
        "fileName": "A00065_Patient Packet_22072026_063000.pdf",
        "contentType": "application/pdf",
        "fileSize": 245113,
        "status": "Generated",
        "objectKey": "tenants/{tenantId}/{tenantId}/{appointmentId}/packet/patient/{guid}.pdf",
        "createdAtUtc": "2026-07-22T18:31:05Z"
      },
      {
        "id": "GUID",
        "source": "document",
        "documentType": "Medical Records",
        "documentName": "Records 2026-Q1",
        "fileName": "records.pdf",
        "contentType": "application/pdf",
        "fileSize": 1048576,
        "status": "Accepted",
        "objectKey": "tenants/{tenantId}/{tenantId-nodash}/{appointmentId-nodash}/{guid}",
        "createdAtUtc": "2026-07-21T10:15:00Z"
      }
    ]
  },
  "meta": { "requestId": "GUID", "timestamp": "2026-07-22T18:32:00Z" },
  "errors": []
}
```

Field notes for the implementer:

- `objectKey` is fully-qualified within `bucket`; use it verbatim with the MinIO/S3 client. Never
  build or parse it.
- `source` distinguishes generated packets (`packet`, always has `kind`) from uploaded files
  (`document`, has `documentType`).
- Include only fetchable files in `documents`: skip uploaded rows in `Pending` status and packets
  not yet `Generated` (they have no object). Their appearance is handled by later updates (section 4).
- `appointmentType.name` is free text set per office; branch on `id` if you need type logic. It is
  seeded as AME / IME / PQME but admins can rename or add types.
- Timestamps are UTC; the appointment slot is local wall-clock plus the `timeZone` (the portal has
  no offset stored, clinic time is Pacific).
- `appointmentId` never changes and is globally unique - the correct dedup key. The
  `confirmationNumber` is per-office and can change on a re-evaluation; do not key on it.

---

## 3. How Case Tracker reads and stores documents (via the shared MinIO)

Single shared object store: the portal's MinIO. Both apps use it; files are stored once.

### Reading a portal document (single copy, read in place)

1. Configure an S3/MinIO client with the shared endpoint, a scoped credential (provided out of
   band), TLS on, path-style access, and the bucket from the payload (`case-evaluation-documents`).
   Java: the MinIO SDK (`io.minio:minio`) or the AWS S3 SDK v2 both work - MinIO is S3-compatible.
2. For each document in the list, call `getObject(bucket, objectKey)` to stream the bytes. Use the
   list's `fileName` / `contentType` for display and download naming (the object itself has none).
3. Persist the list rows in Case Tracker's MySQL, linked to the case, storing at least: portal
   `id`, `source`, `kind`/`documentType`, `fileName`, `contentType`, `fileSize`, `status`,
   `bucket`, `objectKey`. Do NOT copy the bytes into Case Tracker's store - read them on demand.
4. To show a file to a Case Tracker user, generate a short-lived presigned GET URL from the object
   key (MinIO SDK `getPresignedObjectUrl`), or stream it through the Case Tracker backend. Presigned
   URLs avoid proxying large files through the app.

The scoped credential for Case Tracker will be read-only on `case-evaluation-documents`, so Case
Tracker cannot alter or delete portal-owned files.

### Storing Case Tracker's own case documents

- Case Tracker's own uploads (which need not appear on the portal) go into a SEPARATE bucket in the
  same MinIO, e.g. `case-tracker-documents`, under Case Tracker's own key convention (its existing
  `CASE-<epochMillis>-<hash>/<file>` scheme is fine). The scoped credential grants read/write on
  this bucket only. Keeping portal and Case Tracker objects in different buckets means neither app's
  keys can collide with the other's.
- Case Tracker's existing filesystem store (`/var/www/html/docfiles`, ~1.2 GB, 326 files across 119
  case folders, mixed pdf/docx/doc/mp3) is migrated once into `case-tracker-documents` with a
  one-time `mc mirror` or a small script, and the app switches from filesystem paths to MinIO +
  presigned URLs.

### Multi-tenancy reminder

The portal is database-per-office; every appointment carries a `tenantId` (and `facilityId`).
Persist both on the Case Tracker case so files and cases are always attributable to the right
office, and because portal object keys are namespaced by `tenantId`.

---

## 4. Keeping the document list current after approval

Documents keep arriving after the initial push: patients upload required "package" documents via
emailed links, staff upload ad-hoc files, staff accept/reject documents (status change), and
packets can be regenerated. So the list is not static.

Mechanism (portal pushes deltas; Case Tracker upserts):

- The portal emits an event whenever an appointment's documents change - a document is uploaded, a
  document's status flips to Accepted/Rejected, or a packet finishes generating. On each such event
  the portal POSTs an update to Case Tracker for that `appointmentId`.
- Proposed endpoint: `POST /intake/appointments/{appointmentId}/documents` with the same document
  entry shape as section 2 (a list of added/changed entries). Case Tracker UPSERTS by document `id`:
  insert if new, update `status`/metadata if the `id` already exists. Newly-fetchable files (a
  `Pending` row that became `Uploaded`, or a packet that became `Generated`) appear in these updates
  with a real `objectKey`; Case Tracker can then fetch them.
- Idempotency: keyed on document `id`; a re-delivered update must be a no-op if nothing changed.
- Backstop reconcile (recommended): expose a portal read endpoint that returns the full current
  document list for an `appointmentId` (the portal already computes this internally). Case Tracker
  calls it on demand - when opening a case, or on a periodic sweep - to catch any update event that
  was missed. This makes the integration self-healing without relying on perfect event delivery.

Status semantics for Case Tracker:

- Show/allow only `Accepted` (documents) and `Generated` (packets) by default, if you want to mirror
  what the portal treats as final. `Uploaded` documents are awaiting portal review; `Rejected` ones
  were declined. Carry the status through so the case view can reflect it.

---

## 5. What Case Tracker needs to build (summary)

1. Intake endpoint: `POST /intake/appointments` - creates/updates a case from the section-2 payload
   (upsert on `appointmentId`); persists appointment + tenant + doctor + the document list.
2. Document-update endpoint: `POST /intake/appointments/{appointmentId}/documents` - upsert document
   entries by `id` (section 4).
3. MinIO/S3 client integration: read portal documents in place from `case-evaluation-documents`;
   read/write Case Tracker's own files in `case-tracker-documents`; serve via presigned URLs.
4. One-time migration of `/var/www/html/docfiles` into `case-tracker-documents` and switch serving
   to MinIO.
5. Persist `tenantId` + `facilityId` on every case.
6. Auth for the inbound push (tell the portal side what to send: bearer token or mTLS).

## 6. Open infrastructure tasks (portal side / IT, not Case Tracker code)

- Expose the portal MinIO to the Case Tracker VM over the LAN with TLS enabled (it is HTTP and
  unpublished today). Finalize the endpoint host/port; the payload's `storage.endpoint` will reflect it.
- Mint a scoped MinIO credential: read-only on `case-evaluation-documents`, read/write on
  `case-tracker-documents`; create the `case-tracker-documents` bucket. (The portal MinIO supports
  this; only the root user exists today.)
- Capacity (measured 2026-07-23): MinIO currently holds ~3.3 MB of objects (1 tenant, a few docs) -
  it is NOT filling up. The portal VM has a single ~48 GiB disk that is ~79% used, but that is Docker
  build cache (~26 GB, almost entirely reclaimable via `docker builder prune`) plus images (~4.5 GB),
  not document data. No capacity blocker today. Future planning only: reclaim the build cache now,
  and before beta-scale volumes of large (up to 300 MB) files across both apps, grow the LVM volume
  or give MinIO a dedicated data disk.
- Build the portal-side trigger + outbound push + document-change events (portal team).

## 7. Provenance (how this was verified)

- Storage model, statuses, packet kinds, events: read from the portal source on branch `main`
  @ `100a617c`.
- Bucket, real object-key layout, credential/policy path: inspected live on the portal server
  (`appoint-portal`, 192.168.101.37) - bucket `case-evaluation-documents`, keys under
  `tenants/{tenantId}/...`, built-in MinIO policies present, only the root user exists.
- Case Tracker storage (filesystem `docfiles`, size/count, no object store, Spring Boot + MySQL):
  inspected live on `case-tracker-server` (192.168.101.35). No files were opened; no changes made.
