---
stage: 4
title: Document upload + review UIs (D1-D4)
date: 2026-05-04
scope: D1 package docs, D2 ad-hoc docs, D3 JDF, D4 staff review
status: research-only
---

# Stage 4 Research: Document Flows (D1-D4)

This doc collects the OLD ground-truth, the NEW Phase 14 backend that already
landed, and a frontend-only implementation plan to satisfy D1-D4. The Angular
shell `AppointmentDocumentsComponent` (`angular/src/app/appointment-documents/
appointment-documents.component.ts`) and `AppointmentPacketComponent` are
already implemented for the unified ad-hoc + staff-review path; the gap is
**(a)** wiring the package + JDF upload variants to the dedicated backend
endpoints, **(b)** building the anonymous verification-code upload page, and
**(c)** rendering JDF role/visibility gates.

---

## 0. Phase 14 backend summary (NEW state at this audit)

All four flows share the unified entity `AppointmentDocument`
(`src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/
AppointmentDocument.cs`:27-174):

| Field | Notes |
|---|---|
| `IsAdHoc` (bool) | Set true by `UploadStreamAsync` (general/ad-hoc path). |
| `IsJointDeclaration` (bool) | Set true by `UploadJointDeclarationAsync`. |
| `VerificationCode` (Guid?) | Set on package-doc `CreateQueued`; lets anonymous patient upload via emailed link. |
| `Status` (DocumentStatus) | Pending / Uploaded / Accepted / Rejected. Defaults Uploaded; Pending only on auto-queue. |
| `RejectionReason` / `RejectedByUserId` | Populated by `RejectAsync`. |
| `BlobName` | Tenant-prefixed path in `IBlobContainer<AppointmentDocumentsContainer>`; placeholder `(pending-upload)` for queued rows. |

`AppointmentDocumentsAppService` methods (file
`src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/
AppointmentDocumentsAppService.cs`):

| Method | Lines | Auth | Purpose |
|---|---|---|---|
| `GetListByAppointmentAsync(appointmentId)` | 64-76 | `AppointmentDocuments.Default` | Returns all rows for the appointment ordered by CreationTime desc. |
| `UploadStreamAsync(...)` | 78-156 | `AppointmentDocuments.Create` | Ad-hoc path. Sets `IsAdHoc = true`, no status gate. Internal users auto-Accept. |
| `UploadPackageDocumentAsync(documentId, ...)` | 170-200 | `AppointmentDocuments.Create` | Updates an existing Pending row created by `PackageDocumentQueueHandler`. Gates: `EnsureAppointmentApprovedAndNotPastDueDate` + `EnsureNotImmutable`. |
| `UploadJointDeclarationAsync(appointmentId, ...)` | 210-274 | `AppointmentDocuments.Create` | Creates `IsJointDeclaration = true` row. Gates: ApprovedAndNotPastDueDate + `EnsureAme` + `EnsureCreatorIsAttorney`. |
| `UploadByVerificationCodeAsync(documentId, code, ...)` | 286-316 | `[AllowAnonymous]` | Anonymous upload; `EnsureVerificationCodeMatches` IS the auth. |
| `DownloadAsync(id)` | 393-412 | `AppointmentDocuments.Default` | Returns blob stream + filename + ContentType. |
| `DeleteAsync(id)` | 414-432 | `AppointmentDocuments.Delete` | Hard delete + best-effort blob cleanup. |
| `ApproveAsync(id)` | 434-460 | `AppointmentDocuments.Approve` | Status -> Accepted, clears RejectionReason, publishes `AppointmentDocumentAcceptedEto`. |
| `RejectAsync(id, RejectDocumentInput)` | 462-490 | `AppointmentDocuments.Approve` | Status -> Rejected, stores reason, publishes `AppointmentDocumentRejectedEto`. |
| `RegeneratePacketAsync(appointmentId)` | 492-504 | `AppointmentPackets.Regenerate` | Enqueues `GenerateAppointmentPacketArgs` background job. |

HTTP routes (manual controllers per `HttpApi/CLAUDE.md` rule):

| Route | Method | Source | Notes |
|---|---|---|---|
| `/api/app/appointments/{appointmentId}/documents` | GET | `AppointmentDocumentController.cs`:27 | List. |
| `/api/app/appointments/{appointmentId}/documents` | POST (multipart) | `AppointmentDocumentController.cs`:33-50 | Ad-hoc upload (D2). |
| `/api/app/appointments/{appointmentId}/documents/{id}/upload-package` | POST (multipart) | `AppointmentDocumentController.cs`:88-106 | Package upload (D1, authenticated). |
| `/api/app/appointments/{appointmentId}/documents/upload-jdf` | POST (multipart) | `AppointmentDocumentController.cs`:112-130 | JDF upload (D3). |
| `/api/app/appointments/{appointmentId}/documents/{id}/download` | GET | line 53-57 | Stream file. |
| `/api/app/appointments/{appointmentId}/documents/{id}` | DELETE | line 60-63 | Delete. |
| `/api/app/appointments/{appointmentId}/documents/{id}/approve` | POST | line 65-68 | Staff approve (D4). |
| `/api/app/appointments/{appointmentId}/documents/{id}/reject` | POST (`{ reason }`) | line 71-74 | Staff reject (D4). |
| `/api/app/appointments/{appointmentId}/packet/regenerate` | POST | line 77-80 | Trigger packet rebuild. |
| `/api/public/appointment-documents/{id}/upload-by-code/{verificationCode}` | POST (multipart) | `PublicDocumentUploadController.cs`:48-67 | Anonymous email-link upload. |

