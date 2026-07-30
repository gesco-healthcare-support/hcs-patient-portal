# Appointment Portal -> Case Tracker intake: integration handoff

Audience: the engineer/agent building the Case Tracker intake endpoint.
Author: Appointment Portal side (code-grounded; verified against source + the live deployed server).
Source of truth: repo `hcs-case-evaluation-portal`, branch `main` @ `100a617c`.
Stack: Angular 20 + .NET 10 / C# (ABP Commercial 10.0.2), SQL Server (database-per-office), EF Core, OpenIddict, MinIO, Hangfire.
Every field below is cited to a real file. Nothing here is invented. Where a fact could not be
verified, it is labelled explicitly.

> UPDATE 2026-07-23 - file-sharing model FINALIZED (supersedes item 5 below and section 9's
> "infra decision" framing). The two apps SHARE ONE object store: the portal's existing MinIO.
> Case Tracker reads portal documents IN PLACE by object key (read-only) and writes its own case
> files to a separate `case-tracker-documents` bucket; nothing is copied app-to-app. The Case
> Tracker VM and the portal VM are on the same subnet, so reachability is a simple LAN-expose +
> TLS + scoped credential, not an open question. Authoritative design: `case-tracker-document-sharing.md`;
> definitive wire contract: `case-tracker-api-contract.md`. Where this doc differs on the file
> model, those two are current.

---

## 0. Read this first (the 5 things that change the plan)

1. There is NO push/webhook in the portal today. Nothing has ever POSTed to an external system.
   The trigger + the outbound POST must be built on the portal side. Do not wait for an endpoint
   that already exists - it does not.
2. The stable unique key is the appointment `Id` (Guid), NOT the confirmation number.
   `RequestConfirmationNumber` (e.g. `A00065`) is a per-office sequential counter: not globally
   unique across offices, and it changes on a re-evaluation. Dedup on the Guid.
3. The doctor is not an attribute of the appointment. There is exactly one doctor per office
   (tenant = doctor). You get the doctor by reading the single Doctor row in that office's database.
   It has separate FirstName + LastName.
4. At approval time the files are NOT ready. Packets are rendered by an async background job that
   runs after approval; required documents are frequently uploaded by the patient after approval.
   A push fired on approval would ship an incomplete case. Trigger on packet completion instead.
5. The portal's MinIO is currently internal-only (Docker network, plain HTTP, port not published
   to the LAN). A "shared MinIO" is reachable only after an infra change (expose it, or push files).
   See section 4 and 9.

---

## 1. Integration model and current state

Intended flow (per the request): on appointment approval, the portal sends the appointment's data
to a Case Tracker intake endpoint; the two systems share files via a common MinIO. This becomes a
new case.

Current portal reality:
- No outbound HTTP client, no integration event bus to an external system, no webhook. (Verified: a
  repo-wide search for outbound integration/HTTP-post/webhook found only the internal email
  `NotificationOutbox`.)
- There IS a reusable transactional-outbox pattern for email you can mirror for a reliable push
  (outbox row + background drain job + idempotency): `src/HealthcareSupport.CaseEvaluation.Domain/
  Notifications/Outbox/` (`NotificationOutboxItem.cs`, `OutboxDrainJob.cs`, `NotificationOutboxManager.cs`).
  Recommended template for the push so a Case-Tracker outage cannot lose an approval.

---

## 2. Appointment data

Format: `data needed -> exact field name -> type -> format -> nullable -> source file`.
All source paths are relative to the portal repo root.

