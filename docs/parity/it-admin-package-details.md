---
feature: it-admin-package-details
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\DocumentManagementModule\PackageDetailDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DocumentManagementModule\DocumentPackageDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DocumentManagementModule\DocumentDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\document-management\
old-docs:
  - data-dictionary-table.md (PackageDetails, DocumentPackages, Documents, AppointmentDocumentTypes)
audited: 2026-05-01
re-verified: 2026-05-03
status: IMPLEMENTED 2026-05-03 - pending testing
priority: 2
strict-parity: true
internal-user-role: ITAdmin (and possibly StaffSupervisor for package mgmt)
depends-on: []
required-by:
  - clinic-staff-appointment-approval           # approval triggers package-doc creation per AppointmentTypeId
  - external-user-appointment-package-documents # this is the package the user uploads to
---

# IT Admin -- Package details (master data)

## Purpose

The "package" is a structured set of documents that must be filled out and uploaded for a given appointment type. IT Admin (or Staff Supervisor) configures:

- Master `Documents` (the catalog of forms available, e.g., "Patient Intake Form", "Medical Authorization", "Joint Declaration").
- `PackageDetails` rows -- one per `AppointmentTypeId` -- defining the named package (e.g., "PQME Standard Package").
- `DocumentPackages` rows -- many-to-many linking `Documents` to `PackageDetails` -- defining what's in each package.

When an appointment is approved, the system reads `PackageDetails` for the appointment's type, finds linked `Documents`, inserts an `AppointmentDocument` row per document.

> **CORRECTION 2026-05-03 [VERIFIED against OLD source].** OLD's auto-queue
> on approval does NOT read `PackageDetail`. The method
> `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail`
> (P:\PatientPortalOld\PatientAppointment.Domain\DocumentModule\AppointmentDocumentDomain.cs:394+)
> downloads from hardcoded AWS S3 buckets (`aws.patientPacket`,
> `aws.doctorPacket`, `aws.attorneyClaimExaminer`) and creates
> `AppointmentNewDocument` rows with `DocumentStatusId = Accepted`. The
> `PackageDetail` / `DocumentPackage` tables are master-data CRUD only --
> they exist for IT Admin to maintain the catalog but are NOT wired into
> the approval workflow.
>
> **Implication:** Phase 5 implements the master-data CRUD only. The
> NEW `PackageDocumentQueueHandler` design (read PackageDetail on approve
> -> queue AppointmentDocument rows) is a deliberate IMPROVEMENT over OLD's
> hardcoded bucket reads, scheduled for Phase 12. This is treated as a
> framework-level deviation per strict-parity rules, not a behavior
> deviation -- end-user behavior is the same (package docs queued on
> approval) only the source of "which docs" changes.

**Strict parity with OLD.**

## OLD behavior (binding)

### Schema

`PackageDetails` (per data dict):

- `PackageDetailId` (PK)
- `AppointmentTypeId` (FK -- which appointment type this package serves; nullable allows generic packages)
- `PackageName` (varchar 50)
- `StatusId` (Active/Delete)

`DocumentPackages`:

- `DocumentPackageId` (PK)
- `PackageDetailId` (FK)
- `DocumentId` (FK to `Documents`)
- `StatusId`
- Audit fields

`Documents`:

- `DocumentId` (PK)
- `DocumentName`
- `DocumentFilePath` (path to the master template file -- the blank PDF/DOCX form that's emailed to users for filling)
- `StatusId`

### CRUD operations

Standard CRUD:

- `POST /api/PackageDetails` -- create a package
- `GET /api/PackageDetails` -- list
- `GET /api/PackageDetails/{id}` -- detail
- `PUT/PATCH /api/PackageDetails/{id}` -- update
- `DELETE /api/PackageDetails/{id}` -- soft delete (StatusId = Delete)

For `DocumentPackages` (the linking table):

- `POST /api/packagedetails/{packageDetailId}/DocumentPackages` -- add a Document to a Package
- `DELETE /api/packagedetails/{packageDetailId}/DocumentPackages/{id}` -- remove

For `Documents` (master):

- Standard CRUD on `/api/Documents`

### Critical OLD behaviors

- **One package per AppointmentType** (typical) but schema permits multiple packages per type. UI presumably picks the active one.
- **Document templates (the blank forms)** stored on disk; `Documents.DocumentFilePath` references them. Used as email attachments when package docs are queued (per spec line 483-485).
- **Soft delete** via `StatusId`. Inactive packages don't queue docs.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/DocumentManagementModule/PackageDetailDomain.cs` | Package CRUD |
| `PatientAppointment.Domain/DocumentModule/AppointmentDocumentTypeDomain.cs` | Document-type CRUD (categorizes docs e.g. "ID Verification", "Medical History") |
| `patientappointment-portal/.../document-management/...` | UI |

## NEW current state

- `AppointmentPacketsAppService.cs` exists in `Application/AppointmentDocuments/` -- aligns with this audit.
- TO VERIFY presence of `Document` master entity, `DocumentPackage` linking entity, `PackageDetail` entity in NEW.
- `AppointmentTypeId` linkage TO VERIFY.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| `Document` entity (master template catalog) | OLD | NEW: PRESENT (Domain/Documents/Document.cs) | **DONE** -- Phase 1.2 entity has Name + BlobName + ContentType + IsActive. [IMPLEMENTED 2026-05-03 - pending testing] | B |
| `PackageDetail` entity (named package per appointment type) | OLD | NEW: PRESENT (Domain/PackageDetails/PackageDetail.cs) | **DONE** -- Phase 1.2 entity has PackageName + AppointmentTypeId + IsActive. Note: NEW also has a separate `AppointmentPacket` entity (per-appointment merged-PDF feature; orthogonal to Phase 5's per-AppointmentType template). [IMPLEMENTED 2026-05-03 - pending testing] | B |
| `DocumentPackage` link entity (many-to-many) | OLD | NEW: PRESENT (Domain/PackageDetails/DocumentPackage.cs) | **DONE** -- composite key (PackageDetailId, DocumentId), IsActive flag. [IMPLEMENTED 2026-05-03 - pending testing] | B |
| AppointmentType <-> Package association | OLD | NEW: PRESENT (PackageDetail.AppointmentTypeId FK) | **DONE** [IMPLEMENTED 2026-05-03 - pending testing] | I |
| Soft delete | OLD `StatusId` | ABP `ISoftDelete` | **DONE** -- ABP's `FullAuditedAggregateRoot` provides ISoftDelete out of the box. [IMPLEMENTED 2026-05-03 - pending testing] | -- |
| Document template files | OLD: local path | NEW: `IBlobStorage` blob | **DONE** -- `MasterDocumentsContainer` blob marker added (Domain/BlobContainers/MasterDocumentsContainer.cs). DocumentsAppService.CreateAsync / ReplaceFileAsync route uploads through it. [IMPLEMENTED 2026-05-03 - pending testing] | -- |
| Permissions | -- | -- | **DONE** -- `Documents.{Default,Create,Edit,Delete}` + `PackageDetails.{Default,Create,Edit,Delete,ManageDocuments}` registered in CaseEvaluationPermissions.cs + provider; granted to IT Admin in InternalUserRoleDataSeedContributor. [IMPLEMENTED 2026-05-03 - pending testing] | I |
| One active package per AppointmentType (OLD invariant) | OLD enforced at PackageDetailDomain.cs:48-53 | NEW: PackageDetailsAppService.EnsureNoActiveDuplicateAsync | **DONE** -- error code `OneActivePackageDetailPerAppointmentType` mirrors OLD validation message. [IMPLEMENTED 2026-05-03 - pending testing] | B |
| Auto-queue on appointment approval reads from this | OLD does NOT use PackageDetail (correction above) | -- | **DEFERRED** to Phase 12 -- queue handler will read PackageDetail (NEW improvement). Phase 5 master-data CRUD landing first is sufficient for Phase 12 to consume. | B |

## Internal dependencies surfaced

- `Documents` master + `IBlobStorage` for template files

## Branding/theming touchpoints

- Master document template files (logo, copy) -- separate concern from app UI.

## Replication notes

### ABP wiring

- **Entities:** `Document`, `AppointmentPacket`, `AppointmentPacketDocument`. All `FullAuditedAggregateRoot<Guid>, IMultiTenant`.
- **`AppointmentPacketsAppService`** -- already exists; verify CRUD methods present.
- **Document master upload:** IT Admin uploads blank template files via `IBlobStorage`; stored URL in `Document.FilePath`.
- **Package-to-document linking:** `IAppointmentPacketsAppService.AddDocumentAsync(packetId, documentId)` + `RemoveDocumentAsync(...)`. Or expose nested AppService per OLD's pattern.
- **Auto-queue on approve:** `PackageDocumentQueueHandler` reads `AppointmentPacket where AppointmentTypeId = appointment.AppointmentTypeId AND IsActive`, picks first row (or all if multi-package per type allowed -- TO DEFINE), iterates linked Documents, inserts AppointmentDocument rows.

### Things NOT to port

- Local file paths -> `IBlobStorage`.
- `StatusId` -> `IsActive` + `ISoftDelete`.

### Verification (manual test plan)

1. IT Admin creates a Document (uploads blank template PDF) -> file in blob storage
2. IT Admin creates an AppointmentPacket for AppointmentType=PQME -> success
3. IT Admin links Documents to the Packet -> success
4. External user books PQME -> approves -> AppointmentDocument rows inserted matching the linked Documents -> patient receives email with attachments
5. IT Admin deactivates a Document -> future appointments don't include it; existing AppointmentDocument rows unchanged
6. IT Admin deletes a Packet (soft) -> not used for new appointments