The auto-queue handler (`PackageDocumentQueueHandler`,
`src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/
PackageDocumentQueueHandler.cs`) subscribes to `AppointmentApprovedEto`, looks
up `PackageDetail` by AppointmentTypeId, and calls
`AppointmentDocumentManager.CreateQueuedAsync` to insert one Pending row per
Document with a fresh `VerificationCode = Guid.NewGuid()`.

Recurring jobs (Phase 14b):

| Job file | Purpose |
|---|---|
| `src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Jobs/PackageDocumentReminderJob.cs` | Daily 08:30 PT; emails uploader at T-N before DueDate when package/JDF docs are still Pending/Rejected. |
| `src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs` | Daily; transitions appointment to CancelledNoBill + publishes `AppointmentAutoCancelledEto` when JDF still Pending at cutoff. |

---

## 1. D1 -- Package document upload UI

### 1.1 OLD source

- Backend: `P:\PatientPortalOld\PatientAppointment.Domain\
  AppointmentRequestModule\AppointmentDocumentDomain.cs`
  - `Update(AppointmentDocument)` lines 109-182 -- per-document upload
  - `UpdateValidation` lines 90-107 -- Approved/RescheduleRequested gate +
    DueDate gate
  - `GetValidation(appointmentId, userTypeId, verificationCode)` lines 64-75
    -- VerificationCode lookup
  - `AddAppointmentDocumentsAndSendDocumentToEmail(appointment, ...)` lines
    394+ -- the auto-queue routine that runs after staff approval; downloads
    DOCX templates from S3, replaces tokens, inserts `AppointmentNewDocument`
    rows, emails the patient with `EmailTemplate.AppointmentDocumentAddWithAttachment`
- Models: `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\
  AppointmentDocument.cs` (139 lines, schema `spm.AppointmentDocuments`,
  columns include `DocumentPackageId`, `IsJoinDeclaration`, `UserType`,
  `VerificationCode Guid`, `DocumentStatusId`, `RejectionNotes`,
  `ResponsibleUserId`, `AppointmentDocumentTypeId`)
- Frontend: `P:\PatientPortalOld\patientappointment-portal\src\app\
  components\appointment-request\appointment-documents\`
  - `list/appointment-document-list.component.html` lines 1-89
  - `edit/appointment-document-edit.component.html` lines 1-40 (modal)
  - `edit/appointment-document-edit.component.ts` lines 120-153
    (`onFileChange`: `file.size >= (1000 * 1024)` silent abort -- the
    "1 MB cap" bug, base64 encoding via `FileReader.readAsBinaryString` +
    `btoa()`, extension whitelist `DEFAULT_IMAGE_FILE_EXTENSTION = .doc,.docx,.pdf`)

### 1.2 OLD UX flow (D1)

1. Staff approves an appointment -> backend inserts one
   `AppointmentDocuments` row per Document configured in the
   `PackageDetails` -> `DocumentPackages` -> `Documents` graph for the
   AppointmentTypeId.
2. Email lands with one link per document:
   `{clientUrl}/appointment-documents/{appointmentDocumentId}?verificationcode={guid}`
   (path constructed in `AppointmentDocumentDomain.cs`:438 +
   `AddAppointmentDocumentsAndSendDocumentToEmail`).
3. External user clicks the link OR logs in and navigates to
   `/appointment-documents/{appointmentId}` (page header at
   `list/appointment-document-list.component.html`:3-6 with Back button
   for non-internal roles).
4. List shows columns Document Name / Document Status (Pending|Uploaded|
   Accepted|Rejected with status pill classes
   `rescheduled-no-bill / approved / rejected / pending`,
   `appointment-new-document-list.component.html`:36-47) / Document Type /
   Rejection Note / File Name / Action (Edit + Delete icons).
5. Click pencil icon -> opens upload modal
   (`edit/appointment-document-edit.component.html`:1-40). Modal shows
   read-only Document Name + File Path + Choose File button. On submit,
   PUTs `/api/Appointments/{id}/AppointmentDocuments/{docId}` with `fileData`
   (base64) + `fileName` + `fileExtention`.
6. After successful upload backend toast "The document has been uploaded
   successfully." (file: `appointment-document-edit.component.ts`:61).

### 1.3 OLD storage model

Table `spm.AppointmentDocuments` (OLD) -- key columns from
`AppointmentDocument.cs`:

- `AppointmentDocumentId` PK
- `AppointmentId` FK -> `Appointments`
- `AppointmentDocumentTypeId` FK -> `AppointmentDocumentTypes` (nullable)
- `DocumentPackageId` int (links the row to its parent package
  `DocumentPackages`, which links to `PackageDetails` via `PackageDetailId`)
- `DocumentName` (NVARCHAR 50)
- `DocumentFilePath` (NVARCHAR 500) -- local file path
  (`wwwroot/Documents/submittedDocuments/...`)
- `DocumentAwsFilePath` (NVARCHAR 255) -- legacy AWS S3 key, dead-code in OLD
- `DocumentStatusId` FK -> `DocumentStatuses` (1 Uploaded / 2 Accepted /
  3 Rejected / 4 Pending / 5 Deleted; source
  `Models/Enums/DocumentStatuses.cs`)
- `IsJoinDeclaration` (bool, NOT a typo of "Joint" in OLD)
- `RejectedById` int? + `RejectionNotes` (NVARCHAR 500)
- `ResponsibleUserId` int -- the staff member responsible for review
- `UserType` int -- which kind of email recipient this row was queued for
  (Patient / CreatedBy / Adjuster -- enum `UserTypesForEmail`)
- `VerificationCode` Guid -- per-document anonymous-upload key
- Audit columns (`CreatedById`, `CreatedDate`, `ModifiedById`, `ModifiedDate`)

Master data hierarchy: `PackageDetails` (1 per AppointmentType) ->
`DocumentPackages` (link rows: PackageDetailId + DocumentId) ->
`Documents` (the actual catalog of forms; not visible in this audit but
referenced via `DocumentName` copy on the AppointmentDocuments row).

### 1.4 OLD email content (D1)

- Template constant: `EmailTemplate.AppointmentDocumentAddWithAttachment`
  (`Patient-Appointment-Documents-Add-With-Attachment.html`, source
  `P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\
  ApplicationConstants.cs`:69, also `AppointmentDocumentDomain.cs`:457)
- Recipients (per `AddAppointmentDocumentsAndSendDocumentToEmail`):
  - Patient.Email when present (line 467)
  - `appointment.CreatedById`'s `User.EmailId` when no patient email
  - For PQME / PQME-REVAL: also Adjuster + Claim Examiner
    (`AppointmentDocumentDomain.cs`:643+)
- Subject: `"Appointment Request Approved (Patient: {first} {last} -
  Claim: {claim} - ADJ: {adj})"` (line 491)