| Data needed | Exact field name | Type | Format | Nullable | Source file |
|---|---|---|---|---|---|
| Stable unique ID (dedup key) | `Appointment.Id` | `Guid` | GUID (globally unique, immutable) | No (PK) | `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs:21` |
| Confirmation number (human, do NOT use as dedup key) | `Appointment.RequestConfirmationNumber` | `string` | `A` + 5-digit zero-pad e.g. `A00065`; per-office sequential; max 50 | No, but not globally unique and changes on Reval | `Appointment.cs:33`; format at `Application/Appointments/AppointmentBookingValidators.cs:29` |
| Approved timestamp | `Appointment.AppointmentApproveDate` | `DateTime?` | UTC (`DateTime.UtcNow`) | Yes (null until approved) | `Appointment.cs:40`; set at `Domain/Appointments/AppointmentManager.cs:344` |
| Submitted/created timestamp | `CreationTime` (ABP audit base) | `DateTime` | UTC | No | `FullAuditedAggregateRoot` base; exposed on `AppointmentDto` via `FullAuditedEntityDto<Guid>` |
| Appointment status | `Appointment.AppointmentStatus` | enum `AppointmentStatusType` | int; `Approved = 2` | No | `Appointment.cs:42`; `Domain.Shared/Enums/AppointmentStatusType.cs` |
| Appointment type (value) | `Appointment.AppointmentTypeId` -> `AppointmentType.Name` | `Guid` FK -> `string` | see note below | FK not null; `Name` not null | `Appointment.cs:48`; `Domain/AppointmentTypes/AppointmentType.cs:21` |
| Panel number (PQME) | `Appointment.PanelNumber` | `string?` | free text, max 50 | Yes | `Appointment.cs:26` |
| Patient name | `Appointment.PatientId` -> `Patient.FirstName` / `LastName` / `MiddleName` | `Guid` -> `string`/`string`/`string?` | free text (First/Last max 50) | First/Last not null; Middle nullable | `Appointment.cs:44`; `Domain/Patients/Patient.cs:29-35` |
| Doctor first/last name | `Doctor.FirstName` / `Doctor.LastName` (office's single doctor) | `string` / `string` | free text, max 50 each | Not null (FirstName may be seeded empty `""`) | `Domain/Doctors/Doctor.cs:18-23` |
| Facility ID | `Location.FacilityId` (via `Appointment.LocationId`) | `string` | free-text external key; required + unique per office; max 50 | Not null; DB default `""` (empty on legacy rows) | `Domain/Locations/Location.cs:38-39`; `Domain.Shared/Locations/LocationConsts.cs:19` |
| Appointment date | `Appointment.AppointmentDate` | `DateTime` | date; NO timezone/offset stored | No | `Appointment.cs:28` |
| Appointment time (authoritative) | `DoctorAvailability.FromTime` / `ToTime` (via `Appointment.DoctorAvailabilityId`) | `TimeOnly` / `TimeOnly` | wall-clock, clinic-local (Pacific); NO offset | Not null | `Domain/DoctorAvailabilities/DoctorAvailability.cs:20-22` |
| Duration (minutes) | none - derive `ToTime - FromTime` | computed | minutes | n/a | `DoctorAvailability.cs:20-22` |
| Patient contact number | `Patient.PhoneNumber` (+ `Patient.PhoneNumberTypeId`) | `string?` (+ enum) | free text; type `Work = 28` / `Home = 29` | Yes | `Patient.cs:44-45,62`; `Domain.Shared/Enums/PhoneNumberType.cs` |
| Home contact number | none dedicated; `PhoneNumber` when `PhoneNumberTypeId = Home(29)`; plus separate `Patient.CellPhoneNumber` | `string?` | free text | Yes | `Patient.cs:44-45,60,62` |
| Blob key for the appointment's files | `AppointmentDocument.BlobName` / `AppointmentPacket.BlobName` | `string` | logical (relative) key - see section 4 | No | `Domain/AppointmentDocuments/AppointmentDocument.cs:43`, `AppointmentPacket.cs:47` |

Appointment-type note (important): it is NOT a fixed enum. `AppointmentType` is a per-office
(tenant-scoped) catalog table; each office is seeded with 3 defaults and admins can rename/add.
Seeded `Name` values are the full labels (`Domain/AppointmentTypes/AppointmentTypeDataSeedContributor.cs:70-81`):
- `Agreed Medical Examination (AME)`
- `Independent Medical Examination (IME)`   (note: IME, not "QME")
- `Panel Qualified Medical Examination (PQME)`
Do not string-match these. Key off `AppointmentTypeId` (Guid) or send both the id and the name.

The clean read model already exists:
- `AppointmentDto` (`src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentDto.cs`)
  is the scalar payload.
- `AppointmentWithNavigationPropertiesDto` (same folder) wraps it with `Patient`, `AppointmentType`,
  `Location`, `DoctorAvailability`, attorneys, employer, injuries, claim examiner, insurance.
  Note: this DTO has NO Doctor (consistent with section 3).

---

## 3. Doctor

- Representation: separate `FirstName` + `LastName` (+ `Email`, `Gender`) on the `Doctor` entity
  (`Domain/Doctors/Doctor.cs:18-25`). Not an id or name on the appointment.
- One doctor per office, enforced by unique index `IX_AppEntity_Doctors_TenantId_Unique`, seeded as
  the office owner (`Domain/Doctors/DoctorProfileDataSeedContributor.cs:15-53`). To get the doctor
  for an appointment, read the single Doctor row in that appointment's office database.
- You can send first + last name. Caveat: in the fallback seed path `FirstName` can be `""` and the
  office/tenant surname is used as `LastName` (`DoctorProfileDataSeedContributor.cs:104-109`);
  treat empty FirstName as valid.
- Cross-check: the portal's own full appointment-assembly component `PacketTokenResolver` (loads
  across 14 repositories, `Domain/AppointmentDocuments/Templates/PacketTokenResolver.cs`) never
  resolves a Doctor - confirming the doctor is effectively the office identity.

