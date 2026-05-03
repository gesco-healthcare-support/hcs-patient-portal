---
feature: clinic-staff-appointment-approval
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs (Update + UpdateValidation)
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\edit\
old-docs:
  - socal-project-overview.md (lines 471-485)
audited: 2026-05-01
status: audit-only
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
| Approve endpoint | Implicit via Update with status change | NEW: Update doesn't accept status | **Add `ApproveAppointmentAsync(Guid appointmentId, ApproveDto { ResponsibleUserId, PatientMatchOverride? })`** | B |
| Reject endpoint | Implicit | -- | **Add `RejectAppointmentAsync(Guid appointmentId, RejectDto { RejectionNotes })`** | B |
| Patient match check + override UI | OLD: shown on edit page | NEW: TO VERIFY | **Add `IsPatientAlreadyExist + matched PatientId` to GetWithNavigationPropertiesAsync response**; UI surfaces match details + override option | I |
| `PrimaryResponsibleUserId` set on approval | OLD field | NEW: missing | **Add `PrimaryResponsibleUserId Guid?` to Appointment**; require non-null on approve | B |
| `AppointmentApproveDate` written on approval | OLD field | NEW field exists; not written by Update | **Set `AppointmentApproveDate = DateTime.UtcNow`** in approve method | I |
| Rejection notes written | OLD: `RejectionNotes` + `RejectedById` | NEW: missing | **Add `RejectionNotes string?` and `RejectedById Guid?` fields** | B |
| Slot transition Reserved -> Booked on approve | OLD | NEW: no Reserved value in current enum (gap from booking audit) | **Add Reserved + transition** | B |
| Slot transition Reserved -> Available on reject | OLD | NEW: gap | **Add transition** | B |
| Auto-queue package docs on approve | OLD: `AddAppointmentDocumentsAndSendDocumentToEmail` | NEW: TO VERIFY | **Subscribe to `AppointmentApprovedEto` event in `PackageDocumentQueueHandler`** -- reads PackageDetail for AppointmentType, inserts AppointmentDocument rows | B |
| Two-package emails (patient-side + staff-side) | OLD | NEW: TO VERIFY | **Add 2 separate email templates + 2 separate sends** in approval handler | I |
| SMS to all stakeholders on approve / reject | OLD | NEW: TO VERIFY (SubmissionEmailHandler is email-only) | **Add SMS handler** subscribing to ApprovedEto + RejectedEto | I |
| Idempotency check (already Approved -> reject the call) | OLD validation | NEW: no state-machine guard | **State machine on `Appointment.SetStatus`** + idempotency at AppService level | I |
| Permissions | -- | -- | **Add `CaseEvaluation.Appointments.Approve` + `.Reject`**, gate to Clinic Staff / Staff Supervisor / IT Admin | B |
| Audit trail | OLD writes to `AppointmentChangeLogs` | NEW: ABP `[Audited]` on entity covers this | None | -- |

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
