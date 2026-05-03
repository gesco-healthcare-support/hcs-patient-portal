---
feature: external-user-appointment-joint-declaration
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentJointDeclarationDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-joint-declarations\
old-docs:
  - socal-project-overview.md (lines 415-419)
  - data-dictionary-table.md (AppointmentJointDeclarations)
audited: 2026-05-01
status: audit-only
priority: 1
strict-parity: true
depends-on:
  - external-user-appointment-request
---

# External user appointment joint declaration (AME-only)

## Purpose

For **AME** appointments only, the booking attorney (Applicant or Defense) must upload a **Joint Declaration Form** (JDF) before the appointment due date. If missing close to due date, the appointment **auto-cancels** and notifies all stakeholders.

**Strict parity with OLD.**

## OLD behavior (binding)

### Trigger / availability

- Available only when:
  - `AppointmentTypeId == AME` (or AME-REVAL); rejected for PQME with message `"Appointment type is not valid. Please upload appropriate document."`
  - Appointment status is `Approved`. Else: `"Please upload documents after appointment is approved."`
  - `appointment.DueDate >= DateTime.Now`. Else: `"You can not upload document after specified due date."`
- Only the **booking attorney** (the user who created the appointment, where role = ApplicantAttorney or DefenseAttorney) can upload the JDF. Strict parity: enforce via Appointment.CreatedById == CurrentUser.Id check.

### `AppointmentJointDeclarationDomain.Add` (initial create -- triggered automatically?)

