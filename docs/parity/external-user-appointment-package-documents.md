---
feature: external-user-appointment-package-documents
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-documents\
old-docs:
  - socal-project-overview.md (lines 405-413)
  - data-dictionary-table.md (AppointmentDocuments, AppointmentDocumentTypes, DocumentPackages, DocumentStatuses, Documents, PackageDetails)
audited: 2026-05-01
status: audit-only
priority: 1
strict-parity: true
depends-on:
  - external-user-appointment-request  # this flow triggers AFTER staff approval of the request
  - clinic-staff-document-review        # staff accept/reject side
---

# External user appointment package documents

## Purpose

After Clinic Staff approves an appointment, the system emails the patient (or the appointment creator) a structured **package** of documents to fill out and upload. The user clicks a link in the email -> redirected to a per-document upload page (or logs in + opens the appointment + navigates to the upload section). Each document in the package has its own row in `AppointmentDocuments`, can be Pending/Uploaded/Accepted/Rejected independently. Reminders fire if docs are still pending close to due date.

**Strict parity with OLD.**

## OLD behavior (binding)

### Trigger

When `Appointment.AppointmentStatus` transitions to `Approved`:

1. Clinic Staff (during approval) selects a `PackageDetail` for the appointment based on `AppointmentTypeId`.
2. `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail(appointment, appointmentId)` is invoked. It:
   - Reads `DocumentPackages` rows for the chosen `PackageDetailId`.
   - For each `Document` linked via the package, inserts an `AppointmentDocuments` row with: `DocumentStatusId = Pending`, `VerificationCode = Guid.NewGuid()`, `DocumentName` from the Document master, link via `AttachmentLink`.
   - Sends an email to the patient (or appointment creator if no patient email) with a link per document: `{clientUrl}/appointment-documents/{appointmentDocumentId}?verificationcode={code}`.

### Upload flow (per document)

User clicks link in email OR logs in + navigates to appointment + selects upload:

1. **`AppointmentDocumentDomain.GetValidation(appointmentId, userTypeId, verificationCode)`** -- if `verificationCode != Guid.Empty`, look up document by `(AppointmentId, UserType, VerificationCode)`. If not found -> `"Un unauthorized user"` error. (Allows non-logged-in users to upload via the email link without password auth -- security trades convenience for accessibility per OLD design.)
2. **`AppointmentDocumentDomain.UpdateValidation`:**
   - Appointment status must be `Approved` or `RescheduleRequested`. Else: `"Please upload documents after appointment is approved."`
   - `appointment.DueDate < DateTime.Now` -> `"You can not upload document after specified due date."`
3. **`AppointmentDocumentDomain.Update`:**
   - Decode `FileData` (Base64) -> save to `wwwroot/Documents/submittedDocuments/{confirmationNumber}_{name}_{id}_{timestamp}.{ext}` (OLD has commented-out AWS S3 paths -- file storage is local in current OLD code; AWS code is a relic).
   - For external users: set `DocumentStatusId = Uploaded`.
   - For internal users: set `DocumentStatusId = Accepted` (skips review).
   - Clear `RejectionNotes`.
   - Update `ModifiedById`, `ModifiedDate`.
   - Save.
   - **`SendDocumentEmail(appointmentDocument)`** -- emails to:
     - Uploader's email -- "Document uploaded" template (OLD: `EmailTemplate.PatientDocumentUploaded`)
     - PrimaryResponsibleUserId's email (the staff member responsible for the appointment) -- triggers staff review

### Per-document status flow

```
Pending (initial, after staff approval queues the package)
  -> Uploaded (user submits via link or login)
    -> Accepted (staff approves)
    -> Rejected (staff rejects with notes)
       -> back to Uploaded (re-upload allowed)
  -> Deleted (soft delete by user before upload, if allowed)
```

Status enum from `DocumentStatuses`: Pending, Uploaded, Accepted, Rejected, Deleted (TO VERIFY exact values).

### Reminders

Per spec doc lines 569-593:

- "Reminder for incomplete package documents (multiple reminders)" -- includes list of remaining documents
- "Reminder for due date approaching"
- "Reminder for due date approaching and package documents still pending"

Cutoff configured in `SystemParameters.ReminderCutoffTime`.

OLD implementation: reminders sent by a scheduled job (TO VERIFY -- look for `Scheduler` or `Hangfire`-equivalent in OLD).

