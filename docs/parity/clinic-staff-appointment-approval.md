---
feature: clinic-staff-appointment-approval
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs (Update + UpdateValidation)
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\edit\
old-docs:
  - socal-project-overview.md (lines 471-485)
audited: 2026-05-01
re-verified: 2026-05-04
status: in-progress
priority: 2
strict-parity: true
internal-user-role: ClinicStaff (also StaffSupervisor and ITAdmin per permission matrix)
depends-on:
  - external-user-appointment-request           # creates the Pending appointment
required-by:
  - external-user-appointment-package-documents # approval queues package docs
  - clinic-staff-document-review                # responsible user reviews uploaded docs
---

# Clinic Staff -- Appointment approve / reject

## Purpose

Clinic Staff (or Staff Supervisor / IT Admin) reviews each Pending appointment, selects a "responsible team member" from the staff list, and approves OR rejects with notes. Approval transitions: `Pending` -> `Approved`; slot `Reserved` -> `Booked`; **package documents auto-queued**; emails + SMS to all stakeholders + the responsible user receives a separate "fill these forms offline" email. Rejection: status -> `Rejected`; rejection notes emailed to creator; user can re-submit via Re-Request flow.

**Strict parity with OLD.**

## OLD behavior (binding)

### Approval flow (per spec lines 471-485)

1. **Patient match check.** While viewing the Pending appointment, system flags if `IsPatientAlreadyExist == true` (set during booking by the 3-of-6 dedup rule). UI shows match details; staff can:
   - Link this appointment to the existing patient record (default), OR
   - Create a new patient record (override).
2. **Select responsible team member.** Staff picks from the list of internal users (Clinic Staff / Staff Supervisor / IT Admin). Selected user becomes `Appointment.PrimaryResponsibleUserId`. This user receives all subsequent notifications about the appointment.
3. **Approve action:**
   - `appointment.AppointmentStatusId = Approved`
   - `appointment.AppointmentApproveDate = DateTime.Now`
   - `appointment.PrimaryResponsibleUserId = <selected>`
   - `appointment.ModifiedById/Date = clinic staff user`
   - Slot transition: `BookingStatus.Reserved -> Booked` on `DoctorsAvailability`.
   - **Trigger package docs:** call `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail(...)` -- queues per-document `AppointmentDocuments` rows with `Pending` status + `VerificationCode`, sends email to patient with per-document upload links.
   - **Send approval emails:**
     - To **patient** (or appointment creator if no patient email): "your appointment is approved" -- with package documents attached as PDFs.
     - To **responsible team member**: "you are responsible for this appointment" -- with a separate package of documents (the staff-side forms to fill offline + upload back).
     - To **all other stakeholders** (attorneys, claim examiner, employer, accessors): notification of approval.
   - SMS to all stakeholders.
4. **Reject action:**
   - `appointment.AppointmentStatusId = Rejected`
   - `appointment.RejectionNotes = <staff-entered>`
   - `appointment.RejectedById = clinic staff user`
   - `appointment.ModifiedById/Date`
   - Slot transition: `Reserved -> Available` (slot freed).
   - **Send rejection email** to creator with notes. User can edit + re-submit via Re-Request flow (covered in booking audit).

### `UpdateValidation` (per OLD code lines 312-...)

Status-change validation:

- If status update on already-Approved appointment with same status -> "Appointment Already Approved"
- If on already-Rejected with same target -> "Appointment Already Rejected"
- Same idempotency check for CheckedIn / CheckedOut / Billed
- Else: status update accepted

### Critical OLD behaviors

