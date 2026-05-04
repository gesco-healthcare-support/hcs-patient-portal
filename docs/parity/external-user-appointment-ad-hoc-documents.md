---
feature: external-user-appointment-ad-hoc-documents
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentNewDocumentDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-new-documents\
old-docs:
  - data-dictionary-table.md (AppointmentNewDocuments)
  - socal-api-documentation.md (AppointmentNewDocuments section)
audited: 2026-05-01
re-verified: 2026-05-04
status: in-progress
priority: 1
strict-parity: true
depends-on:
  - external-user-appointment-request
---

# External user appointment ad-hoc documents

## Purpose

Beyond the structured **package documents** (post-approval), users can attach **ad-hoc / general documents** to an appointment at any time. Examples: supplemental medical records, ID verification scans, additional injury photos, supporting paperwork. These are stored in the separate `AppointmentNewDocuments` table and have a simpler workflow than package documents.

**Strict parity with OLD.** This is a SEPARATE feature from package documents per Q3 resolution 2026-05-01.

## OLD behavior (binding)

### Differences from package documents

| Aspect | `AppointmentDocuments` (package) | `AppointmentNewDocuments` (ad-hoc) |
|--------|----------------------------------|------------------------------------|
| Triggered by | Clinic Staff approval queues a structured set | User initiative, any time |
| Status gate on upload | Appointment must be Approved/RescheduleRequested + before DueDate | NO gate -- can upload anytime |
| `VerificationCode` (email-link upload) | Yes | No |
| `DocumentPackageId` linkage | Yes | No |
| Initial status | Pending (when queued) | Pending (external upload) or Accepted (internal upload) |
| Email recipients on upload | Uploader + Responsible User | Same: Uploader + Responsible User |
| Per-document accept/reject by staff | Yes | Yes |
| Reminders | Yes (multi-step) | No (no reminders for ad-hoc) |

### `AppointmentNewDocumentDomain.Add` (binding flow)

1. Look up appointment with includes: `Patient, AppointmentAccessors, AppointmentPatientAttorneys, AppointmentDefenseAttorneys, AppointmentInjuryDetails, CustomFieldsValues, AppointmentEmployerDetails`.
2. Check `vInternalUser` view for `UserId == UserClaim.UserId`. If found -> internal user.
3. **Status decision:**
   - Internal user -> `DocumentStatusId = Accepted` (auto-accept).
   - External user -> `DocumentStatusId = Pending`.
4. `ResponsibleUserId = appointment.PrimaryResponsibleUserId.Value` (the staff member responsible for this appointment).
5. `AttachmentLink = "<a href='{clientUrl}/appointment-new-documents/{appointmentId}'>Click here to upload</a>"` -- a link back to the upload page (used in emails).
6. `DocumentFilePath = appointmentNewDocument.FileName` (the local path; the file itself is stored elsewhere).
7. `DocumentAwsFilePath = appointmentNewDocument.DocumentAwsFilePath` (passthrough; AWS storage abandoned in OLD but field retained).
8. Insert row, commit.
9. **`SendDocumentEmail(appointmentNewDocument)`** -- emits email per status (Accepted/Rejected/Pending).

### `AppointmentNewDocumentDomain.Update` (re-upload or staff review)

1. Look up internal-user flag.
2. Look up appointment for `RequestConfirmationNumber`.
3. If `FileData` provided: rename to `{confirmationNumber}_{filename}_{documentId}_{timestamp}.{ext}` (TO PORT).
4. Set `DocumentFilePath`.
5. Clear `RejectionNotes`.
6. Update `ModifiedById`, `ModifiedDate`.
7. Save.
8. `SendDocumentEmail(...)`.

### Email subject patterns

- `"Patient Appointment Portal - ({PatientName} - Claim: {claim} - ADJ: {wcab}) - Appointment document is Accepted."`
- Same template for Rejected, Uploaded (Pending) statuses.
- Templates: `EmailTemplate.PatientDocumentAccepted` / `Rejected` / `Uploaded` (shared with package-document flow -- same templates).

### Critical OLD behaviors