- Generates filename: `{guid}_{requestConfirmationNumber}{MMddyyyyHHMMss}.{ext}`
- Saves file to `wwwroot/Documents/submittedDocuments/{filename}`
- Inserts `AppointmentJointDeclaration` row with `JointDeclarationFilePath = filename`
- (Note: OLD's `Add` method does not appear to set `DocumentStatusId` explicitly; presumably defaults via DB.)

### `AppointmentJointDeclarationDomain.Update` (re-upload by external OR review by internal)

External user path:
1. Save file with renamed filename: `{originalNameWithoutExt}{jdfId}_{ddMMyyyy_hhmmss}.{ext}`
2. Set `JointDeclarationFilePath = filePath`
3. Set `DocumentStatusId = Uploaded`
4. Set `ModifiedById = UserClaim.UserId`, `ModifiedDate = DateTime.Now`
5. Save

Internal user path (staff accept/reject):
1. Set `RejectedById = UserClaim.UserId` (TO VERIFY -- this looks buggy in OLD; should also set `DocumentStatusId = Rejected` or `Accepted`. Strict parity: replicate the bug? Or fix? **Recommend FIX** -- this is clearly broken and would have prevented staff from rejecting JDFs cleanly. Document as "OLD bug, fixed for correctness.")

### Auto-cancellation rule

Per spec line 419: `"In case if the document is pending as of specified number of days before the appointment due date, the appointment will be auto-cancelled and a notification email will be sent to all the stakeholders related to the appointment."`

Cutoff: `SystemParameters.JointDeclarationUploadCutoffDays`. Background job checks daily and auto-cancels.

### Critical OLD behaviors

- **AME-only.** PQME appointments do not require/accept JDF.
- **One JDF per appointment.** (Verify multi-JDF possible -- the data model allows it via `AppointmentId` FK without unique constraint, but business rule is one.)
- **Auto-cancel on missing JDF near due date.**
- **Per-document accept/reject** by clinic staff (similar to package docs).
- **Files stored locally** in `wwwroot/Documents/submittedDocuments/`.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentJointDeclarationDomain.cs` (322 lines) | Add, Update, Delete, GetValidation, SendDocumentEmail |
| `PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentJointDeclarationsController.cs` | API endpoints |
| `patientappointment-portal/.../appointment-joint-declarations/...` | Upload UI |
| `DbEntities.Models.AppointmentJointDeclaration` | EF entity |

## NEW current state

- **No `IAppointmentJointDeclarationsAppService` visible** in the `Application.Contracts/` glob output -- TO VERIFY (may exist or be folded into AppointmentDocuments).
- Per OLD's data model, `AppointmentJointDeclarations` is a SEPARATE table. NEW could either preserve as-is or fold into `AppointmentDocuments` with `IsJointDeclaration` flag (reusing the `IsAdHoc` flag pattern from the ad-hoc docs audit).

**Strict parity decision:** Keep as separate entity if it has unique fields; OR add `IsJointDeclaration` flag to unified `AppointmentDocument` entity. OLD has `IsJoinDeclaration` flag on `AppointmentDocuments` AND a separate `AppointmentJointDeclarations` table -- duplication. Cleaner in NEW: single flag on the unified entity.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| AME-only enforcement | OLD code checks AppointmentTypeId | NEW: TO VERIFY | **Add `[CanUploadJDF]` guard** -- Appointment.AppointmentType.Code == "AME" or "AME-REVAL" | B |
| Approved-only + before DueDate | OLD code enforces | TO VERIFY | **Add validation guard** in upload AppService | B |
| Booking-attorney-only upload | OLD: TO VERIFY explicit check (the code shows CurrentUser-based saves, but no explicit "are you the creator?" check) | NEW: TO VERIFY | **Add explicit check** that `Appointment.CreatedById == CurrentUser.Id` AND user is in attorney role | I |
| Per-document accept/reject by staff | Bug in OLD (only sets RejectedById, not Status) | -- | **FIX in NEW** -- set `DocumentStatusId = Accepted/Rejected` along with `RejectedById` | I |
| Auto-cancel on missing JDF near due date | Background job; cutoff `JointDeclarationUploadCutoffDays` | Verify if NEW's reminder jobs include this | **Add `JointDeclarationAutoCancelJob`** Hangfire recurring job; on trigger, set Appointment.AppointmentStatus = CancelledNoBill (or new "AutoCancelled" status?) + send notification | B |
| Email on auto-cancel | OLD sends to all stakeholders | -- | **Emit `AppointmentAutoCancelledEto` event** + notification handler | I |
| File storage | Local fs in OLD | `IBlobStorage` in NEW | **Use IBlobStorage**; container `appointment-{id}` | -- |
| Filename: `{guid}_{confirmationNumber}{timestamp}.{ext}` | OLD pattern | -- | **Match naming convention** for ops recognizability | C |

## Internal dependencies surfaced

- `SystemParameters.JointDeclarationUploadCutoffDays` (IT Admin slice).
- Email templates for JDF acceptance/rejection/auto-cancel.
- `Appointment.AppointmentType.Code` -- must be queryable from JDF guard.

## Branding/theming touchpoints

- JDF email templates (subject + body).
- Auto-cancel notification template.
- Upload page UI.

## Replication notes

### ABP wiring

- **Single `AppointmentDocument` entity** with `IsJointDeclaration` bool flag (alternative to separate entity); OR keep separate `AppointmentJointDeclaration` entity. Decision deferred to impl phase but recommend unified.
- **AME-only guard:** check `AppointmentType.Code` (or use a `RequiresJointDeclaration` bool on AppointmentType master).
- **Auto-cancel job:** Hangfire `RecurringJob` daily; query AME appointments where `Status == Approved` AND no JDF row exists AND `DueDate - SystemParameters.JointDeclarationUploadCutoffDays <= Today`.
- **State machine:** auto-cancel transitions to `CancelledNoBill` (or a new `AutoCancelled` value -- strict parity says match OLD; OLD presumably uses `CancelledNoBill` or `CancelledLate`; TO VERIFY).

### Things NOT to port

- `vInternalUser` view.
- Local fs storage.
- The bug where staff Accept/Reject only sets `RejectedById` but not the actual status.
- `_RoleId == UserClaim.UserId` weird filter (line 129) -- looks like a bug; fix: should be `UserId == UserClaim.UserId`.

### Verification (manual test plan)

1. Book AME -> Approved -> attorney logs in -> uploads JDF -> success
2. Book PQME -> Approved -> try to upload JDF -> rejected ("Appointment type is not valid")
3. Try to upload JDF on Pending appointment -> rejected
4. Try to upload JDF after DueDate -> rejected
5. Auto-cancel: AME with no JDF + due date approaching -> auto-cancelled at cutoff time -> all stakeholders emailed
6. Staff accepts JDF -> attorney email arrives
7. Staff rejects JDF with notes -> attorney email with notes -> re-upload allowed
8. Non-attorney user (Patient/Adjuster) tries upload -> rejected
9. Attorney who didn't book the appointment tries upload -> rejected (creator check)