---

## 4. Files and MinIO (verified against the live deployed server)

Config in `Domain/CaseEvaluationDomainModule.cs:153-192` (method `ConfigureBlobStoring`). All 8
document/packet containers route to MinIO via `Volo.Abp.BlobStoring.Minio`.

Deployed values (read live from container env on the portal server, secrets not reproduced here):

| Item | Value | Notes |
|---|---|---|
| Provider | MinIO (`Volo.Abp.BlobStoring.Minio`) | one bucket shared by all containers |
| Bucket | `case-evaluation-documents` | env `BlobStoring__Minio__BucketName` |
| Endpoint | `minio:9000` | Docker-network service name, host:port, no scheme |
| TLS | off (`WithSsl=false`) | plain HTTP |
| Credentials | `BlobStoring__Minio__AccessKey` / `SecretKey`, from env `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` in the server's deploy `.env` | NOT included here on purpose - see section 9 |
| Container/host | `hcs-patient-portal-minio-1`, image `minio/minio:latest`, Docker network `hcs-patient-portal_default` | port 9000 NOT published to the LAN (only reverse-proxy 80/443 are) |

Object-key layout (verified two ways: ABP v10 source AND by reading MinIO's on-disk tree on the
server). ABP's MinIO name calculator prepends a scope segment to the logical blob name; the
container name is NOT part of the key (single shared bucket). Actual object key =
`tenants/{tenantId:D}/{logicalBlobName}` when a tenant is in scope (always, for appointment files),
or `host/{logicalBlobName}` for host-scope blobs (only office logos live there).

Real layouts observed on disk (GUIDs genericized):

- Packets: `case-evaluation-documents/tenants/{tenantId-dashed}/{tenantId-dashed}/{appointmentId-dashed}/packet/{kind}/{guid32}.pdf`
  - `{kind}` in `patient` | `doctor` | `attorneyclaimexaminer` (all 3 generated per appointment)
  - the tenant id appears TWICE: once from ABP's `tenants/{id}/` prefix, once from the job's own
    path segment. Built at `Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs:157`.
- Documents: `case-evaluation-documents/tenants/{tenantId-dashed}/{tenantId-nodash}/{appointmentId-nodash}/{guid32}`
  - no file extension; note the tenant + appointment segments here use the no-dash ("N") GUID
    format, unlike packets which use the dashed form. Built at
    `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:333`.

So: the `BlobName` stored on the DB rows is the LOGICAL key (relative, no bucket, no `tenants/`
prefix). To fetch an object directly from MinIO you must prepend `tenants/{tenantId}/` and target
bucket `case-evaluation-documents`. Recommendation: do not hand-reconstruct keys; either (a) have
the portal include the fully-qualified object keys in the push payload, or (b) fetch via a portal
download endpoint (section 8). Direct S3 access couples you to ABP's internal key scheme.