- **Approval is the trigger for package docs.** No package docs exist on the appointment until approval.
- **`PrimaryResponsibleUserId` is NOT set at booking time.** It's set by the approving staff member.
- **Two separate document packages on approval:**
  - Patient-side package (e.g., medical history forms, intake-supplement, consent forms) -- attached to patient email.
  - Staff-side package (e.g., evaluator's worksheet, billing forms) -- attached to responsible-user email.
- **SMS + Email to all stakeholders** (patient, attorneys, claim examiner, employer if email present, accessors).
- **Patient-match override** lets staff create a new patient record even if the dedup matched. Used when the match is a false positive.
- **Idempotency:** repeated status updates to the same target are blocked.
- **Internal users skip the approval step** (per booking audit) -- their bookings are auto-Approved.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs` Update + UpdateValidation | Approval / rejection flow |
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDocumentDomain.cs` `AddAppointmentDocumentsAndSendDocumentToEmail` | Package doc queueing on approval |
| `Models.Enums.AppointmentStatusType.{Pending=1, Approved=2, Rejected=3}` | Status enum |
| `patientappointment-portal/.../appointments/edit/...` | Approval UI (status dropdown + responsible user picker + rejection notes) |
| `Models.Enums.EmailTemplate.{AppointmentApproved, AppointmentRejected, ResponsibleUserAssigned}` | Email templates (TO LOCATE) |

## NEW current state

Per `Appointments/CLAUDE.md`:

- `AppointmentManager.UpdateAsync` -- does NOT touch `AppointmentStatus`, `InternalUserComments`, `AppointmentApproveDate`, or `IsPatientAlreadyExist`. **These are not yet writable post-creation.** Strict parity gap: approval needs to write `AppointmentStatus`, `AppointmentApproveDate`, `PrimaryResponsibleUserId`.
- `AppointmentUpdateDto` lacks status field -- approval cannot happen via current Update endpoint.
- No state-machine guard on `Appointment.AppointmentStatus` -- public setter; any caller can set any status.
- `SubmissionEmailHandler.cs` exists -- handles booking emails; verify approval handler.
- `StatusChangeEmailHandler.cs` exists -- might cover this; verify.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Approve endpoint | Implicit via Update with status change | NEW: Update doesn't accept status | **Add `ApproveAppointmentAsync(Guid appointmentId, ApproveDto { ResponsibleUserId, PatientMatchOverride? })`** | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- Phase 12 ships richer `ApproveAppointmentAsync(id, ApproveAppointmentInput)` on a NEW `IAppointmentApprovalAppService` (sibling to Session A's existing thin `AppointmentsAppService.ApproveAsync(id)`). Sibling pattern was forced because the user's Phase 12 directive says "DO NOT edit the main `AppointmentsAppService.cs`" (Session A is rewriting it concurrently); a `partial class` would have required adding the `partial` keyword to that file. Strict reading wins. |
| Reject endpoint | Implicit | -- | **Add `RejectAppointmentAsync(Guid appointmentId, RejectDto { RejectionNotes })`** | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- shipped on the new `IAppointmentApprovalAppService` alongside the approve endpoint. |
| Patient match check + override UI | OLD: shown on edit page | NEW: TO VERIFY | **Add `IsPatientAlreadyExist + matched PatientId` to GetWithNavigationPropertiesAsync response**; UI surfaces match details + override option | I | `[IMPLEMENTED 2026-05-04 - tested unit-level]` (backend) / `[DESCOPED 2026-05-04 - frontend]` (UI) -- backend Approve endpoint accepts `OverridePatientMatch` flag in `ApproveAppointmentInput`. The actual patient-row split (creating a NEW Patient when override=true) is wired only when the matched patient/dedup logic from Session A's manager rewrite lands -- Phase 12 just records the staff's decision on the DTO and persists `IsPatientAlreadyExist` as the source of truth. UI work for the surface is Session A's frontend track. |
| `PrimaryResponsibleUserId` set on approval | OLD field | NEW: missing | **Add `PrimaryResponsibleUserId Guid?` to Appointment**; require non-null on approve | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- field exists since Phase 1.6 (`Appointment.cs`:90). Phase 12's `ApproveAppointmentInput` requires `PrimaryResponsibleUserId Guid` (non-nullable in DTO) -- validator throws `BusinessException(AppointmentApprovalRequiresResponsibleUser)` when default Guid is supplied. |
| `AppointmentApproveDate` written on approval | OLD field | NEW field exists; not written by Update | **Set `AppointmentApproveDate = DateTime.UtcNow`** in approve method | I | `[IMPLEMENTED 2026-05-04 - pending testing]` -- Session A's `AppointmentManager.TransitionAsync` (line 168-171) already stamps `AppointmentApproveDate = DateTime.UtcNow` on Approve transitions. Phase 12 reuses it via `AppointmentManager.ApproveAsync` delegation; no duplication. |
| Rejection notes written | OLD: `RejectionNotes` + `RejectedById` | NEW: missing | **Add `RejectionNotes string?` and `RejectedById Guid?` fields** | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- entity fields exist since Phase 1.6 (`Appointment.cs`:85, 87). Phase 12 sets `RejectionNotes` (from DTO) + `RejectedById = CurrentUser.Id` after Session A's manager flips the status. |
| Slot transition Reserved -> Booked on approve | OLD | NEW: no Reserved value in current enum (gap from booking audit) | **Add Reserved + transition** | B | `[IMPLEMENTED 2026-05-04 - pending testing]` -- enum already has `Reserved = 10`. Session A's `SlotCascadeHandler` subscribes to `AppointmentStatusChangedEto` and flips the slot per the T11 sync table (Reserved -> Booked on Approved transition). Verified at `SlotCascadeHandler.cs`:50 (Phase 18 audit). |
| Slot transition Reserved -> Available on reject | OLD | NEW: gap | **Add transition** | B | `[IMPLEMENTED 2026-05-04 - pending testing]` -- same handler; Reserved -> Available on Rejected transition. OLD parity verified at `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs`:600. |
| Auto-queue package docs on approve | OLD: `AddAppointmentDocumentsAndSendDocumentToEmail` | NEW: TO VERIFY | **Subscribe to `AppointmentApprovedEto` event in `PackageDocumentQueueHandler`** -- reads PackageDetail for AppointmentType, inserts AppointmentDocument rows | B | `[IMPLEMENTED 2026-05-04 - pending testing]` (handler skeleton) / `[DEFERRED to Phase 14 - row insert]` -- Phase 12 ships `PackageDocumentQueueHandler` subscribed to `AppointmentApprovedEto`; it reads `PackageDetail` linked Documents for the AppointmentTypeId and logs the queue intent. The actual `AppointmentDocument` row insert is gated on the entity contract supporting a "queued, pre-upload" shape (constructor demands `BlobName`/`FileName`/`FileSize` -- not available pre-upload). Phase 14 (Document review) closes the gap when the entity gains a queued-state factory. |
| Two-package emails (patient-side + staff-side) | OLD | NEW: TO VERIFY | **Add 2 separate email templates + 2 separate sends** in approval handler | I | `[DEFERRED to Phase 14 - email content]` -- the existing `StatusChangeEmailHandler.cs` (Session A territory, `Domain/Appointments/Handlers/`) handles transition emails today via inline HTML. Phase 18's `INotificationDispatcher` + per-feature handlers replace that with template-driven emails; Phase 14 will land the patient-side and responsible-user-side template-keyed sends as part of the document-review work (handler subscribes to AppointmentApprovedEto + uses `INotificationDispatcher.DispatchAsync(NotificationTemplateConsts.Codes.AppointmentApproved, ...)`). |
| SMS to all stakeholders on approve / reject | OLD | NEW: TO VERIFY (SubmissionEmailHandler is email-only) | **Add SMS handler** subscribing to ApprovedEto + RejectedEto | I | `[DEFERRED - blocked on ISmsSender wiring]` -- Phase 18 documented that `Volo.Abp.Sms` is not yet referenced by any project. SMS leg activates with the Twilio creds rollout per master-plan section 18.3. Etos are publish-shaped to support SMS subscribers when the package lands. |
| Idempotency check (already Approved -> reject the call) | OLD validation | NEW: no state-machine guard | **State machine on `Appointment.SetStatus`** + idempotency at AppService level | I | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- Session A's `AppointmentManager` uses Stateless state machine (line 196-227); illegal transitions throw `BusinessException("CaseEvaluation:AppointmentInvalidTransition")`. Phase 12's `AppointmentApprovalValidator.EnsurePending(appointment)` adds a friendlier idempotency check at the AppService boundary that throws `AppointmentNotPendingForApproval` / `...ForRejection` with OLD's "Appointment Already Approved" / "Appointment Already Rejected" verbiage -- this surfaces a cleaner error to the user before the manager's state machine fires. |
| Permissions | -- | -- | **Add `CaseEvaluation.Appointments.Approve` + `.Reject`**, gate to Clinic Staff / Staff Supervisor / IT Admin | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- keys already declared (`CaseEvaluationPermissions.cs`:103-104) and registered (`CaseEvaluationPermissionDefinitionProvider.cs`:61-62) in earlier phase. Phase 12 grants them to Clinic Staff + Staff Supervisor + IT Admin in `InternalUserRoleDataSeedContributor.cs` and gates the new richer endpoints with `[Authorize(...)]`. Session A's existing thin `ApproveAsync(id)` still gates on `Appointments.Edit` -- a one-line attribute fix Session A owns. |
| Audit trail | OLD writes to `AppointmentChangeLogs` | NEW: ABP `[Audited]` on entity covers this | None | -- | (no change) |