- **Uses the same email templates as package documents** -- the `SendDocumentEmail` method on `AppointmentNewDocumentDomain` is structurally identical to `AppointmentDocumentDomain.SendDocumentEmail` and reuses the same `EmailTemplate.PatientDocument*` enums.
- **`AppointmentNewDocumentDomain` queries `AppointmentDocuments` table for "remaining doc count"** (line 177) -- a known cross-table reference. Strict parity: NEW should also surface remaining package-doc count in the upload-confirmation email.
- **No status restriction on upload** -- this differentiates it from package documents. User can upload ad-hoc docs even while appointment is Pending.
- **`ResponsibleUserId` required** on the row -- means appointment must have a `PrimaryResponsibleUserId` (set at staff approval). What happens if no responsible user assigned yet (e.g., during Pending)? OLD code throws NRE on `.Value` access. **Strict parity: handle the null case** -- skip the responsible-user email or fall back to a default.
- **No verification code / unauthenticated upload** -- only logged-in users can upload ad-hoc docs.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentNewDocumentDomain.cs` (320 lines) | Add, Update, Delete, SendDocumentEmail, GetBucketName |
| `PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentNewDocumentsController.cs` | API endpoints |
| `patientappointment-portal/.../appointment-new-documents/...` | Upload UI |
| `DbEntities.Models.AppointmentNewDocument` | EF entity |
| `EmailTemplate.PatientDocument{Accepted,Rejected,Uploaded}` (shared with package docs) | Templates |

## NEW current state

NEW's `AppointmentDocuments` folder (under `Application.Contracts/`) appears to handle ONE document concept. There's no separate `AppointmentNewDocuments` AppService.

**Strict parity decision:** NEW should support both flows under the existing `AppointmentDocuments` entity, distinguished by a flag (e.g., `IsAdHoc bool`) OR have separate entities. Recommend a flag for simplicity:

- `IsAdHoc = false` -> behaves like OLD `AppointmentDocuments` (package, with VerificationCode, DocumentPackageId)
- `IsAdHoc = true` -> behaves like OLD `AppointmentNewDocuments` (no VerificationCode, no DocumentPackageId required, no due-date gate)

Alternative: keep two entities. Cleaner separation but more code. Recommend the flag approach -- aligns with NEW's existing single-entity design and avoids the OLD confusion.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Two upload paths (package + ad-hoc) | 2 separate tables | 1 table | **Add `IsAdHoc bool` field on `AppointmentDocument`**; gate validation differently per flag | B | `[VERIFIED 2026-05-04]` -- `IsAdHoc` already exists since Phase 1.6 (`AppointmentDocument.cs`:71). Phase 14 sets the flag in `UploadStreamAsync` (the ad-hoc path) and gates the status/due-date validators on `!IsAdHoc`. |
| Ad-hoc upload available anytime | No status gate | Existing `UploadStreamAsync` has no status gate (which is correct for ad-hoc) | -- | -- | `[VERIFIED 2026-05-04]` -- existing AppService is already gateless for ad-hoc; Phase 14 keeps it that way and ADDS the gate only on the new `UploadPackageDocumentAsync` + `UploadJointDeclarationAsync` paths. |
| Internal user auto-accept | `IsInternalUser != null` -> Accepted | Already wired in `UploadStreamAsync` line 105-107 via `IsInternalActorAsync` helper | -- | -- | `[VERIFIED 2026-05-04]` -- no change. |
| `ResponsibleUserId` required | OLD throws on null | Should not throw | **Handle null `ResponsibleUserId`** -- skip the responsible-user email if null | I | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- `ResponsibleUserId` is nullable on the entity (`AppointmentDocument.cs`:60). Phase 14 publishes `AppointmentDocumentUploadedEto` regardless; the per-feature email handler (Phase 14b) skips the responsible-user leg when null. OLD-bug-fix: OLD's `.Value` access NRE'd. |
| Email templates shared with package docs | Same template enums | Same approach in NEW (`PatientDocumentUploaded` / `Accepted` / `Rejected`) | None (strict parity = shared templates) | -- | `[VERIFIED 2026-05-04]` -- Phase 4's `NotificationTemplateConsts.Codes.PatientDocument*` codes match OLD verbatim. |
| `AttachmentLink` HTML in DB | OLD stores `<a href='...'>Click here to upload</a>` | NEW does not store HTML; built in email handler | -- | -- | `[VERIFIED 2026-05-04]` -- NEW preserves the cleaner approach (link built at email-render time using Phase 18's `TemplateVariableSubstitutor`). |
| `DocumentAwsFilePath` retained but unused | Dead field in OLD | Not carried forward in NEW (uses `BlobName`) | -- | -- | `[VERIFIED 2026-05-04]` -- entity has `BlobName` only. |
| Email subject format with patient name + claim # | OLD format: `(...{Patient Name}...{Claim}...{WCAB ADJ}...)` | TO REPLICATE in subject builder | **Add subject builder helper** that includes patient name, claim, ADJ; localize via `IStringLocalizer` | I | `[DEFERRED to Phase 14b]` -- subject builder lands with the email handler. Phase 14 publishes the Eto with `AppointmentId` only; the handler joins the patient + injury data at render time. |
| File naming convention | `{confirmationNumber}_{filename}_{documentId}_{timestamp}.{ext}` | NEW uses tenant-prefixed GUID (`{tenantSegment}/{appointmentId}/{Guid:N}`) | -- | -- | `[VERIFIED 2026-05-04]` -- intentional NEW deviation (tenant-prefix is required for SaaS isolation; OLD's pattern is single-tenant). The `FileName` column on the entity preserves the human-readable original filename for download. |

## Internal dependencies surfaced

- Same as package documents: `PrimaryResponsibleUserId` set during clinic-staff approval.
- Email templates -- shared with package documents.
- `IBlobStorage` -- shared infra.

## Branding/theming touchpoints

- Email templates (shared with package documents).
- Upload page UI.

## Replication notes

### ABP wiring

- **Single `AppointmentDocument` entity** with `IsAdHoc bool` field.
- **Validation guard** in upload AppService:
  ```csharp
  if (!document.IsAdHoc) {
      // package-doc rules: status must be Approved/RescheduleRequested + before DueDate
  }
  // ad-hoc: no status gate
  ```
- **Internal-user fast-path:** check `CurrentUser.IsInRoleAsync` for any internal role -> auto-accept.
- **Email handler:** subscribes to `DocumentUploadedEto`, sends to uploader + responsible user.

### Things NOT to port

- `vInternalUser` SQL view -- use ABP roles instead.
- `DocumentAwsFilePath` field -- replaced by `IBlobStorage` blob URL.
- `AttachmentLink` HTML stored in DB -- generate at email time.
- Stored proc `spm.spAppointmentNewDocuments` -- use LINQ-to-EF.

### Verification (manual test plan)

1. External user uploads ad-hoc doc on Pending appointment -> succeeds (no status gate)
2. Internal user uploads ad-hoc doc -> auto-Accepted
3. External user uploads ad-hoc doc on Approved appointment after DueDate -> succeeds (ad-hoc has no DueDate gate; package docs would reject)
4. Staff rejects ad-hoc doc -> email with notes, re-upload allowed
5. Email subject contains patient name + claim # + WCAB ADJ