Are all files uploaded before approval? NO:
- Required "package" documents are created as `Pending` rows with a `(pending-upload)` placeholder
  blob at SUBMISSION time (`Application/Notifications/Handlers/PackageDocumentQueueHandler.cs`,
  subscribes to `AppointmentSubmittedEto`), then uploaded later by the patient via an emailed
  verification-code link - frequently AFTER approval.
- Ad-hoc documents can be uploaded any time including after approval
  (`Application/AppointmentDocuments/DocumentUploadGate.cs:59` allows `Approved`).
- Packets are rendered by an async Hangfire job after approval; they reach `Generated` seconds to
  minutes later and can fail/retry. Status per kind is in `AppointmentPacket.Status`
  (`PacketGenerationStatus`: `Generating=1` / `Generated=2` / `Failed=3`).
- Document review state is `AppointmentDocument.Status` (`DocumentStatus`: `Uploaded=1` /
  `Accepted=2` / `Rejected=3` / `Pending=4`).

---

## 5. Lifecycle and timing (the events you can hook)

1. Booking submitted -> `AppointmentSubmittedEto` -> `PackageDocumentQueueHandler` queues required
   document rows as `Pending` (no files yet).
2. Staff approves via `POST api/app/appointment-approvals/{id}/approve`
   (`HttpApi/Controllers/Appointments/AppointmentApprovalController.cs:34`) ->
   `AppointmentApprovalAppService.ApproveAppointmentAsync` (`Application/Appointments/AppointmentsAppService.Approval.cs`).
   This stamps `AppointmentApproveDate = UtcNow`, sets status `Approved`, and publishes:
   - `AppointmentStatusChangedEto(ToStatus=Approved)` (`AppointmentManager.cs:366`)
   - `AppointmentApprovedEto` (`AppointmentsAppService.Approval.cs:129`)
3. `AppointmentStatusChangedEto(Approved)` -> `PacketGenerationOnApprovedHandler`
   (`Domain/AppointmentDocuments/Handlers/PacketGenerationOnApprovedHandler.cs`) enqueues (on UoW
   commit) the Hangfire job `GenerateAppointmentPacketJob`.
4. The job renders all 3 packet kinds via the WeasyPrint packet-renderer sidecar, saves each PDF to
   MinIO, marks the `AppointmentPacket` row `Generated`, and publishes `PacketGeneratedEto` per kind
   (`GenerateAppointmentPacketJob.cs:221`).
5. Documents may continue to arrive after approval (patient uploads / staff review flips to
   `Accepted`).

Implication: the earliest moment a full case (data + all packets) exists is after the 3
`PacketGeneratedEto` events, not at approval. Documents may still be incomplete even then.

---

## 6. Recommended trigger + payload contract (to be built on the portal side)

Trigger: add a local event handler that, on `PacketGeneratedEto`, checks whether all 3 packet kinds
for the appointment are `Generated`; when so, enqueue the push (via a new outbox mirroring
`Notifications/Outbox/`). Also add a manual "Push to Case Tracker" button that reuses the same path,
and a follow-up sync when a document later flips to `Accepted`/`Uploaded`.

Suggested JSON envelope (Gesco cross-project convention - the Case Tracker intake endpoint should
accept this shape; values below use the confirmed field names):

```json
{
  "data": {
    "appointmentId": "<guid>",
    "tenantId": "<office-guid>",
    "facilityId": "<Location.FacilityId>",
    "confirmationNumber": "A00065",
    "status": "Approved",
    "approvedAtUtc": "2026-07-22T18:30:00Z",
    "submittedAtUtc": "2026-07-20T14:05:00Z",
    "appointmentType": { "id": "<guid>", "name": "Panel Qualified Medical Examination (PQME)" },
    "appointmentDateLocal": "2026-08-15",
    "appointmentTimeLocal": "09:30",
    "timeZone": "America/Los_Angeles",
    "durationMinutes": 60,
    "patient": {
      "firstName": "<synthetic>", "middleName": null, "lastName": "<synthetic>",
      "phoneNumber": "<synthetic>", "phoneNumberType": "Home", "cellPhoneNumber": null
    },
    "doctor": { "firstName": "<synthetic>", "lastName": "<synthetic>" },
    "files": {
      "bucket": "case-evaluation-documents",
      "packets": [
        { "kind": "patient", "objectKey": "tenants/<tid>/<tid>/<aid>/packet/patient/<guid>.pdf" }
      ],
      "documents": [
        { "documentName": "<label>", "status": "Accepted",
          "objectKey": "tenants/<tid>/<tid-nodash>/<aid-nodash>/<guid>" }
      ]
    }
  },
  "meta": { "requestId": "<guid>", "timestamp": "<utc>" },
  "errors": []
}
```