- URL pattern: `{clientUrl}/appointment-new-documents/{appointmentId}` for
  the package landing page. Per-document deep links use the verification
  code (`AppointmentDocumentDomain.cs`:438 -- documentUploadUrl).
- Status-change emails (per `AppointmentDocumentDomain.SendDocumentEmail`
  lines 220-303):
  - Accepted -> `EmailTemplate.PatientDocumentAccepted` (or
    `PatientDocumentAcceptedRemainingDocs` when other docs still pending,
    line 244)
  - Rejected -> `PatientDocumentRejected` /
    `PatientDocumentRejectedRemainingDocs`
  - Uploaded -> `PatientDocumentUploaded` (sent to PrimaryResponsibleUserId,
    line 297)

### 1.5 NEW current state (D1)

Backend Phase 14: complete.

- `UploadPackageDocumentAsync(documentId, fileName, contentType, fileSize,
  content)` updates the queued row.
- `PackageDocumentQueueHandler` auto-creates Pending rows on
  `AppointmentApprovedEto`.
- `PackageDocumentReminderJob` runs daily.
- `DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate` +
  `EnsureNotImmutable` enforce OLD's gates.

Frontend gap: `AppointmentDocumentsComponent.upload()` currently calls
`service.upload(...)` which hits POST `/documents` -- that goes through
`UploadStreamAsync` -> `IsAdHoc = true`. Pending rows ARE listed by
`getList`, but a Pending row has no `Upload` button bound to
`/upload-package/{id}`.

Generated proxy
(`angular/src/app/proxy/appointment-documents/appointment-document.service.ts`)
exposes only `getList`, `upload`, `buildDownloadUrl`, `delete`, `approve`,
`reject`, `regeneratePacket`. Missing: `uploadPackage`, `uploadJdf`,
`uploadByVerificationCode`. Per `angular/src/app/CLAUDE.md` Convention 2,
proxy/ must NOT be hand-edited; the fix is to run `abp generate-proxy` after
the backend lands. Verify by re-running and diffing.

`AppointmentDocumentDto` model lacks `isAdHoc`, `isJointDeclaration`,
`verificationCode`, `documentPackageId` -- proxy needs regen so the UI can
distinguish row types.

### 1.6 D1 implementation plan (frontend only)

Files to touch (all under `angular/src/app/appointment-documents/`):

1. **Regenerate proxy** -- run `abp generate-proxy -t ng -m app -u
   https://localhost:44368/` (per `docs/frontend/PROXY-SERVICES.md`); diff
   to confirm new fields + `uploadPackage` / `uploadJdf` /
   `uploadByVerificationCode` methods appear in
   `appointment-document.service.ts`.
2. **`appointment-documents.component.ts`**:
   - Extend `AppointmentDocumentDto` rendering: Pending rows show no
     uploaded-file metadata; show "Upload required" subtitle + Upload button
     wired to `service.uploadPackage(appointmentId, doc.id, formData)`.
   - For `IsAdHoc = true` rows keep current row layout.
   - For `IsJointDeclaration = true` rows render the JDF visibility gate
     (Section 3 below).
   - Add `documentTypeBadge` showing `Package` / `Ad-hoc` / `JDF`.
3. **`appointment-documents.component.html`**:
   - Add a top-section legend "Required documents" listing Pending package
     rows separately from completed/ad-hoc rows.
   - Reuse the existing reject modal markup (lines 124-163).
4. **Status badge map**: extend `statusBadgeClass` with `Pending` ->
   `bg-warning text-dark`, label "Pending" (currently the default branch
   returns "Uploaded" for any non-Approved/Rejected status -- ensure
   Pending is treated separately).
5. **Permission check**: external users with
   `AppointmentDocuments.Create` see the Upload button on Pending rows;
   staff see it too (matrix in design doc Section 8).

### 1.7 D1 acceptance criteria

(Cross-reference design doc Section 12.)

- Approving an appointment auto-creates one Pending row per Document in the
  AppointmentType's PackageDetail.
- Pending rows render with "Upload required" + Upload button.
- Upload routes to `POST /documents/{id}/upload-package`; response 200
  flips status badge to Uploaded.
- Upload after appointment is unapproved or after DueDate -> server returns
  the localized "UploadAfterApproval" / "UploadAfterDueDate" error; toast
  surfaces it.
- Internal user upload lands as Accepted (green) immediately.
- Cannot re-upload an Accepted row (server `EnsureNotImmutable`).
- Email link with valid `verificationCode` opens public upload page; bad
  code -> 401-style error toast (D1.b -- see Section 5 below).

