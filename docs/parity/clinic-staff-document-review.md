---
feature: clinic-staff-document-review
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs (UpdateValidation, Update, SendDocumentEmail)
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentNewDocumentDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentJointDeclarationDomain.cs
old-docs:
  - socal-project-overview.md (lines 487-501)
audited: 2026-05-01
re-verified: 2026-05-04
status: in-progress
priority: 2
strict-parity: true
internal-user-role: ClinicStaff (also StaffSupervisor / ITAdmin)
depends-on:
  - clinic-staff-appointment-approval           # responsible user is set here
  - external-user-appointment-package-documents
  - external-user-appointment-ad-hoc-documents
  - external-user-appointment-joint-declaration
---

# Clinic Staff -- Document review (accept / reject)

## Purpose

After external user uploads documents (package docs, ad-hoc docs, JDF), the assigned Clinic Staff (the `PrimaryResponsibleUserId` user, or any Clinic Staff per permissions) reviews each document and accepts or rejects with notes. Accept = final; user cannot modify. Reject = uploader notified with notes; user can re-upload to that same document slot.

**Strict parity with OLD.** Same review flow for all 3 document types (package, ad-hoc, JDF) with table-specific entities.

## OLD behavior (binding)

### Per-document review

For each `AppointmentDocument`, `AppointmentNewDocument`, or `AppointmentJointDeclaration` row, staff can:

- **Accept:** set `DocumentStatusId = Accepted`. Uploader notified via `EmailTemplate.PatientDocument*Accepted` template. Document becomes immutable for external users.
- **Reject:** set `DocumentStatusId = Rejected` + `RejectionNotes` (required) + `RejectedById = staff user`. Uploader notified via `EmailTemplate.PatientDocument*Rejected` template with notes. User can re-upload (which clears `RejectionNotes` and sets status back to `Uploaded`).

### Email templates (shared across doc types)

- `EmailTemplate.PatientDocumentAccepted` -- "Your appointment document is Accepted"
- `EmailTemplate.PatientDocumentRejected` -- "Your appointment document is Rejected" -- includes `<b> Rejection Reason: </b> {notes}`
- `EmailTemplate.PatientDocumentUploaded` -- "Appointment document is uploaded by user" -- sent to responsible user

Email subject pattern (from `AppointmentNewDocumentDomain.SendDocumentEmail`):

`"Patient Appointment Portal - ({PatientName} - Claim: {claimNumber} - ADJ: {wcabAdj}) - Appointment document is {Status}."`

### Per-package status drift

When ANY package doc is rejected, the appointment's overall doc-completion status visible to staff drops back below 100%. Staff can see "X documents pending re-upload" + "Y still not uploaded". Used by reminder cadence.

### Critical OLD behaviors