Notes for the receiver:
- Treat `appointmentId` as the idempotency key (upsert on it; ignore re-delivery).
- `appointmentType.name` is free text per office; branch on `id` if you need type logic.
- Send timestamps as UTC; send the slot as local date + local time + IANA zone (the portal stores
  the slot as clinic-local wall-clock with no offset).
- Expect a follow-up sync for late-arriving documents; do not assume the first push is complete.

---

## 7. Multi-tenancy (must-know)

The portal is database-per-office. Each office is a separate ABP tenant with its own SQL database;
tenant is resolved by subdomain. Appointment Guids are globally unique, but the shared MinIO is
namespaced by `tenants/{tenantId}/`. Every push must carry `tenantId` and `facilityId` so the Case
Tracker knows which office the case belongs to.

---

## 8. Pull alternative (if the portal exposes an API instead of pushing)

Read endpoints that already exist:
- `AppointmentWithNavigationPropertiesDto` via the appointments app-service, incl.
  `GetByConfirmationNumberAsync` (`Application/Appointments/AppointmentsAppService.cs:269`).
- Packet + document download via `AppointmentPacketsAppService` / `AppointmentDocumentsAppService`.
Auth: OpenIddict. Password grant is disabled, so a machine caller needs a client-credentials /
PKCE token (a client must be registered). Budget for this if you pull.

---

## 9. Gaps and open decisions (need Gesco + IT)

1. Shared-MinIO reachability. MinIO is on the portal host's internal Docker network, plain HTTP,
   port 9000 NOT published to the LAN. A Case Tracker on another host cannot reach it today.
   Options: (a) expose MinIO on the LAN with TLS + a dedicated scoped access key for Case Tracker
   (least privilege - do NOT share the root credentials); (b) the portal pushes file bytes to Case
   Tracker or a shared store; (c) co-locate. This is an infra decision.
2. Credentials. The MinIO root credentials live in the portal server's deploy `.env`
   (`MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD`). They are intentionally not in this document. If the
   shared-bucket route is chosen, create a dedicated MinIO service account / access key scoped to
   read-only on `case-evaluation-documents` rather than handing over root.
3. The push itself. RESOLVED 2026-07-28: trigger, outbox, POST client, the document-update feed and
   the change re-push are all BUILT and merged (PRs #393 + #395 -> main `8a1568eb`), shipping disabled
   behind `CaseTrackerPushEnabled`. Only the reconcile GET and failure visibility remain -- see
   `case-tracker-api-contract.md` §J for the current split.
4. No `DurationMinutes` field - compute from the slot.
5. Timezone: slot date/time is clinic-local with no offset; audit/approve times are UTC. Agree on a
   convention (proposal in section 6).
6. Deployment lag: the `FacilityId` field landed on `main` on 2026-07-22 (PR #379). Confirm it is in
   the deployed build and that offices have populated it (legacy rows default to empty string)
   before relying on it as the join key.

---

## 10. Verification notes

- Field names, types, nullability, formats, enums, lifecycle, events: verified by reading the
  cited source files on branch `main` @ `100a617c`.
- MinIO deployed config, bucket, network exposure, and real object-key layout: verified live on the
  portal server (`appoint-portal`, LAN 192.168.101.37) by inspecting container env and MinIO's
  on-disk tree. No file contents were opened; GUIDs are genericized here.
- ABP key-prefix behavior: verified against ABP v10 source
  (`DefaultMinioBlobNameCalculator.Calculate`) AND confirmed empirically on disk (`host/` +
  `tenants/{id}/` at the bucket root).
- Not verified: whether the deployed DB already has `FacilityId` populated per office (see 9.6);
  whether Case Tracker's host can be granted network access to MinIO (infra).