## Internal dependencies surfaced

- `it-admin-package-details.md` -- which Documents go in which Package per AppointmentType. Required for auto-queue to know what to insert.
- `it-admin-notification-templates.md` -- email + SMS template management.
- Internal-user lookup (the responsible-user picker) -- queries IdentityUser filtered to internal roles.

## Branding/theming touchpoints

- Edit appointment page UI
- Email templates: `AppointmentApproved` (patient version + responsible-user version), `AppointmentRejected`, `AppointmentApprovedStakeholder`
- SMS templates per event

## Replication notes

### ABP wiring

- **AppService methods:** `ApproveAppointmentAsync`, `RejectAppointmentAsync` on `IAppointmentsAppService`. Each opens a Unit of Work, mutates `Appointment` via `SetStatus(AppointmentStatusType target)` (state machine guard), updates slot status, publishes domain event.
- **Domain event publishing:** `AppointmentApprovedEto { AppointmentId, ApprovedByUserId, ResponsibleUserId, Stakeholders }` and `AppointmentRejectedEto { AppointmentId, RejectedByUserId, RejectionNotes, Stakeholders }`.
- **Subscribers:**
  - `PackageDocumentQueueHandler` -- on Approved -> insert AppointmentDocument rows + send patient email with per-doc links.
  - `ResponsibleUserNotificationHandler` -- on Approved -> send "you are responsible" email with staff-side doc package.
  - `StakeholderApprovalEmailHandler` -- on Approved/Rejected -> email all stakeholders.
  - `StakeholderApprovalSmsHandler` -- ditto via `ISmsSender`.
