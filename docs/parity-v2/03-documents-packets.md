# 03. Documents & packets -- OLD vs NEW behavioral parity

Scope: package documents (auto-queue on approve, per-document verification
code, anonymous upload, per-document accept/reject, immutability, re-upload),
ad-hoc documents, AME Joint Declaration Form (JDF), the document-type master
library, the document master catalog + package builder, packet PDF
generation (Patient / Doctor / Attorney-Claim-Examiner), the Joint Agreement
Letter download, and document/packet download endpoints.

Emails are deferred to area 04; the JDF auto-cancel + day-reminder jobs are
deferred to area 05; CSV/Excel exports to area 08. They are referenced here
only where the document workflow depends on them.

Source of truth = code. OLD: `P:\PatientPortalOld`. NEW:
`W:\patient-portal\replicate-old-app`.

---

## Coverage

OLD anchors read in full:

- `PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentDocumentsController.cs`
- `...\AppointmentRequest\AppointmentNewDocumentsController.cs`
- `...\AppointmentRequest\AppointmentJointDeclarationsController.cs`
- `...\Document\AppointmentDocumentsController.cs` (+ Search dir present)
- `...\Document\AppointmentJointDeclarationsController.cs` (+ Search)
- `...\Document\AppointmentDocumentTypesController.cs` (+ Search)
- `...\DocumentManagement\DocumentsController.cs`, `DocumentPackagesController.cs`, `PackageDetailsController.cs`
- `...\Core\DocumentDownloadController.cs`, `DocumentUploadController.cs`
- `PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs` (1232 lines, full)
- `...\AppointmentRequestModule\AppointmentNewDocumentDomain.cs`, `AppointmentJointDeclarationDomain.cs`
- `...\DocumentManagementModule\DocumentDomain.cs`, `DocumentPackageDomain.cs`, `PackageDetailDomain.cs`
- `...\DocumentModule\AppointmentDocumentTypeDomain.cs`
- `PatientAppointment.Infrastructure\Utilities\FileOperations.cs`, `AmazonBlobStorage.cs` (packet assembly + GetJointAgreementLetter + DownloadPackageDocument)
- Angular OLD: `components\document\*`, `document-management\*`, `appointment-request\appointment-{documents,new-documents,joint-declarations}\*`, `appointment-document-types\*` (dir inventory + JDF view component)

NEW anchors read in full:

- `Domain\AppointmentDocuments\` -- `AppointmentDocument.cs`, `AppointmentDocumentManager.cs`, `AppointmentPacket.cs`/`Manager`, `CoverPageGenerator.cs`, `PacketMergeService.cs`, `JointDeclarationCutoff.cs`, `Handlers\PacketGenerationOnApprovedHandler.cs`, `Jobs\GenerateAppointmentPacketJob.cs`, `Templates\{PacketTokenContext,PacketTokenResolver,DocxTemplateRenderer,EmbeddedTemplateResources}.cs`, `Pdf\GotenbergDocxToPdfConverter.cs`, `PacketAttachmentProvider.cs`
- `Application\AppointmentDocuments\` -- `AppointmentDocumentsAppService.cs`, `AppointmentPacketsAppService.cs`, `DocumentUploadGate.cs`
- `Application\Documents\DocumentsAppService.cs`, `Application\PackageDetails\PackageDetailsAppService.cs`
- `Application\Notifications\Handlers\PackageDocumentQueueHandler.cs`
- `Domain\Notifications\Jobs\JointDeclarationAutoCancelJob.cs`
- `Domain.Shared\AppointmentDocuments\{DocumentStatus,PacketKind}.cs`
- `HttpApi\Controllers\AppointmentDocuments\{AppointmentDocumentController,AppointmentPacketController,PublicDocumentUploadController}.cs`, `Documents\DocumentsController.cs`, `PackageDetails\PackageDetailsController.cs`
- Angular NEW: `appointment-documents\*`, `appointment-packet\*` (file inventory)

---

## Summary counts

| Class | Count |
|---|---|
| Missing behavior | 3 |
| Partial behavior | 2 |
| Intent deviation | 1 |
| Equivalent (different implementation) | 9 |
| OLD-bug (do not port) | 5 |

---

## Behavioral gaps

### G-03-01 -- Document-type master library (`AppointmentDocumentType`) not ported

- **Class:** Missing behavior
- **OLD:** `AppointmentDocumentTypeDomain.cs` (full CRUD); `AppointmentDocumentTypesController.cs`; `AppointmentDocumentTypesSearchController.cs`; Angular `components\document\appointment-document-types\{add,edit,list}` + `appointment-request\...\appointment-document-types`.
- **NEW:** No `AppointmentDocumentType` entity, AppService, controller, or permission. The string `AppointmentDocumentType` appears only in a doc-comment in `Domain\AppointmentDocuments\AppointmentDocument.cs:16` listing it as a deferred cut ("free-text DocumentName at MVP").
- **What it is:** A tenant-level master list of document-type names (e.g. "Medical Records", "Subpoena", "AME Report"). OLD lets IT/clinic staff CRUD these and the ad-hoc / package document records reference `AppointmentDocumentTypeId` (with `OtherDocumentTypeName` free-text fallback -- see `AppointmentNewDocument` fields at `AppointmentDocumentDomain.cs:545-546`).
- **Why it existed:** Gives a controlled vocabulary for classifying uploaded documents so staff can filter/report by type instead of relying on free text.
- **What it does + user impact:** Without it, NEW classifies every document by free-text `DocumentName` only (`AppointmentDocument.DocumentName`). Staff lose the typed dropdown when uploading ad-hoc docs and lose type-based filtering. The package-queue path copies the master `Document.Name` into `DocumentName` so package docs are still labelled, but ad-hoc uploads are unconstrained free text.
- **Plain-English:** OLD had an admin-managed list of "kinds of document" you pick from a dropdown. NEW just lets you type a name.
- **Keep in NEW?** Decide later. Low-risk to defer through Phase 1 (free text works); revisit if staff want typed filtering/reporting. Needs its own parity audit doc before implementation.

### G-03-02 -- Joint Agreement Letter download not ported

- **Class:** Missing behavior
- **OLD:** `DocumentDownloadController.Get()` (route `api/DocumentDownload/Download`) calls `FileOperations.GetJointAgreementLetter()` (`FileOperations.cs:92`), which pulls a pre-authored DOCX from the `aws.jointagreementletter` blueprint folder (`AmazonBlobStorage.cs:178-223`) and streams it back as a download.
- **NEW:** No equivalent endpoint or AppService. NEW's `JointAgreement*` strings refer only to the JDF feature (`JointDeclarationsContainer`, JDF email subjects, JDF rejected/accepted ETOs) -- a different concept (the attorney UPLOADS a JDF; this OLD letter is a blank template the user DOWNLOADS).
- **Why it existed:** Gave attorneys a blank "Joint Agreement" letter template to fill out and re-submit (the precursor to the uploaded JDF).
- **What it does + user impact:** Users could click a link to download the standard letter. NEW users have no in-app way to obtain the blank letter; they would need it supplied out-of-band.
- **Plain-English:** OLD had a "download the blank joint-agreement letter" button. NEW does not.
- **Keep in NEW?** Decide later. Confirm with Adrian whether OLD's download button was still surfaced in the UI (only the `DocumentDownloadController` and a `reportFilePath` setting reference it; no clear Angular caller found). If the JDF upload flow superseded it, this is intentionally dead and should NOT be ported.

### G-03-03 -- Ad-hoc document has no dedicated upload surface / type metadata; unified into generic path

- **Class:** Partial behavior
- **OLD:** `AppointmentNewDocument` is a distinct table with its own controller (`AppointmentNewDocumentsController`), domain, and Angular `appointment-new-documents\{add,edit,list,detail}`. It carries `AppointmentDocumentTypeId`, `OtherDocumentTypeName`, `ResponsibleUserId`, and a dedicated `Add` path that sets status Accepted for internal users / Pending for external (`AppointmentNewDocumentDomain.cs:85-92`).
- **NEW:** Unified into the single `AppointmentDocument` entity via the `IsAdHoc` flag (per the project mission's allowed unification). The generic `UploadStreamAsync` is the ad-hoc path (`AppointmentDocumentsAppService.cs:206-211`), sets `IsAdHoc=true`, internal -> Accepted / external -> Uploaded, sets `ResponsibleUserId` for internal.
- **What it is:** General "extra document" attached to an appointment outside the package list.
- **What it does + user impact:** Behaviorally equivalent EXCEPT (a) no document-type classification on ad-hoc rows (depends on G-03-01) and (b) OLD's ad-hoc `Add` has NO due-date/approved gate (`AppointmentNewDocumentDomain.UpdateValidation` is commented out, `:114-126`), and NEW's ad-hoc `UploadStreamAsync` likewise applies no upload gate -- so this part matches. The only true gap is the missing type metadata.
- **Plain-English:** The "attach an extra document" feature works; it just can't tag the document with a type yet.
- **Keep in NEW?** Yes (unification is intended). The residual gap is the type metadata = G-03-01.

### G-03-04 -- JDF rejection does not cascade into a reschedule request

- **Class:** Partial behavior
- **OLD:** The spec referenced in the prompt ("per-doc accept/reject, cascade into reschedule") is NOT implemented in OLD code. The OLD JDF reject path (`appointment-joint-declaration-view.component.ts:65-83`) only PATCHes `documentStatusId=Rejected` + rejection notes; it does NOT touch appointment status. The OLD internal-user JDF `Update` (`AppointmentJointDeclarationDomain.cs:171-178`) only sets `RejectedById` and never changes status (see OLD-bug G-03-B2). No reschedule cascade exists in OLD.
- **NEW:** JDF reject = generic `RejectAsync` (`AppointmentDocumentsAppService.cs:565-596`), sets `Rejected` + reason, publishes `AppointmentDocumentRejectedEto`. No appointment-status cascade. The only JDF-driven appointment-status change in NEW is the auto-cancel job (`JointDeclarationAutoCancelJob.cs`) when NO JDF is uploaded by the cutoff -- which transitions to `CancelledNoBill`, not a reschedule.
- **What it is:** Whether rejecting an attorney's JDF reopens the appointment for rescheduling.
- **What it does + user impact:** Neither app cascades a JDF rejection into a reschedule. NEW matches OLD's actual (non-cascading) behavior. The "cascade into reschedule" in the audit brief appears to be a spec expectation not realized in OLD code.
- **Plain-English:** Rejecting a joint-declaration form just marks it rejected in both apps; it does not automatically reschedule the visit.
- **Keep in NEW?** Decide later. Flag to Adrian: confirm the intended business rule. If a reschedule cascade is genuinely wanted, it is NEW work (no OLD source to port).

### G-03-05 -- Per-document email link points at one document; OLD emailed whole-packet links + remaining-count nudges

- **Class:** Intent deviation
- **OLD:** Document status-change emails embedded a generic "upload your documents" link to `/appointment-documents/?appointmentid=...&appointmenttype=...` and appended a "N documents are still not uploaded" remaining-count nudge (`AppointmentJointDeclarationDomain.cs:238-244`, and commented-out blocks throughout `AppointmentDocumentDomain.cs:261-285`). The link was appointment-scoped, not document-scoped.
- **NEW:** Per-document `VerificationCode` GUID drives a document-scoped anonymous upload link (`/api/public/appointment-documents/{id}/upload-by-code/{code}`, `PublicDocumentUploadController.cs`). Each queued package document gets its own code (`AppointmentDocument.CreateQueued`, `AppointmentDocumentManager.CreateQueuedAsync`). The remaining-document-count nudge is an email-layer concern (area 04).
- **What it is:** How the patient is directed to upload pending documents.
- **What it does + user impact:** NEW is finer-grained and more secure (one code per document, anonymous upload of THAT document only). This is the OLD's `AddAppointmentDocument`/`VerificationCode` design (`AppointmentDocumentDomain.cs:1102-1123`) FULLY WIRED -- in OLD it was commented out (see OLD-bug G-03-B1), so OLD never actually issued per-document codes at runtime. NEW realizes the intended-but-dead OLD design. Outcome differs: NEW patient gets a per-document link; OLD patient got one appointment-wide link.
- **Plain-English:** OLD sent one "upload your documents here" link for the whole appointment. NEW sends a separate secure link per document. Both let the patient upload without logging in.
- **Keep in NEW?** Yes -- this is the more correct realization of OLD's own (disabled) design and is more secure. Flag the UX change for Adrian's awareness.

---

## Equivalent -- different implementation

These are NOT gaps. Outcome matches OLD; only the mechanism differs (expected per project mission).

| # | Behavior | OLD | NEW | Note |
|---|---|---|---|---|
| E1 | File storage | local `wwwroot\Documents\submittedDocuments` + AWS S3 (commented out) | `IBlobContainer<AppointmentDocumentsContainer>` (DB BLOB at MVP -> MinIO/Azure) | Allowed swap. |
| E2 | Two doc tables | `AppointmentDocument` (package) + `AppointmentNewDocument` (ad-hoc) | one `AppointmentDocument` entity + `IsAdHoc` / `IsJointDeclaration` flags | Allowed unification (explicitly sanctioned). |
| E3 | Auto-queue package docs on approve | `AddAppointmentDocumentsAndSendDocumentToEmail` fired on approval | `PackageDocumentQueueHandler` on `AppointmentApprovedEto` -> `CreateQueuedAsync` (Pending rows, one per active `DocumentPackage` for the type) | Same outcome: pending rows materialized at approval. Updated by #271 (2026-05-29): package docs queue at submission. |
| E4 | Per-document verification code | `AppointmentDocument.VerificationCode` Guid + `GetValidation` (`AppointmentDocumentDomain.cs:64-75`) | `VerificationCode` + `DocumentUploadGate.EnsureVerificationCodeMatches` | OLD error string "Un unauthorized user" preserved verbatim (localized). |
| E5 | Anonymous upload via code | `DocumentUploadController.GetFiles/UploadFile` keyed by `verificationCode` | `PublicDocumentUploadController` + `UploadByVerificationCodeAsync` `[AllowAnonymous]`, rate-limited | Same: unauthenticated upload of one document by code. |
| E6 | Per-document accept/reject + immutability + re-upload-clears-rejection | internal user sets Accepted; reject sets Rejected + notes; re-upload clears `RejectionNotes` (`AppointmentDocumentDomain.cs:159-166`) | `ApproveAsync`/`RejectAsync`; `EnsureNotImmutable` blocks external write on Accepted; `OverwriteUploadedFileAsync` nulls `RejectionReason`/`RejectedByUserId` (`:457-458`) | Status enum copied verbatim (Uploaded=1/Accepted=2/Rejected=3/Pending=4); Deleted=5 -> ABP soft-delete. |
| E7 | Upload gate (approved + before DueDate; AME-only JDF; booking-attorney JDF) | `UpdateValidation` (`AppointmentDocumentDomain.cs:90-107`), JDF `UpdateValidation` (`AppointmentJointDeclarationDomain.cs:102-125`) | `DocumentUploadGate.{EnsureAppointmentApprovedAndNotPastDueDate,EnsureAme,EnsureCreatorIsAttorney}` | OLD error strings preserved; OLD's PQME-rejection on JDF reframed as AME-allow (same net effect). Resolved by #271 (2026-05-29): upload allowed at Pending (request-time), not just Approved. |
| E8 | Packet generation engine | OpenXml token-replace of DOCX blueprints (`ReplaceText`, `InsertAPicture`) downloaded from blueprint folder; emailed as `.docx` | `GenerateAppointmentPacketJob` -> `PacketTokenResolver` + `DocxTemplateRenderer` (OpenXml) -> `GotenbergDocxToPdfConverter` -> PDF blob; 3 templates embedded byte-identical from OLD blueprints | DOCX deliverable -> PDF deliverable is the intended mission change. Patient/Doctor/AttorneyClaimExaminer kinds + per-type gating (PQME/AME) match OLD's `:643/:689/:740/:801` branches. Signature stamped at OLD's exact 880000x880000 EMU; OLD silent-skip on null signature preserved. Refined by #270 (2026-05-29): per-role PacketVisibility allow-list + all-type AttyCE. |
| E9 | Document master catalog + package builder (master library + package details) | `DocumentDomain` (blueprint CRUD), `PackageDetailDomain` (one active package per AppointmentType, `:48-53`), `DocumentPackageDomain` (M:N link) | `DocumentsAppService` (`MasterDocumentsContainer` blob), `PackageDetailsAppService` (`EnsureNoActiveDuplicateAsync` = one active per type), `DocumentPackage` link + `LinkDocuments`/`Unlink` | Same business rules; NEW additionally fixes OLD's orphan-link bug (see G-03-B3) and OLD's empty `DocumentDomain.DeleteValidation` by adding an in-use reference check (`DocumentInUse`). Treated as sanctioned OLD-bug-fix per audit lifecycle. |

---

## OLD bugs (do not port)

### G-03-B1 -- Per-recipient document queueing is commented out in OLD
- `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail` calls `AddAppointmentDocument(...)` (which would insert per-recipient `AppointmentDocument` rows with `VerificationCode` + status Pending) but EVERY call site is commented out (`:487, :681, :729, :775, :789, :835, :849`). At runtime OLD emailed packet attachments but never created the per-document upload rows or codes. NEW correctly implements the intended (live) design. The hardcoded `DocumentPackageId = 77` in that dead method (`:1109`) is an obvious placeholder bug. Do not port the dead code; NEW's wired version is correct.

### G-03-B2 -- OLD internal-user JDF "reject" never sets Rejected status
- `AppointmentJointDeclarationDomain.Update` internal-user branch (`:171-178`) only sets `RejectedById = UserClaim.UserId` and commits -- it never sets `DocumentStatusId = Rejected`. So a staff "reject" left the JDF in its prior status with only a `RejectedById` stamp. Additional bug: the branch's guard `vInternalUser.RoleId == UserClaim.UserId` (`:129`) compares a RoleId to a UserId, so the internal/external classification is wrong. NEW's `RejectAsync` correctly sets `Rejected` + reason + `RejectedByUserId`. Do not port.

### G-03-B3 -- OLD `PackageDetailDomain.Delete` orphans all-but-first link
- `PackageDetailDomain.Delete` (`:98-112`) soft-deletes only the FIRST `DocumentPackage` (`FirstOrDefault`) when removing a package, orphaning the rest as active links pointing at a deleted package. NEW's `PackageDetailsAppService.DeleteAsync` deletes ALL link rows. Sanctioned fix; do not port the orphan behavior.

### G-03-B4 -- OLD packet generation hardcodes a single responsible-user signature for ALL recipients + signature is keyed by wrong path setting
- In `AddAppointmentDocumentsAndSendDocumentToEmail`, the signature image inserted into the doctor/attorney/claim-examiner packets is always `appointment.PrimaryResponsibleUserId`'s signature (`:567-572, :655-660, :702-707, :753-758, :814-819`) regardless of recipient. This is arguably intentional (the clinic's responsible staffer signs all packets), so NEW replicates it (`PacketTokenResolver.PopulateAppointmentAsync:197-205` uses `PrimaryResponsibleUserId`). Noting it as ambiguous-but-replicated; no PARITY-FLAG needed since NEW matches.

### G-03-B5 -- OLD `AppointmentNewDocument.Add` swallows all exceptions and returns success
- `AppointmentNewDocumentDomain.Add` (`:64-108`) wraps the insert in try/catch that on ANY exception returns the un-persisted object as if successful (`:104-107`), and the controller `Post` does the same (`AppointmentNewDocumentsController.cs:54-58`). A failed upload silently reports success. NEW propagates failures (UoW rollback + compensating blob delete, `SaveBlobWithRollbackAsync`). Do not port the swallow.

---

## Open questions

1. **Joint Agreement Letter (G-03-02):** Was the OLD `DocumentDownloadController.Download` button still surfaced in the live UI, or was it superseded by the JDF upload flow? No clear Angular caller found. If dead, do not port.
2. **JDF reject -> reschedule (G-03-04):** The audit brief expects a cascade; OLD code has none. Confirm the intended business rule before building NEW-only logic.
3. **Document-type master (G-03-01):** Defer through Phase 1, or is typed classification/filtering a near-term staff need? Needs its own parity audit doc before implementation.
4. **Doctor packet not emailed:** OLD generates + stores the Doctor packet but never emails it (`AppointmentDocumentDomain.cs:561-634` has no SendMail). NEW preserves this asymmetry (`GenerateAppointmentPacketJob` doctor kind has no email subscriber). Confirm this is still desired (delivered to the doctor out-of-band).
5. **Per-document vs appointment-wide upload link (G-03-05):** Confirm Adrian accepts NEW's per-document secure-link UX in place of OLD's single appointment-wide link.