### Critical OLD behaviors

- **`VerificationCode` per document** allows non-logged-in users to upload via email link. Each document has its own GUID. This is a valuable feature for patients without portal accounts (e.g., elderly patients).
- **Per-document accept/reject** -- staff can accept some docs and reject others in the same package.
- **Accepted documents cannot be modified** by external users (per spec line 493).
- **Rejection notes are emailed** to the uploader.
- **Internal user uploads auto-accept.**
- **Multi-reminder cadence** for pending docs.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDocumentDomain.cs` | `Add` (initial queue), `Update` (user upload), `UpdateValidation`, `Delete`, `SendDocumentEmail`, `AddAppointmentDocumentsAndSendDocumentToEmail` |
| `PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentDocumentsController.cs` | API endpoints |
| `patientappointment-portal/.../appointment-documents/{add,edit,list}/...` | Upload UI components |
| `Models.Enums.EmailTemplate.{PatientDocumentUploaded, PatientDocumentAccepted, PatientDocumentRejected}` | Templates |
| `DbEntities.Models.{AppointmentDocument, DocumentPackage, Document, PackageDetail}` | EF entities |

## NEW current state

### Per `Appointments/CLAUDE.md` and existing files

- `Application.Contracts/AppointmentDocuments/{AppointmentDocumentDto.cs, AppointmentPacketDto.cs, IAppointmentDocumentsAppService.cs, IAppointmentPacketsAppService.cs, RejectDocumentInput.cs}` -- DTOs and service interfaces present
- `Application/AppointmentDocuments/{AppointmentDocumentsAppService.cs, AppointmentPacketsAppService.cs}` -- impls present
- NEW separates the concept: **`AppointmentPackets`** (the package grouping) + **`AppointmentDocuments`** (individual docs). This maps cleanly to OLD's `PackageDetails` / `DocumentPackages` / `AppointmentDocuments` hierarchy.
- File storage: TO VERIFY -- NEW likely uses ABP's `IBlobStorage` abstraction (configurable to local filesystem, S3, Azure Blob).

### Known unknowns (TO VERIFY during impl)

- Does NEW implement `VerificationCode`-based unauthenticated upload? Likely NO (ABP defaults to authenticated access). **Strict parity says ADD it.**
- Is the package auto-queued on appointment approval? TO VERIFY in `AppointmentManager.UpdateAsync` or status-change handler.
- Are reminder jobs implemented? `Appointments/Notifications/Jobs/RequestSchedulingReminderJob.cs` exists; verify if document-specific reminder also exists.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Per-document `VerificationCode` for unauthenticated upload | GUID stored on `AppointmentDocuments` row | Likely missing | **Add `VerificationCode Guid` field to `AppointmentDocument`** + endpoint accepts code-based access without auth (with rate limiting) | B |
| Auto-queue package on approval | `AddAppointmentDocumentsAndSendDocumentToEmail` triggered at status=Approved | TO VERIFY | **Add subscription** to a `AppointmentApprovedEto` distributed event that creates the package documents | B |
| Per-document upload endpoint | `PUT /api/AppointmentDocuments/{id}` with FileData (base64) | NEW likely uses `[HttpPut("upload-file/{id}")]` with multipart | **Strict parity:** keep ABP-native multipart; OLD's base64 encoding is a workaround for the in-house HTTP client. Framework-allowed deviation. | -- |
| Status flow | Pending -> Uploaded -> Accepted/Rejected | Verify NEW DocumentStatus enum has Uploaded as separate from Pending and Accepted | **Verify enum values**; add Uploaded if missing | I |
| Internal user auto-accept on upload | `IsInternalUser != null` -> `Accepted` | TO VERIFY | **Add internal-user bypass** in upload AppService | I |
| Validation: appointment Approved/RescheduleRequested + before DueDate | OLD enforces in `UpdateValidation` | TO VERIFY | **Add `ValidatePackageDocumentUpload(appointment)` guard** in NEW AppService | B |
| Email on upload (to uploader + responsible user) | `SendDocumentEmail` triggered | NEW: `StatusChangeEmailHandler` exists; verify covers documents | **Add `DocumentUploadedEto` event + handler** | I |
| Reminder cadence | Multi-step reminders pre due date | Background job pattern; verify presence of doc-reminder job | **Add `PackageDocumentReminderJob`** scheduled via ABP Background Jobs (Hangfire integration) | I |
| File storage | OLD: local `wwwroot/Documents/submittedDocuments/`; AWS code commented out | NEW: should use `IBlobStorage` (ABP abstraction) | **Use `IBlobStorage`** with `Volo.Abp.BlobStoring.FileSystem` for local dev, swappable to S3/Azure later. Better than OLD. | -- |
| Per-document accept/reject | `RejectDocumentInput.cs` exists in NEW | Verify reject sends email with notes | **Verify `RejectAsync` method emits event** for email subscriber | I |
| Accepted documents read-only for external users | OLD enforces in update | TO VERIFY in NEW | **Add status check** in upload endpoint | I |

## Internal dependencies surfaced

- **PackageDetails master data** -- IT Admin / clinic staff configures which Documents go in which Package per AppointmentType. Audit slice: `it-admin-package-details.md` (or fold into `it-admin-master-data.md`).
- **Document master** -- the catalog of documents available across packages. Same audit slice as above.
- **Email templates: `PatientDocumentUploaded`, `PatientDocumentAccepted`, `PatientDocumentRejected`** -- TO LOCATE.
- **`PrimaryResponsibleUserId`** field on Appointment -- set by clinic staff at approval time. Audit slice: `clinic-staff-approve-reject-appointment.md`.

## Branding/theming touchpoints

- Email templates per status (Uploaded/Accepted/Rejected) -- subject + body
- Document upload page UI -- logo, primary color, page title
- Email subject: `"Patient Appointment Portal - ({Patient Info}) - Appointment document is {Status}."`

## Replication notes

### ABP wiring

- **Auto-queue package:** subscribe to `AppointmentStatusChangedEto` (where `NewStatus == Approved`). Handler reads PackageDetail for the AppointmentType, inserts `AppointmentDocument` rows with `Pending` status + `VerificationCode = Guid.NewGuid()`, sends email.
- **Unauthenticated upload via VerificationCode:** add `[AllowAnonymous]` endpoint `PUT /api/app/appointment-documents/{id}/upload-by-code/{verificationCode}`. Validates `(AppointmentDocumentId, VerificationCode)` matches; throws if not. Rate limit 5/hour per code.
- **`IBlobStorage`:** register `Volo.Abp.BlobStoring.FileSystem` in dev. Container per appointment: `appointment-{appointmentId}`. Blob name: `{requestConfirmationNumber}_{documentName}_{documentId}_{timestamp}.{ext}` (matches OLD naming).
- **Reminder job:** Hangfire `RecurringJob` running daily; queries `AppointmentDocuments` where `DocumentStatusId = Pending` AND appointment.DueDate within `SystemParameters.ReminderCutoffTime` days; sends reminder email + SMS.
- **Per-document accept/reject:** existing `IAppointmentDocumentsAppService.AcceptAsync` / `RejectAsync` (per `RejectDocumentInput.cs` presence). Emit `DocumentReviewedEto` event for email subscriber.

### Things NOT to port

- Local `wwwroot/Documents/...` file storage -- use `IBlobStorage`.
- Base64 in body -- use multipart upload.
- Commented-out AWS S3 code in OLD -- delete in NEW (parity is to OLD's WORKING code, not its TODO comments).

### Verification (manual test plan)

1. Approve appointment as Clinic Staff -> package documents auto-created (one row per Document in the chosen Package) -> email sent to patient/creator
2. Click link in email (no login) -> upload page loads -> upload PDF -> success
3. Login + open appointment -> see uploaded doc + remaining docs
4. Internal user uploads doc on behalf -> status = Accepted immediately
5. Staff rejects doc with notes -> uploader email arrives with notes -> re-upload succeeds
6. Try uploading after DueDate -> rejected with "You can not upload document after specified due date"
7. Try uploading on a Pending appointment (not yet approved) -> rejected
8. Reminder fires on day N before DueDate -> email arrives listing remaining docs
9. Try `verificationcode` link with bad code -> 401/403
10. Rate-limit verificationcode endpoint -> 6th request blocks

Tests:
- `AppointmentDocumentsAppServiceTests.UploadByCode_ValidCode_AcceptsFile`
- `..._InvalidCode_Throws`
- `..._AfterDueDate_Throws`
- `..._InternalUser_AutoAccepts`
- `..._RateLimited_BlocksAfterFifth`
- `PackageQueueOnApproval_CreatesDocumentsForAppointmentType`