### 1.8 D1 gotchas

- **Verification-code link.** OLD's link format `?verificationcode={guid}`
  must be parsed; NEW route is path-based:
  `/upload?docId={id}&code={verificationCode}` per design doc Section 7.
  Confirm with backend: `PublicDocumentUploadController.cs`:48 expects
  `{id}/upload-by-code/{verificationCode}` in the path. The Angular public
  page should accept either query-string OR path-style and adapt.
- **File size cap.** NEW is 25 MB
  (`AppointmentDocumentConsts.MaxFileSizeBytes`); OLD claimed 10 MB but the
  bug `file.size >= (1000 * 1024)` made it ~1 MB. Frontend toast must read
  the actual cap from the constants module.
- **MIME / extension.** Backend `EnsureValidFileFormat`
  (`AppointmentDocumentsAppService.cs`:511-542) limits to PDF + JPG + PNG
  via magic-byte sniff. UI must surface this clearly; OLD allowed `.doc` +
  `.docx` -- those are now rejected. Strict-parity exception logged in
  design doc Section 14.
- **Package contents per AppointmentType.** OLD reads from
  `PackageDetails` configured by IT Admin (audit doc
  `docs/parity/it-admin-package-details.md` referenced from
  `external-user-appointment-package-documents.md`:135). NEW seeds an
  initial PackageDetail set; verify
  `CaseEvaluationSeedIds.AppointmentTypes.Ame/Pqme` map to seeded packages
  in the migrator. Frontend has no direct dependency -- it just renders
  whatever Pending rows the backend created.
- **Package-doc cutoff.** `Documents.PackageDocumentReminderDays`
  setting key (default 7), per Phase 14b row in
  `external-user-appointment-package-documents.md`:128. Frontend doesn't
  consume this directly; only relevant for tooltip "Reminder will be sent
  on..." copy if added.

---

## 2. D2 -- Ad-hoc document upload UI

### 2.1 OLD source

- Backend: `P:\PatientPortalOld\PatientAppointment.Domain\
  AppointmentRequestModule\AppointmentDocumentDomain.cs`:305-385
  (`SendDocumentEmail(AppointmentNewDocument)` -- the ad-hoc sibling table
  was `AppointmentNewDocuments`; OLD uses two tables, one per concept).
  `AppointmentNewDocumentDomain.cs` (320 lines) is the parallel domain
  service.
- Frontend: `P:\PatientPortalOld\patientappointment-portal\src\app\
  components\appointment-request\appointment-new-documents\`
  - `list/appointment-new-document-list.component.html` lines 1-103
    (page header "Document Manager", "Upload Document" button on top-right,
    rx-table with status pills lines 36-47, edit + delete icons gated
    `*ngIf="!(event.documentName === 'PATIENT PACKET')"` line 61, and a
    `linkExpired` branch lines 80-102 for the JDF auto-cancel notice)
  - `add/appointment-new-document-add.component.html`
  - `edit/appointment-new-document-edit.component.html`

### 2.2 OLD UX flow (D2)

1. From appointment-edit page, "Upload Documents" button opens
   `/appointment-new-documents/{appointmentId}`.
2. List page shows the table with all ad-hoc + auto-queued docs (because
   in OLD `AppointmentNewDocuments` is the unified table and "PATIENT
   PACKET" / "DOCTOR PACKET" rows are inserted on approval as
   AppointmentNewDocument rows -- see `AppointmentDocumentDomain.cs`:528
   and 612).
3. Click "Upload Document" button -> upload modal: text input for Document
   Name + Choose File + base64 upload.
4. Status statuses match D1: Pending(4), Uploaded(1), Accepted(2),
   Rejected(3), Deleted(5).
5. **No status-gate validation** in OLD's `AppointmentNewDocumentDomain`
   for the upload (verified in the existing parity audit). Patient can
   upload at any appointment state.

### 2.3 OLD storage model (D2)

Table `spm.AppointmentNewDocuments` (separate from AppointmentDocuments).
Schema overlaps with AppointmentDocuments minus `DocumentPackageId` and
`IsJoinDeclaration`. NEW collapses this into `AppointmentDocument` with
`IsAdHoc = true` (Phase 1.6 decision in design doc Exception 2).

### 2.4 OLD email content (D2)

- Templates: `EmailTemplate.PatientNewDocumentUploaded`,
  `PatientNewDocumentAccepted`, `PatientNewDocumentRejected`
  (`ApplicationConstants.cs`:49-51).
- Subject: `"Patient Appointment Portal - ({Patient Info}) - Appointment
  document is uploaded by user."` (`AppointmentDocumentDomain.cs`:367).
- Recipients on Uploaded status: the appointment's `PrimaryResponsibleUserId`
  user (`AppointmentDocumentDomain.cs`:375-378) -- **bug-flag**: OLD calls
  `.PrimaryResponsibleUserId` directly; if null this NREs. NEW uses
  null-safe access (Phase 14b `DocumentUploadedEmailHandler`, design doc
  Exception 5).
- URL pattern: none specific; uploader is already authenticated.

### 2.5 NEW current state (D2)

`UploadStreamAsync` (`AppointmentDocumentsAppService.cs`:78-156) is fully
wired and currently the default `POST /documents` endpoint hit by
`AppointmentDocumentsComponent.upload()`. **Already works.** Internal-user
auto-Accept already implemented (`IsInternalActorAsync` line 506-509,
returns true when `AppointmentDocuments.Approve` is granted).

### 2.6 D2 implementation plan

Mostly already done. Frontend deltas:

1. Confirm proxy exposes `IsAdHoc` field after regen (Section 1.6 step 1).
2. **`appointment-documents.component.ts`**:
   - Render `IsAdHoc = true` rows with an "Ad-hoc" pill (use design-token
     `--text-muted` for neutral styling).
   - Ad-hoc rows allow re-upload (delete + re-upload via the existing
     upload form) at any appointment status.
3. No backend changes.

### 2.7 D2 acceptance criteria

- External user uploads at any appointment status (Pending, Approved,
  Rejected) -- no UploadAfterApproval error.
- File appears in unified list with `IsAdHoc` badge.
- Internal user upload appears as Accepted immediately.
- Delete works for any role (own uploads for external users; all rows for
  staff).

### 2.8 D2 gotchas

- **Role restrictions on ad-hoc upload.** Per design doc Section 4 matrix,
  Patient + Adjuster + Attorney + Claim Examiner can all upload ad-hoc;
  Clinic Staff and Supervisor do NOT upload (read-only); IT Admin can.
  No backend role check on ad-hoc -- relies on
  `AppointmentDocuments.Create` permission grant per role profile. Verify
  the seed permissions match this matrix in
  `CaseEvaluationPermissionDefinitionProvider.cs` before sign-off.
- **PrimaryResponsibleUserId nullable.** Don't send the responsible-user
  email when null; OLD bug-fix already in handler.
- **Ad-hoc and package docs share the list.** Visual distinction via badge
  is required so the patient knows which docs are required vs. extras.

---

## 3. D3 -- JDF (Joint Declaration Form) upload UI

### 3.1 OLD source