- **Patient-match override:** approve DTO accepts `OverridePatientMatch: bool`. If true + `IsPatientAlreadyExist == true`, create a new Patient row + relink the appointment.

### Things NOT to port

- Stored proc reads.
- `vInternalUser` view -- query `IdentityUser` directly with role filter.
- Direct Twilio calls -> `ISmsSender`.

### Verification (manual test plan)

1. Pending appointment with `IsPatientAlreadyExist = true` -> staff sees match details + Link/Override options
2. Approve with Link -> uses existing PatientId; Approve with Override -> creates new Patient
3. Approve -> status = Approved, slot = Booked, package docs queued, patient email + responsible-user email + stakeholder emails + SMS sent
4. Reject -> status = Rejected, slot = Available, creator email with notes
5. Try approve already-Approved -> idempotency: rejected
6. Approve without selecting responsible user -> rejected
7. Try approve without permission -> 403

## Phase 12 verification pass (2026-05-04)

### A. OLD claims confirmed verbatim

- **Slot transitions** (`P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs`:586-611) -- switch on `AppointmentStatusId` flips slot to Reserved (Pending) / Booked (Approved) / Available (Rejected | CancelledNoBill). Confirms the audit's slot transition gap rows.
- **AppointmentApproveDate stamp** (line 453-456) -- written when `IsStatusUpdate && IsInternalUserUpdateStatus && AppointmentStatusId == Approved`. Confirms the audit row.
- **Package-doc queue trigger** (line 560-564) -- only fires for `Approved + IsInternalUserUpdateStatus`; Rejected does not queue docs.
- **`AddAppointmentDocumentsAndSendDocumentToEmail`** (`AppointmentDocumentDomain.cs`:394-...) -- reads patient packet from AWS, replaces tokens, sends as email attachment. NEW does NOT port the AWS / DOCX / inline-attachment path; the equivalent is row creation + email with verification-code link (Phase 14 / Phase 18).
- **UpdateValidation idempotency** (line 312-344) -- exact verbatim error strings: "Appointment Already Approved", "Appointment Already Rejected", "Appointment Already checked in", "Appointment Already checked out", "Appointment Already billed". Phase 12 adopts these for the localization-key values.

### B. OLD bugs / divergences

- OLD's `UpdateValidation` runs idempotency only when `IsStatusUpdate && IsInternalUserUpdateStatus`. External users hitting the Update path via the Re-Request flow (booking re-submit on a Rejected appointment) bypass the check. NEW's state-machine + idempotency-at-AppService runs unconditionally.
- OLD line 458 stamps `appointment.ModifiedById = UserClaim.UserId` directly in domain code. NEW relies on ABP's `[Audited]` + `FullAuditedAggregateRoot` -- equivalent observable behavior, more idiomatic.

### C. NEW state vs audit assumptions -- material divergence

