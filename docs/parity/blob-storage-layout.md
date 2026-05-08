# Blob storage layout — OLD vs NEW (bug-batch-2 #8)

Documents the deliberate deviation between OLD's bucket-keyed file
layout and NEW's tenant-and-appointment-keyed `IBlobContainer` layout.
Audit answer for bug-batch-2 issue #8 ("Folder structure parity with
OLD"); the recommendation is **Option 1 — keep NEW layout, document
deviation**.

## OLD layout

OLD writes uploaded documents to `wwwroot/Documents/<bucket>/` with the
bucket name keyed off `aws.*` config keys in
`P:\PatientPortalOld\PatientAppointment.Api\server-settings.json:34-46`.
Eleven distinct buckets:

| OLD bucket name | OLD config key | OLD use |
|---|---|---|
| `documentBluePrint` | `aws:documentBluePrint` | Per-AppointmentType blueprint stub document, manually configured by IT Admin |
| `submittedDocuments` | `aws:submittedDocuments` | Generic per-appointment uploads from external users (Patient / AA / DA / CE) |
| `userSignature` | `aws:userSignature` | Patient e-signature image attached to JDF |
| `patientPacket` | `aws:patientPacket` | Pre-built packet output for the Patient role |
| `attorneyClaimExaminer` | `aws:attorneyClaimExaminer` | Pre-built packet output for AA / DA / CE roles |
| `doctorPacket` | `aws:doctorPacket` | Pre-built packet output for the doctor / clinic |
| `attornypacketpqme` | `aws:attornypacketpqme` | Per-attorney PQME packet variant |
| `attornypacketame` | `aws:attornypacketame` | Per-attorney AME packet variant |
| `claimexaminerpacketame` | `aws:claimexaminerpacketame` | CE-specific AME packet output |
| `claimexaminerpacketpqme` | `aws:claimexaminerpacketpqme` | CE-specific PQME packet output |
| `jointagreementletter` | `aws:jointagreementletter` | Joint Declaration Form uploads |

Within each bucket, OLD writes flat-by-filename — no per-appointment or
per-tenant subfolder. Ad-hoc upload at
`AppointmentNewDocumentsController.cs:109-118` lands in
`submittedDocuments` flat. Packet fan-out into per-recipient
sub-folders happens at packet-build time only:
`AmazonBlobStorage.cs:301-339` writes into the corresponding role
bucket above.

## NEW layout

NEW writes every document through ABP's
`IBlobContainer<AppointmentDocumentsContainer>` interface with a
single key shape regardless of role or origin:

```
{tenantSegment}/{appointmentId}/{Guid:N}
```

Where:
- `tenantSegment` is the current `ICurrentTenant.Id.ToString()` (or
  `host` when the upload is host-scoped — not used in this product)
- `appointmentId` is the parent `Appointment.Id` GUID
- The trailing `Guid:N` is a freshly minted blob name; the original
  filename is stored on the `AppointmentDocument` row's `Name`
  column, not in the blob path

Implementations:
- `AppointmentDocumentsAppService.UploadStreamAsync` at
  `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:111-113`
- `UploadJointDeclarationAsync` at `:242-244`
- `OverwriteUploadedFileAsync` at `:344-346`

Discrimination across the OLD bucket categories is achieved via
**columns on the entity row**, not folders:

| OLD bucket | NEW discriminator |
|---|---|
| `documentBluePrint` | `AppointmentDocument.IsBlueprint = true` |
| `submittedDocuments` | `IsBlueprint = false`, `IsAdHoc = true` |
| `userSignature` | Tracked separately in `JointDeclaration.SignatureImageBlobName` (no entity flag on `AppointmentDocument`) |
| `patientPacket` / `attorneyClaimExaminer` / `doctorPacket` / `attorneypacket{pqme,ame}` / `claimexaminerpacket{pqme,ame}` | Output of `AppointmentPacket.Render*` per role; rendered packets are written under the same `tenant/appointment/<guid>` shape and stored on `AppointmentDocument` rows tagged `IsPacket = true`. Per-recipient role is stored in `AppointmentPacket.Role` |
| `jointagreementletter` | `AppointmentDocument.IsJointDeclaration = true` |

## Why the deviation is safe

1. **No information loss.** Every OLD bucket has a NEW row-column
   discriminator. Any query that walked OLD's bucket folder list
   (e.g. "give me every JDF for this appointment") now walks the
   `AppointmentDocument` table with a `WHERE IsJointDeclaration = 1`
   filter, which is faster and does not require a directory listing.

2. **Tenant isolation is enforced.** OLD's flat-bucket layout had no
   tenant prefix because OLD was single-tenant. NEW prefixes every
   blob with `tenantSegment` so cross-tenant blob access is impossible
   even if a tenant guesses an `appointmentId` from another tenant
   (the blob path will not resolve to anything in their tenant
   container).

3. **ABP idiom.** ABP's "one entity = one container" pattern (every
   `IBlobContainer<TContainer>` is a logical category) lets us swap
   storage backends — local disk in dev, Azure Blob Storage in
   staging/prod — without touching the upload code. Splitting into
   eleven physical containers would force eleven sets of credentials
   and connection strings per environment.

4. **Packet renderer reuses one container.** OLD's bucket-per-packet
   model existed because the packet builder wrote directly to disk;
   NEW's packet renderer (`AppointmentPacketRenderer.RenderAsync` and
   role-specific subclasses) writes through the same
   `IBlobContainer<AppointmentDocumentsContainer>` and tags the row
   so a "give me all packets for appointment X" query is a single
   indexed lookup, not a directory traversal across multiple
   buckets.

## When to revisit

If a future feature requires Azure Blob Storage's per-container access
policies (e.g. "rotate the SAS token on the JDF bucket independently
of submittedDocuments") we may split into two containers
(`AppointmentDocumentsContainer` + `AppointmentJointDeclarationContainer`)
keeping the rest collapsed. That is a non-breaking refactor — entity
rows continue to point at their own blob names — and is tracked as a
future-enhancement item, not a parity blocker.

## Cross-references

- `_parity-flags.md` — see PARITY-FLAG-NEW-* entries for related deviations
- `docs/parity/2026-05-07-bug-batch-2.md` issue #8 — the audit that drove this doc
- `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` — every upload path passes through here
- ABP docs — [IBlobContainer](https://docs.abp.io/en/abp/latest/Blob-Storing) explains the one-entity-one-container idiom