- Backend: `P:\PatientPortalOld\PatientAppointment.Domain\
  AppointmentRequestModule\AppointmentJointDeclarationDomain.cs` (322
  lines, fully read).
  - `Add(AppointmentJointDeclaration)` lines 65-101 -- creates a row +
    saves base64 file to local fs.
  - `UpdateValidation` lines 102-125 -- enforces: status == Approved AND
    AppointmentTypeId != PQME ("Appointment type is not valid. Please
    upload appropriate document.") AND DueDate >= now ("You can not upload
    document after specified due date.").
  - `Update` lines 127-179 -- external user re-upload sets
    `DocumentStatusId = Uploaded`. **Bug**: internal user path lines
    173-178 only sets `RejectedById = UserClaim.UserId` and never updates
    Status; design doc Exception 3 documents the NEW fix.
  - `SendDocumentEmail` lines 200-282 -- emits Accepted/Rejected emails
    with remaining-docs flavor when `remainingDocumentCount > 0`.
- Frontend: `P:\PatientPortalOld\patientappointment-portal\src\app\
  components\appointment-request\appointment-joint-declarations\`
  - `list/appointment-joint-declaration-list.component.html`
  - `edit/appointment-joint-declaration-edit.component.html` lines 1-39
    (modal with read-only Document Name + Appointment Type + File Path +
    Choose File button).

### 3.2 OLD UX flow (D3)

1. Booking attorney opens an AME-typed appointment.
2. JDF tab/section visible with one row representing the JDF requirement.
3. Click pencil icon -> upload modal (`appointment-joint-declaration-edit
   .component.html`).
4. Submit triggers `PUT /api/Appointments/{id}/AppointmentJointDeclarations
   /{jdfId}` -> `AppointmentJointDeclarationDomain.Update`.
5. Status -> Uploaded; staff reviews; on accept -> Accepted email
   (`EmailTemplate.PatientDocumentAccepted` /
   `PatientDocumentAcceptedRemainingDocs`).
6. If JDF still Pending at cutoff (`SystemParameters.
   JointDeclarationUploadCutoffDays`, found in
   `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\
   SystemParameter.cs`:69-70 + `vSystemParameter.cs`:59-60): scheduled
   job auto-cancels the appointment. The patient/attorney sees the
   `linkExpired` branch on the new-documents list page
   (`appointment-new-document-list.component.html`:80-102 -- "This
   appointment has been auto cancelled in absence of Joint Declaration
   Document. Please book an appointment again.").

### 3.3 OLD storage model (D3)

Table `spm.AppointmentJointDeclarations`, separate from AppointmentDocuments.
Columns include `AppointmentJointDeclarationId`, `AppointmentId`,
`JointDeclarationFilePath`, `DocumentStatusId`, `RejectedById`,
`RejectionNotes`, `ModifiedById/Date`. NEW collapses into `AppointmentDocument`
with `IsJointDeclaration = true` (design doc Exception 2; parity audit
`external-user-appointment-joint-declaration.md`:78-83).

### 3.4 OLD email content (D3)

- On upload (status -> Uploaded): `EmailTemplate.PatientDocumentUploaded`
  -> uploader + PrimaryResponsibleUserId.
- On accept: `PatientDocumentAccepted` or
  `PatientDocumentAcceptedRemainingDocs` (when other docs still pending,
  link `{clientUrl}/appointment-documents/?appointmentid={id}&appointmenttype=
  {typeId}` -- file `AppointmentJointDeclarationDomain.cs`:242-244).
- On reject: `PatientDocumentRejected` /
  `PatientDocumentRejectedRemainingDocs`.
- On auto-cancel: per parity audit, all stakeholders (Booker, Patient,
  Applicant Attorney, Defense Attorney, Claim Examiner, Primary Insurance,
  Employer, Office Mailbox) via `IAppointmentRecipientResolver`. NEW
  template: `JDFAutoCancelled`.

System parameter: `SystemParameters.JointDeclarationUploadCutoffDays`
(`SystemParameter.cs`:69-70). No default published in OLD seeds; NEW will
need a settings key. Recommend
`Documents.JointDeclarationUploadCutoffDays`, default 3 (verify against
OLD seed when DB migration audit runs).

### 3.5 NEW current state (D3)

Backend: `UploadJointDeclarationAsync`
(`AppointmentDocumentsAppService.cs`:210-274) creates a new row with
`IsJointDeclaration = true`. Gates: `EnsureAppointmentApprovedAndNotPastDueDate`
(line 234), `EnsureAme(appointment.AppointmentTypeId)` (line 235),
`EnsureCreatorIsAttorney(appointment, CurrentUser.Id, roleNames)` (line
238). Routes: `POST /api/app/appointments/{appointmentId}/documents/
upload-jdf` (multipart). `JointDeclarationAutoCancelJob` is registered.

Frontend: not yet wired -- no JDF upload button anywhere; the unified list
will show JDF rows once they exist but creates need a dedicated button.

### 3.6 D3 implementation plan

1. **`appointment-documents.component.ts/.html`**: add a "JDF" upload
   section visible only when:
   - Appointment is AME or AME-REVAL
     (`appointment.appointmentTypeCode === 'AME' ||
     appointment.appointmentTypeCode === 'AME-REVAL'`; need this field on
     the appointment view model -- coordinate with Stage 3 audit for
     `appointment-view` page);
   - Current user is Applicant Attorney OR Defense Attorney
     (`PermissionService.getGrantedPolicy('CaseEvaluation.AppointmentDocuments.Create')`
     plus role check via ABP `currentUser.roles.includes(...)`);
   - AND user is the booking attorney (i.e. `appointment.creatorId ===
     currentUser.id`). If any condition fails -> JDF section hidden.
2. JDF section UI:
   - Title "Joint Declaration Form" + subtitle "Required for AME
     appointments. Upload before {DueDate - cutoff days}."
   - File input + Upload button -> `service.uploadJdf(appointmentId,
     formData)` (post-proxy-regen).
   - Existing JDF row (if any) shows in unified list with `JDF` badge
     (use design token `--brand-secondary` or status badge).
3. **Auto-cancel notice**: when appointment status is `CancelledNoBill`
   AND last status-change reason is "JDF-not-uploaded" (need this on the
   appointment DTO -- coordinate with Session A `AppointmentManager`
   transitions), render a notice card "This appointment has been auto
   cancelled in absence of Joint Declaration Document. Please book an
   appointment again." -- mirrors OLD's `linkExpired` branch.

### 3.7 D3 acceptance criteria

- JDF section is visible on AME/AME-REVAL appointments only.
- JDF section is hidden for Patient, Adjuster, Claim Examiner.
- Booking attorney sees Upload button; non-booking attorney sees read-only.
- Upload on PQME -> server `JdfRequiresAmeAppointment` error toast.
- Upload by non-booking-attorney -> `JdfUploaderMustBeBookingAttorney`
  error toast.
- Upload past DueDate -> `DocumentUploadAfterDueDate` error toast.
- Successful upload -> JDF row in list with Uploaded badge.
- Staff Approve / Reject works via existing buttons.
- Auto-cancel notice card appears when appointment is CancelledNoBill
  due to JDF.

### 3.8 D3 gotchas

- **AME-REVAL deferred.** Backend `EnsureAme` only checks
  `CaseEvaluationSeedIds.AppointmentTypes.Ame`; AME-REVAL seed is deferred
  per `DocumentUploadGate.cs`:73-83 comment. Frontend visibility logic
  must match: if AME-REVAL is not yet seeded, do NOT show JDF section for
  it. Coordinate with future PackageDetails seed work.
- **Role + creator check.** `EnsureCreatorIsAttorney`
  (`DocumentUploadGate.cs`:93-117) checks both
  `appointment.IdentityUserId == currentUserId` AND role name in
  `{Applicant Attorney, Defense Attorney}` (case-insensitive). If your
  user has both roles via ABP role membership, listing both passes.
  Frontend check should mirror this exactly to avoid showing buttons that
  the server will reject.
- **Cutoff system parameter.** Backend reads
  `JointDeclarationCutoff.IsAtOrPastCutoff` predicate; the actual setting
  key + default should be confirmed in `JointDeclarationAutoCancelJob.cs`
  before the UI quotes a date. If unsure, render generic "before due date"
  copy.
- **One JDF per appointment.** OLD's data model permits multiple via FK
  without unique constraint, but business rule is one. NEW allows
  multiple `IsJointDeclaration = true` rows technically; frontend should
  hide the Upload button once a JDF row exists with status != Rejected.

---

## 4. D4 -- Clinic staff document review UI

### 4.1 OLD source

- Backend: there is no approve/reject in OLD. The closest is
  `AppointmentDocumentDomain.Update` lines 157-160 -- if caller is internal
  set status = Accepted; else status = Uploaded. No reject path was wired
  to UI. Documented in design doc
  `clinic-staff-document-review-design.md`:25-31 (Overview) +
  Exception 4.
- Frontend OLD: list view in
  `appointment-document-list.component.html`:67-77 only shows Edit + Delete
  icons (no approve/reject); the ad-hoc list
  `appointment-new-document-list.component.html`:59-69 same.

### 4.2 OLD UX flow (D4)

OLD does NOT have a clinic-staff document-review surface. Staff could
delete documents and re-upload on behalf of the patient (which silently
flipped Status to Accepted via the internal-user code path). NEW adds the
full approve/reject workflow as design doc Exception 4.

### 4.3 OLD storage model (D4)

Status enum 1 Uploaded / 2 Accepted / 3 Rejected / 4 Pending / 5 Deleted
(`Models/Enums/DocumentStatuses.cs`). Rejection reason persisted in
`AppointmentDocuments.RejectionNotes` (NVARCHAR 500). RejectedById int?.

### 4.4 OLD email content (D4)

- Accepted: `EmailTemplate.PatientDocumentAccepted` (subject "Patient
  Appointment Portal - ({Patient Info}) - Appointment document is
  Accepted." -- `AppointmentDocumentDomain.cs`:258).
- Rejected: `PatientDocumentRejected` (subject "...Appointment document
  is Rejected." -- line 275).
- Token: rejection reason rendered as `<b> Rejection Reason: </b>
  {RejectionNotes}` (line 278).
- Recipient: appointment's CreatedBy user (line 269 -- uploader).

### 4.5 NEW current state (D4)

**Fully implemented.** UI: `AppointmentDocumentsComponent`
(`angular/src/app/appointment-documents/appointment-documents.component.ts`).

- Approve flow: `approve()` (lines 195-206) -> `service.approve(...)` ->
  `POST /documents/{id}/approve`. Status badge flips to Approved (green);
  Approve button hides. Single-click, no confirmation modal.
- Reject flow: `openRejectModal()` (line 208) opens modal; `submitReject()`
  (line 222) posts `{ reason }` to `/documents/{id}/reject`. Validation:
  `reason.trim().length > 0 && reason.length <= 500` (lines 227-234).
- Modal markup: `appointment-documents.component.html`:124-163 (per design
  doc Section 7).
- Permission gate: `canApprove` getter line 105-107 reads
  `CaseEvaluation.AppointmentDocuments.Approve` permission.
- Backend `ApproveAsync`/`RejectAsync` publish Etos consumed by the email
  handler (Phase 14b): uploader receives `PatientDocumentAccepted` /
  `PatientDocumentRejected` template per design doc Section 7.

### 4.6 D4 implementation plan

Mostly complete. Verification deltas:

1. **Confirm rejection-reason field**: textarea, 4 rows, maxlength=500,
   character counter "X / 500", red when over (matches design doc Section
   7 spec). Verify in
   `appointment-documents.component.html`:124-163 -- if absent, add the
   counter span + class binding `[class.text-danger]="reason.length > 500"`.
2. **Confirm rejection reason persistence**: refresh shows reason below
   document name in red (status badge component handles via
   `doc.rejectionReason` field on DTO -- already present in `models.ts`:20).
3. **Document the change-log integration**: per the task brief, status
   transitions must appear in the change log. Confirm
   `AppointmentChangeLog` (Stage 3 audit) consumes
   `AppointmentDocumentAcceptedEto` and `AppointmentDocumentRejectedEto`.
   If not, file follow-up issue (out of scope for D4 frontend; depends on
   Session B's change-log handler wiring).

### 4.7 D4 acceptance criteria

(Cross-reference design doc Section 16 verification checklist.)

- Staff sees Approve + Reject buttons on all documents (Pending /
  Uploaded / Rejected) except Approved rows.
- Approve is one click; status flips green immediately.
- Reject opens modal; reason required; max 500 chars; counter visible.
- Rejected status shows reason below document name in red.
- Approve / Reject calls fire the correct endpoint and trigger packet
  refresh via `documentsChanged` emitter (line 86).
- Non-permitted external user does not see the buttons (DOM hidden) AND
  cannot call the endpoints (server returns 403).

### 4.8 D4 gotchas

- **`canApprove` is a getter, not a signal.** It re-evaluates on every
  change-detection pass which is fine for Default CDS but expensive under
  OnPush; keep `ChangeDetectionStrategy.Default` (already set, line 28).
- **Reject reason maxlength** is enforced both client (textarea
  `maxlength="500"`) and server (`AppointmentDocumentsAppService.cs`:466
  -- 500 char trim). Don't relax either side.
- **Audit change-log entries.** OLD did not have an audit-log entry per
  doc status change; NEW Phase 14 publishes Etos, but the
  `AppointmentChangeLog` consumer is owned by Session B. Verify before
  user-facing claims.

---

## 5. Cross-cutting items

### 5.1 Anonymous email-link upload page (used by D1 + D3)

Backend: `PublicDocumentUploadController` is live at
`/api/public/appointment-documents/{id}/upload-by-code/{verificationCode}`.

Frontend gap (separate from `AppointmentDocumentsComponent`): a
standalone Angular route is needed for the patient who clicks the email
link without logging in. Recommended path:
`angular/src/app/public-document-upload/public-document-upload.component.ts`
(new file).

UX:

- Route: `/public/upload/:docId` with query string `?code={guid}`.
- Page is OUTSIDE the authenticated shell (no top-bar nav, just brand
  header + footer).
- Single form: read-only Document Name (from server-side echo), Choose
  File, Upload button.
- POST to `/api/public/appointment-documents/{docId}/upload-by-code/{code}`
  via the regenerated proxy.
- Success message: "Document uploaded. You can close this page."
- Error: 401-style "Un unauthorized user" for bad code (OLD-verbatim
  string, localized via `Document:UnauthorizedVerificationCode`).

### 5.2 File-type / size validation summary

| Limit | Source | Value |
|---|---|---|
| Max file size | `AppointmentDocumentConsts.MaxFileSizeBytes` (verify) -> `AppointmentDocumentManager.cs`:42-45 | 25 MB (per design doc Exception 1; OLD UI claimed 5 MB / 10 MB but capped at ~1 MB due to bug) |
| Allowed MIME / extension | `AppointmentDocumentsAppService.EnsureValidFileFormat` (lines 511-542) | `.pdf`, `.jpg`, `.jpeg`, `.png` -- magic-byte sniff verifies header. **Note**: this is stricter than OLD's `.doc + .docx + .pdf` whitelist; logged as parity exception. |
| Rejection reason | DTO `RejectDocumentInput.reason` + entity `AppointmentDocument.RejectionReason` | string, server-trims, max 500 chars (server `RejectAsync` line 466) |

### 5.3 Status enum mapping

| OLD `DocumentStatuses` | NEW `DocumentStatus` (`models.ts`:3-8) | Notes |
|---|---|---|
| Pending = 4 | `Pending` (Phase 1.7 added; not in proxy enum -- only on backend) | Used for queued package rows. |
| Uploaded = 1 | `Uploaded = 1` | Default status post-upload. |
| Accepted = 2 | `Approved = 2` | NEW renames "Accepted" -> "Approved" in TypeScript proxy; backend label is `DocumentStatus.Accepted`. |
| Rejected = 3 | `Rejected = 3` | -- |
| Deleted = 5 | (none) | NEW uses hard delete via `DELETE` endpoint. |

The frontend proxy `DocumentStatus` enum lacks `Pending`. After regen,
verify it includes `Pending = 4` so the UI can render Pending package
rows; if not, file a backend fix to expose the enum value to the proxy
(currently `models.ts`:3 declares the type-union as `1 | 2 | 3` only).

### 5.4 Per-flow matrix

| Flow | Trigger | Endpoint | Auth | Status flow | Email template (uploader) | Recipients |
|---|---|---|---|---|---|---|
| D1 Package upload | Click email link or in-app upload on Pending row | POST `/documents/{id}/upload-package` (auth) OR POST `/api/public/appointment-documents/{id}/upload-by-code/{code}` (anon) | `AppointmentDocuments.Create` OR anon + verification code | Pending -> Uploaded; Internal -> Accepted | `PatientDocumentUploaded` | Uploader + PrimaryResponsibleUserId |
| D2 Ad-hoc upload | "Upload" button on appointment-view | POST `/documents` | `AppointmentDocuments.Create` | (no Pending) -> Uploaded; Internal -> Accepted | `PatientNewDocumentUploaded` | Uploader + PrimaryResponsibleUserId |
| D3 JDF upload | Booking attorney clicks JDF Upload | POST `/documents/upload-jdf` | `AppointmentDocuments.Create` + role + creator + AME guards | (new row) -> Uploaded | `PatientDocumentUploaded` (per Phase 14b handler) | Uploader + PrimaryResponsibleUserId |
| D4 Approve | Staff click Approve | POST `/documents/{id}/approve` | `AppointmentDocuments.Approve` | -> Accepted | `PatientDocumentAccepted` | Uploader |
| D4 Reject | Staff click Reject + reason | POST `/documents/{id}/reject` `{ reason }` | `AppointmentDocuments.Approve` | -> Rejected | `PatientDocumentRejected` | Uploader |
| D3 Auto-cancel | Recurring job at cutoff | Internal Hangfire | -- | Appointment -> CancelledNoBill | `JDFAutoCancelled` | Booker, Patient, Applicant + Defense Attorneys, Claim Examiner, Primary Insurance, Employer, Office Mailbox |

### 5.5 Files to touch (frontend, all paths absolute)

| Path | Change | Owner flow |
|---|---|---|
| `W:\patient-portal\replicate-old-app\angular\src\app\proxy\appointment-documents\appointment-document.service.ts` | Regenerate via `abp generate-proxy` (do NOT hand-edit) -- exposes `uploadPackage`, `uploadJdf`, `uploadByVerificationCode` | D1 + D3 + 5.1 |
| `W:\patient-portal\replicate-old-app\angular\src\app\proxy\appointment-documents\models.ts` | Regenerate -- adds `isAdHoc`, `isJointDeclaration`, `verificationCode`, `Pending` to enum | D1 + D2 + D3 |
| `W:\patient-portal\replicate-old-app\angular\src\app\appointment-documents\appointment-documents.component.ts` | Add badges (Package/Ad-hoc/JDF), Pending-row Upload button, JDF section visibility, JDF upload action | D1 + D2 + D3 |
| `W:\patient-portal\replicate-old-app\angular\src\app\appointment-documents\appointment-documents.component.html` | Markup updates per badge/section additions | D1 + D2 + D3 |
| (new) `W:\patient-portal\replicate-old-app\angular\src\app\public-document-upload\public-document-upload.component.ts` + `.html` | Anonymous upload page; routed `/public/upload/:docId?code=` | 5.1 |
| `W:\patient-portal\replicate-old-app\angular\src\app\app.routes.ts` | Register `/public/upload/:docId` route OUTSIDE the auth-required shell | 5.1 |

No backend file touches required for D1-D4 (Phase 14 + 14b are complete).

### 5.6 Verification (manual smoke)

Run end-to-end for each flow per the per-flow design-doc verification
checklists (already enumerated -- D1 lines 239-255, D2 lines 128-135,
D3 lines 155-169, D4 lines 330-348).

Top integration smoke tests:

1. Approve appointment -> Pending package rows exist -> patient gets email
   -> click link (no login) -> upload -> success toast -> staff sees row
   as Uploaded.
2. Patient uploads ad-hoc document at any status -> appears in unified
   list with Ad-hoc badge.
3. Booking attorney uploads JDF on AME -> success; on PQME ->
   `JdfRequiresAmeAppointment` error toast.
4. JDF auto-cancel: backdate DueDate to within cutoff with Pending JDF;
   trigger job manually -> appointment flips to CancelledNoBill;
   stakeholder emails fire.
5. Staff rejects ad-hoc with reason -> external user receives
   `PatientDocumentRejected` email -> sees reason in list -> re-uploads
   the same row -> reason clears.