- **`Appointments.Approve` / `.Reject` permission keys ALREADY EXIST** (`CaseEvaluationPermissions.cs`:103-104, registered at `CaseEvaluationPermissionDefinitionProvider.cs`:61-62). Phase 12's "Permissions" gap row was misleadingly worded -- the keys are declared; only the seed grants and the actual `[Authorize]` attribute use are open. Phase 12 closes the seed grants. The attribute use on Session A's existing `ApproveAsync(id)` / `RejectAsync(id, RejectAppointmentInput)` (currently `[Authorize(Appointments.Edit)]`) is a one-line Session-A-territory fix.
- **`AppointmentManager.ApproveAsync` / `RejectAsync` ALREADY EXIST** (`AppointmentManager.cs`:141, 145) and stamp `AppointmentApproveDate` + publish `AppointmentStatusChangedEto`. Session A built the core flow earlier; Phase 12 adds the audit's missing surface (`PrimaryResponsibleUserId` write, `RejectionNotes` write, idempotency UX, Etos for Phase 14, package-doc-queue handler) on top. Phase 12 does NOT duplicate the state-machine logic or the slot-cascade handler.
- **`PrimaryResponsibleUserId` field exists** (`Appointment.cs`:90) since Phase 1.6 -- only the write-on-approve is missing, which Phase 12 adds.
- **`AppointmentDocument` entity constructor demands file metadata** (`AppointmentDocument.cs`:92-120) -- pre-upload "queued" rows are not constructible today. Phase 12's `PackageDocumentQueueHandler` ships as a logging stub; Phase 14 closes when the entity adds a queued-state factory.
- **No `AppointmentApprovedEto` / `AppointmentRejectedEto` exist** -- Phase 18 added forward-declared Etos under `Domain.Shared/Notifications/Events/` BUT the user's Phase 12 directive locked these specific Etos under `Application.Contracts/Appointments/Events/`. Phase 12 honors the user's path even though it deviates from the Phase 18 Eto convention; both locations are valid ABP placements.

### D. Audit-doc lifecycle annotations

The original gap table (lines 99-114) gets per-row `[IMPLEMENTED]` / `[DEFERRED]` / `[DESCOPED]` tags inline (see updated table). Two rows split into "implemented for backend / deferred for UI" because the backend writes the decision but UI surface lives in Session A's frontend track.

### E. Phase 12 implementation summary

Files added:

- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/Events/AppointmentApprovedEto.cs`
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/Events/AppointmentRejectedEto.cs`
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/ApproveAppointmentInput.cs`
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/IAppointmentApprovalAppService.cs`
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.Approval.cs` (sibling `AppointmentApprovalAppService` class -- naming deviation explained in commit body and §C above)
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentApprovalValidator.cs` (internal static helpers)
- `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/PackageDocumentQueueHandler.cs` (logging stub; Phase 14 wires row insert)
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Appointments/AppointmentApprovalController.cs`
- `test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/AppointmentApprovalValidatorUnitTests.cs`

Files modified (additive):

- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs` -- grants `Appointments.Approve` / `.Reject` to Clinic Staff + Staff Supervisor + IT Admin (Track Identity block).
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs` -- adds `AppointmentApprovalRequiresResponsibleUser`, `AppointmentNotPendingForApproval`, `AppointmentNotPendingForRejection`.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` -- localization keys for the three new error codes (verbatim OLD strings: "Appointment Already Approved", "Appointment Already Rejected", "Please select a responsible user before approving the appointment").

Strict-parity deviations called out:

- **Class-naming deviation:** user directive specified `partial class AppointmentsAppService` in `AppointmentsAppService.Approval.cs` plus "DO NOT edit the main `AppointmentsAppService.cs`". Partial classes need the `partial` keyword on every fragment, including the main-file declaration. Resolution: ship a sibling class `AppointmentApprovalAppService` in the user's requested file path. Functional outcome is identical (Approve/Reject endpoints under `api/app/appointment-approvals`); only class layout differs.
- **PackageDocumentQueueHandler is a logging stub:** the `AppointmentDocument` entity's constructor demands file metadata. Pre-upload row creation needs an entity-side queued-state factory that lives in Session B's Phase 14 territory. Phase 12 ships the subscriber wiring + log; Phase 14 lands the row insert.
- **Email + SMS handlers stay deferred:** Phase 18's `INotificationDispatcher` + per-feature handlers replace the existing inline-HTML `StatusChangeEmailHandler` (Session A territory) over time. Phase 14 (document review) lands the approve/reject template-keyed sends. Phase 12 publishes the Etos so the future handlers have something to subscribe to.