- **Per-document accept/reject, not bulk.** Each doc reviewed individually. (Spec line 489: "The clinic staff can accept few documents and reject others.")
- **Rejection notes required** on reject; cleared on re-upload by user.
- **Accepted documents are immutable.** Update from external user blocked once `DocumentStatusId = Accepted`.
- **Internal user uploads bypass review** -- auto-Accepted (per booking audit's internal-user fast-path + per `AppointmentDocumentDomain.Update` line 159 setting `Accepted` for internal users).
- **Email subject includes patient identity + claim** for staff/uploader recognition.
- **Three review surfaces:** package docs (`AppointmentDocuments`), ad-hoc docs (`AppointmentNewDocuments`), JDF (`AppointmentJointDeclarations`). Same template logic applies; per-entity routes.
- **JDF review has a known OLD bug** (per JDF audit): `Update` sets `RejectedById` but not `DocumentStatusId`. Strict parity exception: FIX in NEW (recorded in JDF audit).

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDocumentDomain.cs` Update + SendDocumentEmail | Package doc review |
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentNewDocumentDomain.cs` Update + SendDocumentEmail | Ad-hoc doc review |
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentJointDeclarationDomain.cs` Update + SendDocumentEmail | JDF review (with bug) |
| `Models.Enums.DocumentStatuses.{Pending, Uploaded, Accepted, Rejected, Deleted}` | Status enum |
| `patientappointment-portal/.../appointment-{documents,new-documents,joint-declarations}/edit/...` | Review UI |

## NEW current state

- `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` exists.
- `Application.Contracts/AppointmentDocuments/RejectDocumentInput.cs` exists -- suggests reject-with-notes is partially implemented.
- TO VERIFY whether NEW has `AcceptAsync` + `RejectAsync` methods and whether they emit notification events.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Accept endpoint | OLD: implicit via Update | Existing `ApproveAsync(Guid)` line 172 | -- | -- | `[VERIFIED 2026-05-04]` -- already exists. Phase 14 adds Eto publish (`AppointmentDocumentAcceptedEto`). |
| Reject endpoint | OLD: implicit via Update with RejectionNotes | Existing `RejectAsync(Guid, RejectDocumentInput)` line 185 | -- | -- | `[VERIFIED 2026-05-04]` -- already exists. Phase 14 adds Eto publish (`AppointmentDocumentRejectedEto`). |
| Re-upload clears RejectionNotes + status -> Uploaded | OLD | NEW upload paths now reset state on re-upload | -- | -- | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- `UploadPackageDocumentAsync` clears `RejectionReason` + sets `Status = Uploaded` (or `Accepted` for internal users) per OLD `AppointmentDocumentDomain.cs`:166. |
| Accepted -> immutable for external users | OLD enforces in update validation | Missing | **Add status check** on uploader endpoint -- reject if currently Accepted | I | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- `DocumentUploadGate.EnsureNotImmutable(document, isInternalUser)` throws when external user attempts upload against Accepted row. |
| Internal user upload auto-Accepted | OLD | Already wired in `UploadStreamAsync` line 105-107 | -- | -- | `[VERIFIED 2026-05-04]` -- propagated to new `UploadPackageDocumentAsync` path. |
| Notification on accept/reject | OLD: `EmailTemplate.PatientDocument{Accepted,Rejected}` | Phase 18 declared the Etos; Phase 14 publishes them | -- | -- | `[IMPLEMENTED 2026-05-04 - tested unit-level]` (publish) / `[DEFERRED to Phase 14b]` (email handler) -- the per-feature handler that consumes the Etos + dispatches via `INotificationDispatcher` lands in Phase 14b. |
| Email subject includes patient + claim + ADJ | OLD pattern | TO REPLICATE in subject builder | **Subject builder helper** that pulls `Patient.FirstName/LastName + InjuryDetail.ClaimNumber + WcabAdj` | C | `[DEFERRED to Phase 14b]` -- subject builder lives with the per-feature email handler. Phase 14 publishes Etos with `AppointmentId` only; the handler joins the patient + injury data at render time. |
| Per-document review (not bulk) | OLD | Per-document `ApproveAsync(Guid)` / `RejectAsync(Guid, ...)` | -- | -- | `[VERIFIED 2026-05-04]` -- single-document signatures preserved. |
| JDF status flow bug fix | OLD bug | NEW already correct | -- | -- | `[VERIFIED 2026-05-04]` `[OLD-BUG-FIX]` -- `RejectAsync` sets both `Status = Rejected` AND `RejectedByUserId` (line 192-194). OLD's bug structurally non-reproducible in NEW. |
| Permissions | -- | -- | -- | -- | `[VERIFIED 2026-05-04]` -- `AppointmentDocuments.Approve` exists (`CaseEvaluationPermissions.cs`:115); granted to Clinic Staff / Staff Supervisor / IT Admin in `InternalUserRoleDataSeedContributor.cs`. |
| Responsibility scope | OLD: any Clinic Staff can review (no per-appointment scope) | -- | **Strict parity: don't restrict to PrimaryResponsibleUserId** | -- | `[VERIFIED 2026-05-04]` -- existing AppService gates on permission only. PrimaryResponsibleUserId used only for notification recipients, not for authz. |

## Internal dependencies surfaced

- Email templates -- IT Admin slice.

## Branding/theming touchpoints

- Email templates (subject + body) per status.
- Review UI (logo, primary color).

## Replication notes

### ABP wiring

- **`IAppointmentDocumentsAppService.AcceptAsync(Guid)` + `RejectAsync(Guid, RejectDocumentInput)`.** Updates `DocumentStatus` via state-machine method on entity. Publishes `DocumentAcceptedEto` / `DocumentRejectedEto` event.
- **Same pattern for `IAppointmentJointDeclarationsAppService` if separate** (or use `IsJointDeclaration` flag with shared service per JDF audit recommendation).
- **`DocumentReviewedEmailHandler`:** subscribes to both events, builds subject from patient + claim, sends to uploader.
- **`Appointment.SetDocumentStatus` method** on the entity to gate transitions: Pending -> Uploaded -> Accepted | Rejected; Rejected -> Uploaded (re-upload). Throws `InvalidStatusTransitionException` for others.

### Things NOT to port

- The OLD JDF bug.
- `vInternalUser` view -- use ABP roles.

### Verification (manual test plan)

1. User uploads package doc -> staff sees Uploaded status
2. Staff Accept -> status = Accepted, user email arrives
3. Staff Reject with notes -> status = Rejected, user email with notes
4. User re-uploads -> status = Uploaded, RejectionNotes cleared
5. Staff Accept on already-Accepted -> idempotent or rejected (TO DEFINE -- OLD likely just no-ops; strict parity)
6. External user tries to update Accepted doc -> rejected
7. Internal user uploads -> auto-Accepted, staff doesn't need to review
8. Same flows for ad-hoc docs and JDF
