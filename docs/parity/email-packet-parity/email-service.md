# Email Service & Notifications - OLD vs NEW Parity Audit

**Date:** 2026-05-08 (Pass 1 + Pass 2 corrections appended)
**Scope:** Every email-touching code path in OLD (`P:\PatientPortalOld`) vs NEW (`W:\patient-portal\replicate-old-app`).
**Status:** Research / contract for next-session implementation. No code written.
**Companion doc:** `docs/parity/email-packet-parity/document-packets.md` (packet generation - separate task per Adrian).

> **READ FIRST:** This doc was produced in 2 passes. Pass 1 (sections 1-10) was rushed and contains material errors. Pass 2 (section 11+, "Pass 2 corrections") is the corrected ground truth - read it first if you find conflicts. Specific Pass 1 sentences are also amended inline with bracketed `[PASS 2 CORRECTION: ...]` notes.

---

## 0. How to read this doc

- **OLD citations** are file path + line number from a fresh read of `P:\PatientPortalOld` on 2026-05-08. The wave-1-parity audit drafts (under `docs/parity/wave-1-parity/`) drifted; do NOT trust their line numbers without re-verifying.
- **NEW citations** are the current source under `src/HealthcareSupport.CaseEvaluation.*/`.
- **Phase 1 gating** (Adrian, 2026-05-06): only 3 email types are scope-active right now (verification, appointment-requested, appointment-approved/rejected). Two reasons handlers are gated off: (1) Azure ACS rate limits during demos, (2) trigger / recipient ambiguity. **All gated handlers must still be re-enabled later** - this audit is the contract for that work.
- **Auto-rebuild status (2026-05-08):** `docker compose down && up -d --build` ran cleanly for sql/redis/api/authserver. The `angular` container exits 255 with `exec /app/dev-entrypoint.sh: no such file or directory` - the script does not exist in the repo. Adrian is fixing in a separate session.

---

## 1. Executive summary

| Item | OLD | NEW | Gap |
|---|---|---|---|
| **Transport** | `ISendMail.SendSMTPMail` (`async void`, fire-and-forget, exceptions swallowed) loads `SMTPConfiguration` row from DB on every send | ABP `IEmailSender` -> `MailKit` SMTP via Hangfire `SendAppointmentEmailJob`. Swapped to `NullEmailSender` in Development when SMTP creds start with `REPLACE_`. | NEW transport is sound; OLD's `ISendMail.SendSMTPMailWithAttachment` (used for packet delivery) has no NEW equivalent today. |
| **Template store** | TWO mechanisms: `Templates` SQL table (16 rows, `TemplateCode` int enum) + 57 HTML files on disk (`wwwroot/EmailTemplates/`, of which 43 are referenced by C# enum + 14 are dead). | Single `NotificationTemplate` table per tenant. 59 codes seeded (16 + 43 unified). 6 codes have OLD-verbatim HTML bodies; 53 codes carry stub strings. | Body migration for the 53 stub codes is the bulk of remaining work. |
| **Variable substitution** | Reflection-based (every `vEmailSenderViewModel` property -> `##PropName##`) + 7 hardcoded brand tokens. ToUpper applied selectively. | `TemplateVariableSubstitutor.Substitute(body, variables)` - flat `body.Replace("##Key##", value)` loop. DateTime values formatted `MM/dd/yyyy` invariant. | NEW handler must build the variables dict per-template; OLD's "all properties become tokens automatically" is not replicated. Each NEW handler currently builds the dict by hand. |
| **Recipient resolution** | `GetAppointmentStackHoldersEmailPhone(appointmentId)` -> stored proc `spm.spAppointmentStackHoldersEmailAndPhone` returns semicolon-joined `EmailList`. | `IAppointmentRecipientResolver.ResolveAsync(id, kind)` walks 7 sources (link tables + appointment-level email columns + booker + patient + office mailbox), dedupes by email first-wins, returns role-tagged `SendAppointmentEmailArgs`. | Functional parity reached; the ROLE TAG dimension is a NEW improvement. |
| **CC behavior** | Hardcoded `ServerSetting clinicStaffEmail` appended on Pending + Approved sends only (NOT Rejected, NOT change-request). | `SystemParameter.CcEmailIds` (per-tenant, semicolon-separated) appended as additional recipients in `BookingSubmissionEmailHandler` only. | NEW does not yet append CC for Approved/Rejected paths - parity gap. |
| **Active handlers** | 50+ explicit `SendMail.SendSMTPMail*` call sites across 11 domain classes + 9 scheduler reminder methods (2 of which are commented out at the call site). | 11 wired handlers in `Application/Notifications/Handlers/` + 1 inline-HTML `SubmissionEmailHandler` in `Domain/Appointments/Handlers/`. | Specific gaps tabulated in section 6. |
| **CC list at Pending stakeholder** | `clinicStaffEmail` (single dev address; `pooja.fithani@radixweb.com` in OLD's server-settings.json) | `BookingSubmissionEmailHandler` appends `SystemParameter.CcEmailIds` BUT the Pending stakeholder dispatch is **commented out** at line 138-147 (`B15-followup`). The Domain `SubmissionEmailHandler` covers stakeholders with inline HTML; CC is not appended there. | The "CC the office on Pending" parity is broken in NEW. See section 6 row 2. |
| **Demo-critical errata** | Subject typo "Your have registered" (`UserDomain.cs:321`); template constant `Apprvd`; HTML filename `Apporved.html`; constant `RejectedJoinDeclarationDocument`; "Stackholder" mis-spelling; "AppointmenTime" token typo | All 4 typos fixed in NEW (`NotificationTemplateConsts.Codes` + `EmailSubjects.UserRegistered`); the AppointmenTime token typo is preserved in `PacketTokenContext.AppointmentTime`'s XML doc but the property name is corrected. | Document the corrections so reviewers don't flag them as drift. |

**Bottom line.** NEW has a solid platform (dispatcher + renderer + recipient resolver + 11 handlers + per-tenant template store) but only 6 of 59 templates carry real OLD-verbatim bodies; ~30% of the OLD send call sites have no NEW handler today; CC behavior is partial; and one stakeholder-side branch (`BookingSubmissionEmailHandler` Pending dispatch) is intentionally commented out. The work to close parity is wiring + body authoring, not architecture.

---

## 2. OLD email surface - line-by-line catalog

The following table is the complete inventory of EVERY `SendMail.SendSMTPMail*` call site in `P:\PatientPortalOld\PatientAppointment.Domain\` (the Domain layer). Verified by `grep` then file read 2026-05-08.

`ISendMail.SendSMTPMail*` is defined at `PatientAppointment.Infrastructure\Utilities\SendMail.cs:166-265` (no-attachment) and `:388-463` (with-attachment). The interface (line 467-472) only exposes the SMTP variants - the AWS SES variants (`SendSMTPMailAWS`, `SendSMTPMailWithAttachmentAWS`) exist on the class but are NOT in the public interface, so they are dead code reachable only through reflection. **Important corrigendum:** the prior wave-1 audit claimed `SystemParameter.CcEmailIds` is auto-appended to all outbound mail. **That is wrong.** It only fires on the AWS variants (`SendSMTPMailAWS:49`, `SendSMTPMailWithAttachmentAWS:276`). The SMTP path (the only one actually called) does NOT touch SystemParameter. CC is appended only when the caller passes it explicitly via the `mailCC` parameter.

### 2.1 User auth + lifecycle (`UserAuthenticationDomain.cs`, 336 lines)

| OLD line | Trigger | Recipient | Subject | Template | Variables | NEW status |
|---|---|---|---|---|---|---|
| `:138-139` | `PostLogin` when `!user.IsVerified` - **fires on every unverified login attempt**, not just registration. Generates `VerificationCode = Guid.NewGuid()` if missing. | `user.EmailId` | `"Your have registered successfully - Patient Appointment portal"` (typo "Your") | `EmailTemplate.UserRegistered` (`User-Registed.html`, filename typo) | `PatientFirstName`, `PatientLastName`, URL = `clientUrl + "/verify-email/" + UserId + "?query=" + VerificationCode` | NEW does NOT replicate the "fire on every unverified login" behavior. NEW only sends UserRegistered once on initial registration. **Behavioral gap.** |
| `:201-209` | `PostForgotPassword` after committing a fresh `VerificationCode` | `userCredential.EmailId` | `"Patient Appointment Portal - Reset Password"` | `EmailTemplate.ResetPassword` | `PatientFirstName`, `PatientLastName`, URL = `clientUrl + "/reset-password/?activationkey={code}&emailId={email}"` | **No NEW handler.** Password reset goes through ABP Account UI; not wired through the dispatcher. Stub template in seed. |
| `:300-318` | `SendEmail(user)` private helper, called from `PutCredential:253` after password change | `user.EmailId` | `"Your password has been successfully changed - Patient Appointment portal"` | `EmailTemplate.PasswordChange` | `PatientFirstName`, `PatientLastName` | **No NEW handler.** Same ABP-stock issue as ResetPassword. |

### 2.2 User CRUD (`UserDomain.cs`, 365 lines)

| OLD line | Trigger | Recipient | Subject | Template | Variables | NEW status |
|---|---|---|---|---|---|---|
| `:309` | `AddInternalUser:281-312` - new internal user created with random 8-char password | `user.EmailId` | `"Welcome to socal"` (lowercase "socal", no portal prefix) | `EmailTemplate.AddInternalUser` | `UserName = "{first} {last}"`, `LoginUserName = email`, `Password = randomPassword` (**plaintext password in email body**) | **No NEW handler.** Internal user creation goes through ABP Identity Suite UI which fires its own emails. The plaintext-password security regression is not replicated (an improvement). |
| `:332` | `SendEmail(user, isNewUser=true)` from `Add:126` for external users | `user.EmailId` | `"Your have registered successfully - Patient Appointment portal"` (same typo) | `EmailTemplate.UserRegistered` | `PatientFirstName`, `PatientLastName`, URL = same verify-email construction | NEW: `UserRegisteredEmailHandler` at `Application/Notifications/Handlers/UserRegisteredEmailHandler.cs`. Subscribes to `UserRegisteredEto`. Uses ABP `GenerateEmailConfirmationTokenAsync` instead of a `VerificationCode` GUID column. Verify URL points at SPA `/account/email-confirmation` (not AuthServer Razor). Subject typo fixed. **Wired and active.** |
| `:345` | `SendEmail(user, false)` from `Update:188` after IsChangePassword | `user.EmailId` | `"Your password has been successfully changed - Patient Appointment portal"` | `EmailTemplate.PasswordChange` | `PatientFirstName`, `PatientLastName` | Same gap as `UserAuthenticationDomain:317`. |

### 2.3 User queries (`UserQueryDomain.cs`, 180 lines)

| OLD line | Trigger | Recipient | Subject | Template | Variables | NEW status |
|---|---|---|---|---|---|---|
| `:88` | `Add` when query is associated with an Approved appointment - sends to the appointment's `PrimaryResponsibleUserId` | `vInternalUserEmail.EmailId` for the responsible user | `"Patient Appointment Portal - {patientDetailsEmailSubject} - User query"` | `EmailTemplate.UserQuery` | `UserQueryMessage = userQuery.Message` | **No NEW handler.** UserQuery feature deferred per Phase 1 scope. Stub template in seed. |
| `:104` | `Add` when query has no appointment - sends to ALL users with `RoleId == ItAdmin` | `;`-joined ItAdmin emails | `"Patient Appointment Portal - {patientDetailsEmailSubject} User query"` (note: subject lacks the `-` between `{subject}` and `User query`, inconsistent with `:88`) | `EmailTemplate.UserQuery` (same template, different recipient set) | `UserQueryMessage` | Same: deferred. |

### 2.4 Scheduler / cron jobs (`SchedulerDomain.cs`, 380 lines)

`SchedulerDomain.ConfigureNotificaion(SchedulerParameters)` is a switch over 9 reminder types. Each type queries a stored proc, builds a per-row `vEmailSenderViewModel`, and sends. **Important:** 2 of the 9 have their `SendSMTPMail` call commented out at the call site - the proc still fires but no email goes out. Also note one proc-result branch hardcodes `devendra.lohar@radixweb.com;karnavi.soni@radixweb.com` as recipients (OLD developer emails left in production code).

| OLD line | Reminder type | Recipient | Subject | Template | Variables | Status |
|---|---|---|---|---|---|---|
| `:84` | `PendingAppointmentDailyNotification` | `ServerSetting.clinicStaffEmail` | `"Pending Appointment Request"` | `EmailTemplate.PendingAppointmentDailyNotification` | `DailyNotificationContent` (raw SP result) | Active in OLD. **No NEW handler.** No background-job scheduling of reminders in NEW. |
| `:113` | `AppointmentApproveRejectInternalUser` | `item.EmailList` (per-row staff list from SP) | `"Updated Appointment Request"` | `EmailTemplate.AppointmentApproveRejectInternal` | `PendingAppointmentCount`, `ApprovedAppointmentCount` | Active. **No NEW handler.** |
| `:146` | `AppointmentPackageDocumentPending` | `item.EmailList`, CC = `item.PrimaryEmailList` | `"Please Upload Pending Documents"` | `EmailTemplate.UploadPendingDocuments` | `AppointmentRequestConfirmationNumber`, `DueDate`, `PendingDocList` | Active. NEW: `PackageDocumentReminderEmailHandler` exists (subscribes to `PackageDocumentReminderEto`) but **the Eto is never published** - no NEW recurring job that fires it. Handler is dead code. Also note NEW's handler uses `AppointmentDocumentIncomplete` template not `UploadPendingDocuments` (drift from OLD). |
| `:171` | `AppointmentDueDateApproaching` | `item.EmailList` | `"Appointment Due Date Approaching"` | `EmailTemplate.AppointmentDueDateReminder` | `AppointmentRequestConfirmationNumber`, `DueDate` | Active. **No NEW handler.** |
| `:199` | `AppointmentDueDateDocumentApproaching` | `item.EmailList` | `"Appointment Document Incomplete"` | `EmailTemplate.AppointmentDocumentIncomplete` | `AppointmentRequestConfirmationNumber`, `PendingDocList` | Active. NEW handler `PackageDocumentReminderEmailHandler` uses this template but for a different trigger (see row 3). |
| `:229` | `AppointmentJointDeclarationDocumentUpload` | `item.EmailList` | `"Please Upload Pending Documents"` | `EmailTemplate.UploadPendingDocuments` (REUSED) | `AppointmentRequestConfirmationNumber`, `DueDate`, `PendingDocList` | Active. **No NEW handler.** |
| `:257` | `AppointmentAutoCancelled` | `item.EmailList` | `"Appointment Cancelled"` | `EmailTemplate.AppointmentCancelledDueDate` | `AppointmentRequestConfirmationNumber`, `DueDate` | Active. NEW: `JdfAutoCancelledEmailHandler` uses this same template but only fires for the JDF-not-uploaded reason. The generic due-date elapsed reason is unhandled. |
| `:309` | `AppointmentPendingReminderStaffUsers` | `item.EmailList` | `"Next day Pending Appointments"` | `EmailTemplate.AppointmentPendingNextDay` | 9 properties (PatientFirstName, DOB, AppointmentTypeName, ResponsibleUserName, ApproveDate, AppointmentDate, DefenseName/Email/Phone, ApplicantAttorneyName/Email/Phone) | **`SendSMTPMail` is commented out at L309.** OLD wires the proc + builds the body but does not actually send. |
| `:361` | `AppointmentPendingDocumentSendToResponsibleUser` | `item.EmailList` | `"Appointment Pending Document"` | `EmailTemplate.UploadPendingDocuments` | Same as :146 | **`SendSMTPMail` is commented out at L361.** Same pattern. |

### 2.5 Booking lifecycle (`AppointmentDomain.cs`, 1100+ lines)

`SendEmail(int statusId, Appointment appointment, string emails, bool internalUserUpdateStatus)` at `:883-1049` is the dispatcher; status-switch in 7 branches.

| Caller | Calling-line | Status | OLD status email line(s) | Subject | Template | Recipients (override of `emails` param) |
|---|---|---|---|---|---|---|
| `Add:290` | After committing the appointment + (if internal user) calling `AddAppointmentDocumentsAndSendDocumentToEmail`. Always fires with `internalUserUpdateStatus=false`. | Initial booking - status will typically be Pending or Approved | `:933` (Pending) -> all stakeholders + `clinicStaffEmail` CC; `:950` (Pending + external user) -> all StaffSupervisor+ClinicStaff users **NO CC** | `Patient Appointment Portal - {bracketed patient details} - Your appointment request has been Pending.` (`:926`) AND `... - Approve or Reject New Appointment Request` (`:940`) | `PatientAppointmentPending` (`:930`); `PatientAppointmentApproveReject` (`:944`) | First send: `appointmentStackHoldersEmailPhone.EmailList` (from stored proc). CC: `ServerSetting.clinicStaffEmail`. Second send: only when `currentUserTypeId == ExternalUser` - all `vInternalUserEmail` rows where `RoleId == StaffSupervisor || RoleId == ClinicStaff`. |
| `Update:559` | After commit on status change | Whatever new status the row was set to | Same switch | Same per-status | Same per-status | `appointmentStackHoldersEmailPhone.EmailList`. |
| `Update:563` | Only when `IsStatusUpdate && IsInternalUserUpdateStatus && AppointmentStatus == Approved` | Approved | `:966` -> single recipient + clinicStaffEmail CC | `... - Your appointment request has been approved successfully.` (`:957`, **same string** as :970) | `PatientAppointmentApprovedInternal` (`:965`) | Single recipient: `User.EmailId` for `appointment.PrimaryResponsibleUserId`. CC: `clinicStaffEmail`. **Triggers packet generation immediately after via `:564 AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail(appointment)`.** |

The 7 status switch branches inside `SendEmail`:

| OLD line | Status | Subject | Template | Variables | Recipients | CC |
|---|---|---|---|---|---|---|
| `:925-952` | `Pending` | `... - Your appointment request has been Pending.` | `PatientAppointmentPending` | `PatientFirstName`, `PatientLastName`, `AppointmentDate` (MM-dd-yyyy), `AppointmentFromTime` (hh:mm tt), `AppointmentToTime`, `AppointmentRequestConfirmationNumber`, `CancellationReason` | `emails` param (stakeholder list) | `clinicStaffEmail` |
| `:935-951` (inside Pending branch) | Pending + ExternalUser | `... - Approve or Reject New Appointment Request` | `PatientAppointmentApproveReject` | Same | All `StaffSupervisor + ClinicStaff` users | NONE |
| `:953-983` | `Approved` | `... - Your appointment request has been approved successfully.` | `PatientAppointmentApprovedInternal` (when internalUserUpdateStatus) OR `PatientAppointmentApprovedExt` (else) | Same + `InternalUserComments` wrapped `<b> Staff comments for an appointment: </b>...` (Internal) or `<b> Please note: </b>...` (Ext) | `emails` param | `clinicStaffEmail` |
| `:984-991` | `Rejected` | `... - Your appointment request has been rejected by our clinic staff.` | `PatientAppointmentRejected` | Same + `RejectionNotes` wrapped `Please note rejection reason: ...` | `emails` param | NONE |
| `:992-1003` | `CheckedIn` | `... - Your appointment request has been checked In by our clinic staff.` | `PatientAppointmentCheckedIn` | Same + `RejectionNotes` (yes, rejection notes on a CheckedIn email - copy-paste artifact) | `emails` param | NONE |
| `:1004-1015` | `CheckedOut` | `... - Your appointment request has been checked out by our clinic staff.` | `PatientAppointmentCheckedOut` | Same + RejectionNotes (same copy-paste) | `emails` param | NONE |
| `:1016-1027` | `NoShow` | `... - Appointment request number :<b style='font-size:17px'> {n}</b>  has been no show.` | `PatientAppointmentNoShow` | Same | **Override:** all `StaffSupervisor + ClinicStaff` users (`emails` param IGNORED) | NONE |
| `:1029-1034` | `CancelledNoBill` | `... - Your appointment has been cancelled.` | `PatientAppointmentCancelledNoBill` | Same | `emails` param | NONE |

**The bracketed patient-details prefix construction at `:921`:** `"(" + patientName + (claim ? " - Claim: {x}" : "") + (wcabAdj ? " - ADJ: {y}" : "") + ")"`. NEW expresses this via the `##EmailSubjectIdentity##` token in `EmailSubjects.cs`. Construction logic must move into the dispatcher.

### 2.6 Change requests - cancel + reschedule (`AppointmentChangeRequestDomain.cs`, 1000+ lines)

| OLD line | Trigger | Recipient | Subject | Template | Variables | NEW status |
|---|---|---|---|---|---|---|
| `:659` (`SendEmailToClinicStaffForCancellation`) | Cancel-request submitted (called from `Update`) | `email` param (single clinic staff?) | `... - Appointment request has been cancelled` | `EmailTemplate.ClinicalStaffCancellation` | PatientFirstName, PatientLastName, AppointmentDate, AppointmentFromTime, CancellationReason wrapped `Please review cancellation reason: ...` | **No NEW handler** for ClinicalStaffCancellation. |
| `:717` | `SendEmailData` -> `isAdminReschedule=true` | `appointmentStackHoldersEmailPhone.EmailList` (line 770) | `... - Reschedule request has been changed by our team.` | `PatientAppointmentRescheduleReqAdmin` | `ReScheduleReason`, `NewAppointmentDate`, `NewAppointmentFromTime` | NEW `ChangeRequestApprovedEmailHandler` collapses the admin-override into the same `AppointmentRescheduleRequestApproved` template - **template gap**: NEW does not use `PatientAppointmentRescheduleReqAdmin`. |
| `:730` | `isRescheduleRequestApproved=true` | Stakeholder list | `... - Your reschedule request has been approved` | `PatientAppointmentRescheduleReqApproved` | `ReScheduleReason`, `NewAppointmentDate`, `NewAppointmentFromTime` | NEW `ChangeRequestApprovedEmailHandler` uses `AppointmentRescheduleRequestApproved` (TemplateCode 8 in OLD's int enum). **Two templates conflated; OLD has both.** |
| `:740` | `isRescheduleRequestRejected=true` | Stakeholder list | `... - Your reschedule request has been rejected` | `PatientAppointmentRescheduleReqRejected` | RejectionNotes wrapped | NEW `ChangeRequestRejectedEmailHandler` uses `AppointmentRescheduleRequestRejected`. **Same conflation: NEW uses TemplateCode-9 only, doesn't load the disk-HTML `PatientAppointmentRescheduleReqRejected` body.** |
| `:747` | `isCancellationRequestApprved=true` | Stakeholder list | `... - cancellation request has been accepted` | `PatientAppointmentCancellationApprvd` (typo: "Apprvd" + filename "Apporved.html") | Standard | NEW `ChangeRequestApprovedEmailHandler` uses `AppointmentCancelledRequestApproved` (TC 5). Typo fixed in NEW seed. |
| `:767` | `rescheduleRequested=true` (user submits a reschedule) | Stakeholder list | `... -  Your have successfully requested for reschedule.` (typo: "Your have", same as registration subject) | `PatientAppointmentRescheduleReq` | `ReScheduleReason`, `NewAppointmentDate`, `NewAppointmentFromTime` | NEW `ChangeRequestSubmittedEmailHandler` uses `AppointmentRescheduleRequest` (TC 7). Disk-HTML body not loaded. |
| `:945-998` (`SendEmailOfJointAggDocumentStatus`) | Joint Agreement Letter document status change | Per branch | Per branch | `JointAgreementLetterAccepted`/`Uploaded`/`Rejected` | Standard | **No NEW handler for the JointAgreementLetter* templates** - NEW unifies JDF doc upload into `DocumentUploadedEmailHandler` etc. which all use `PatientDocument*` templates not `JointAgreementLetter*`. |

### 2.7 Document workflows - 3 separate domain classes

OLD has THREE separate domain classes for document uploads, each with its own SendDocumentEmail method:

#### `AppointmentDocumentDomain.SendDocumentEmail(AppointmentDocument)` at `:240-304`
For "package" documents (post-approval queued docs).

| OLD line | Status | Subject | Template | Recipient |
|---|---|---|---|---|
| `:269` | `Accepted` | `... - Appointment document is Accepted.` | `EmailTemplate.PatientDocumentAccepted` | `user.EmailId` (creator/uploader) |
| `:286` | `Rejected` | `... - Appointment document is Rejected.` | `EmailTemplate.PatientDocumentRejected` (with `RejectionNotes` wrapped) | `user.EmailId` |
| `:297` | `Uploaded` | `... - Appointment document is uploaded by user.` | `EmailTemplate.PatientDocumentUploaded` | `responsibleUserDetails.EmailId` (PrimaryResponsibleUserId) |

#### `AppointmentDocumentDomain.SendDocumentEmail(AppointmentNewDocument)` at `:305-385`
For ad-hoc documents stored in the SECOND domain class for `AppointmentNewDocument`.

| OLD line | Status | Subject | Template | Recipient |
|---|---|---|---|---|
| `:340` | `Accepted` | `... - Appointment document is Accepted.` | `EmailTemplate.PatientNewDocumentAccepted` (DIFFERENT template, "New" prefix) | `user.EmailId` |
| `:362` | `Rejected` | `... - Appointment document is Rejected.` | `EmailTemplate.PatientNewDocumentRejected` | `user.EmailId` |
| `:378` | `Uploaded` | `... - Appointment document is uploaded by user.` | `EmailTemplate.PatientNewDocumentUploaded` | `responsibleUserDetails.EmailId` |

#### `AppointmentNewDocumentDomain.SendDocumentEmail(AppointmentNewDocument)` at `:164-255` (DIFFERENT FILE)
**This is a separate implementation.** Same input type, different templates.

| OLD line | Status | Subject | Template | Recipient |
|---|---|---|---|---|
| `:214` | `Accepted` | Same as above | `EmailTemplate.PatientDocumentAccepted` (NOT `PatientNewDocument*`) | `user.EmailId` |
| `:231` | `Rejected` | Same | `EmailTemplate.PatientDocumentRejected` | `user.EmailId` |
| `:242` | `Pending` | `...uploaded by user.` | `EmailTemplate.PatientDocumentUploaded` | `responsibleUserDetails.EmailId` |

**This is a bug or refactoring leftover in OLD.** Two methods send emails for AppointmentNewDocument with different templates. Strict-parity policy says: replicate one path, flag the other. Since this is OLD, NEW unifies into a single handler per status (`DocumentUploadedEmailHandler` / `DocumentAcceptedEmailHandler` / `DocumentRejectedEmailHandler`) using `PatientDocument*` templates and the `IsAdHoc`/`IsJointDeclaration` flags on `AppointmentDocument` to pick branch behavior. The `PatientNewDocument*` templates are seeded in NEW but not yet wired to a handler - documenting the intent so a future implementer can branch on the flag.

#### Joint Declaration (AME-only) (`AppointmentJointDeclarationDomain.cs`, 323 lines)

`AppointmentJointDeclarationDomain.SendDocumentEmail(AppointmentJointDeclaration)` at `:200-282`. Crucially, JDF Add (`:65-101`) and Update (`:127-179`) **do NOT call SendDocumentEmail**. The send method is on the public interface (`:319`), so external callers can invoke it - but inside the domain class itself it's unreachable from local Add/Update. The actual email fires via `AppointmentChangeRequestDomain.SendEmailOfJointAggDocumentStatus` (already in 2.6) which uses the `JointAgreementLetter*` templates. So `AppointmentJointDeclarationDomain.SendDocumentEmail` is wired through a public interface but effectively dead in OLD's actual call graph.

### 2.8 Accessors (`AppointmentAccessorDomain.cs`, 390 lines)

| OLD line | Trigger | Recipient | Subject | Template | Variables | NEW status |
|---|---|---|---|---|---|---|
| `:260` | `SendEmailToAccessor` called from `CreateAccountOfAppointmentAccessors` (in turn called from `AppointmentDomain.Add:285` for any accessors on a fresh appointment) | `email` param (the accessor's email) | `Patient Appointment Portal - {bracketed patient details} - Please find these appointment details` | `EmailTemplate.AccessorAppointmentBooked` | PatientFirstName, PatientLastName, AppointmentRequestConfirmationNumber, AppointmentDate, AppointmentFromTime, **`UserInfo`** (only when accessor is a new user) = `"EmailId : {email} Password : {plaintext password}"` | NEW: `AccessorInvitedEmailHandler`. **Security improvement preserved** - NEW issues a single-use ABP password-reset token instead of plaintext password. URL points at `/Account/ResetPassword?userId=...&resetToken=...`. |

### 2.9 Audit / change log (`AppointmentChangeLogDomain.cs`, 400+ lines)

| OLD line | Trigger | Recipient | Subject | Template | Variables | NEW status |
|---|---|---|---|---|---|---|
| `:309` | `SendEmailForChangeLog` called from `SendEmailForIntakeFormChanges:350` when intake-form fields change. Called from `AppointmentDomain.Update:569`. | `emails` param (stakeholder list) | `... - Upload documents` (intake form changed -> reupload prompt) | `EmailTemplate.AppointmentChangeLogs` | PatientFirstName, PatientLastName, PatientEmail, AppointmentDate, AppointmentFromTime, AppointmentRequestConfirmationNumber, **AppointmentChangeLogs (HTML table of FieldName/OldValue/NewValue rows)** | **No NEW handler.** Audit-log feature deferred. |
| `:325` | `SendEmailForAppointmentTimeChangedByAdmin` called from `:371` when an Appointment Date or Time field changes via change log | `emails` param | `... - Appointment reschedule changes by our staff` | `EmailTemplate.AppointmentRescheduleRequestByAdmin` | RequestConfirmationNumber, AppointmentDate, AppointmentFromTime | **No NEW handler.** Distinct from `:717` reschedule path. |

### 2.10 Total OLD email send call sites

Counted from grep:
- `UserAuthenticationDomain.cs`: 3 sends
- `UserDomain.cs`: 3 sends (1 Welcome, 2 in `SendEmail`)
- `UserQueryDomain.cs`: 2 sends
- `SchedulerDomain.cs`: 9 reminder methods, 7 active + 2 commented out
- `AppointmentDomain.cs`: 7 sends in switch + 2 sends from callers (`:933` is the actual SMTP call counted in switch)
- `AppointmentChangeRequestDomain.cs`: 5 SendMail calls (`:659, :770, :979, :990, :997`)
- `AppointmentDocumentDomain.cs`: 7 SendMail calls (`:269, :286, :297, :340, :362, :378` for `SendDocumentEmail`; `:491, :683, :731, :777, :791, :838, :851` are `SendSMTPMailWithAttachment` for packets - covered in packet audit)
- `AppointmentNewDocumentDomain.cs`: 3 sends in `SendDocumentEmail`
- `AppointmentJointDeclarationDomain.cs`: 2 sends in `SendDocumentEmail` (effectively dead)
- `AppointmentAccessorDomain.cs`: 1 send (`SendEmailToAccessor`)
- `AppointmentChangeLogDomain.cs`: 2 sends

**Active distinct templates referenced: 36** (not counting the 3 commented-out scheduler methods or dead reminder paths). The "59 templates" framing from prior wave-1 audits comes from counting both the 16 DB-managed `TemplateCode` enum entries AND the 43 `EmailTemplate` static class entries; many of those are not actually invoked. The next session should not blindly write 59 handlers - prioritize the 36 active paths.

---

## 3. NEW email surface - line-by-line catalog

Verified by reading every file in `src/HealthcareSupport.CaseEvaluation.Application/Notifications/` and `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/` on 2026-05-08.

### 3.1 Infrastructure layer

| Component | Path | Purpose |
|---|---|---|
| `INotificationDispatcher` + `NotificationDispatcher` | `Application/Notifications/NotificationDispatcher.cs` | Top-level facade. Loads template via renderer, fans out to `IBackgroundJobManager.EnqueueAsync(SendAppointmentEmailArgs)` per recipient. Empty recipient list short-circuits. SMS leg deferred. |
| `INotificationTemplateRenderer` + `NotificationTemplateRenderer` | `Application/Notifications/NotificationTemplateRenderer.cs` | Loads `NotificationTemplate` by code via repository, runs `TemplateVariableSubstitutor.Substitute` on subject/body/bodySms, returns `RenderedNotification`. Throws `BusinessException(NotificationTemplateNotFound)` on missing template. |
| `TemplateVariableSubstitutor` | `Domain/Notifications/TemplateVariableSubstitutor.cs` | Pure function: `body.Replace("##Key##", value)` for each kvp. Mirrors OLD's `ApplicationUtility.GetEmailTemplateFromHTML` reflection loop verbatim. DateTime values formatted `MM/dd/yyyy` invariant. |
| `IAppointmentRecipientResolver` + `AppointmentRecipientResolver` | `Domain/Appointments/Notifications/AppointmentRecipientResolver.cs` | Walks 7 sources (AA link table -> DA link table -> ClaimExaminer -> appointment-level email columns -> booker user -> patient row -> office mailbox). Dedupes by email first-wins. Returns role-tagged `SendAppointmentEmailArgs[]`. |
| `IRecipientRoleResolver` + `RecipientRoleResolver` | `Domain/Appointments/Notifications/RecipientRoleResolver.cs` | Classifies a typed email against an EXPECTED role - prevents same-email accounts registered under a different role from being mis-tagged. |
| `DocumentEmailContextResolver` | `Application/Notifications/Handlers/DocumentEmailContextResolver.cs` | Reusable context loader for document handlers. Pulls appointment + patient + booker user + first injury + branding + (optional) document row + tenant settings (PortalBaseUrl). Returns `DocumentEmailContext` snapshot. |
| `DocumentNotificationContext.BuildVariables(...)` | `Application/Notifications/Handlers/DocumentNotificationContext.cs` (static helper) | Builds the variables dict for document-related handlers. Standard tokens: PatientFirstName/LastName/Email, RequestConfirmationNumber, AppointmentDate, ClaimNumber, WcabAdj, DocumentName, RejectionNotes, ClinicName, PortalBaseUrl. |
| `EmailSubjectBuilder` | `Application/Notifications/EmailSubjectBuilder.cs` | Builds the bracketed subject identity prefix (`(Patient: X Y - Claim: Z - ADJ: W)`) from per-recipient context. Mirrors OLD `:921`. |
| `EmailBodyResources` | `Domain/NotificationTemplates/EmailBodyResources.cs` | Loads OLD-verbatim HTML bodies from embedded resources at `NotificationTemplates/EmailBodies/*.html`. Returns null when the resource is absent (seed falls back to stub). |
| `EmailSubjects.ByCode` | `Domain/NotificationTemplates/EmailSubjects.cs` | Per-code subject lookup. **Only 6 entries today**: UserRegistered, PatientAppointmentPending, PatientAppointmentApproveReject, PatientAppointmentApprovedInternal, PatientAppointmentApprovedExt, PatientAppointmentRejected. The other 53 codes get a stub subject `"[code] -- TODO: parity-correct subject"`. |
| `NotificationTemplateDataSeedContributor` | `Domain/NotificationTemplates/NotificationTemplateDataSeedContributor.cs` | Host pass: seeds `NotificationTemplateType` rows (Email + SMS). Tenant pass: ensures one `NotificationTemplate` row per code in `NotificationTemplateConsts.Codes.All`. Re-seed overwrites subject + body when `EmailBodyResources.TryLoadBody` returns non-null (so template updates flow through). IT-Admin edits to non-resource-backed (stub) codes are preserved. |
| `IEmailSender` swap | `Domain/CaseEvaluationDomainModule.cs:85-97` | Replaces `IEmailSender` with `NullEmailSender` ONLY when `ASPNETCORE_ENVIRONMENT=Development` AND the SMTP UserName/Password starts with `REPLACE_`. Real ACS creds bypass the gate. |

### 3.2 Wired handlers (12 total)

11 in `Application/Notifications/Handlers/` + 1 in `Domain/Appointments/Handlers/`. The Domain-layer one is the active stakeholder-side booking email; the Application-layer `BookingSubmissionEmailHandler` has its stakeholder dispatch commented out.

| Handler | Eto | OLD parity | Status |
|---|---|---|---|
| `UserRegisteredEmailHandler` | `UserRegisteredEto` | `UserDomain.SendEmail(user, true)` `:332` | **Active.** Subject typo fixed. Verify URL points at SPA `/account/email-confirmation`. Strict-role match - unknown roles abort with warning rather than coerce to Patient. |
| `BookingSubmissionEmailHandler` | `AppointmentSubmittedEto` | `AppointmentDomain.SendEmail(Pending, ...)` calls `:933, :950` | **Half-active.** The Pending-stakeholder dispatch is commented out at `:138-147` (B15-followup, 2026-05-07). Only the staff-blast for external-user bookings is active. |
| `SubmissionEmailHandler` (Domain layer) | `AppointmentSubmittedEto` | OLD `:933` Pending stakeholder send | **Active**, but uses inline-HTML body construction (NOT NotificationTemplate). Per-recipient template branching: OfficeAdmin / Patient registered / AA-DA-CE registered / AA-DA-CE not registered (the last gets a `?__tenant=&email=` Register CTA). Falls back to W1-2 office+booker direct enqueue if resolver returns 0 recipients. |
| `StatusChangeEmailHandler` (Application layer) | `AppointmentStatusChangedEto` | `AppointmentDomain.SendEmail(Approved/Rejected, ...)` lines `:966, :980, :990` | **Active.** Approved -> 2 dispatches (stakeholders Ext + responsible-user Internal). Rejected -> 1 dispatch (stakeholders, NO CC matching OLD `:990`). InternalUserComments wrapped per OLD prefix conventions. **Gap:** OLD's `clinicStaffEmail` CC for Approved is not yet appended - tracked in section 6. |
| `AccessorInvitedEmailHandler` | `AppointmentAccessorInvitedEto` | `AppointmentAccessorDomain.SendEmailToAccessor` `:260` | **Active.** Plaintext-password security regression intentionally not replicated. Issues single-use reset token instead. |
| `DocumentUploadedEmailHandler` | `AppointmentDocumentUploadedEto` | `AppointmentDocumentDomain.SendDocumentEmail(... Uploaded)` `:297` AND `AppointmentNewDocumentDomain.SendDocumentEmail(... Pending)` `:242` | **Active.** Always dispatches `PatientDocumentUploaded` template (NOT `PatientNewDocumentUploaded` even for ad-hoc per the comment - intent was unification). Sends to uploader + responsible user. **Gap:** the `IsAdHoc`/`IsJointDeclaration` flag-based template branching the doc claims is not implemented - the code always uses `PatientDocumentUploaded`. |
| `DocumentAcceptedEmailHandler` | `AppointmentDocumentAcceptedEto` | `AppointmentDocumentDomain.SendDocumentEmail(... Accepted)` `:269` | **Active.** Always uses `PatientDocumentAccepted` template. Single recipient: original uploader (or booker/patient fallback if anonymous via verification code). |
| `DocumentRejectedEmailHandler` | `AppointmentDocumentRejectedEto` | `:286` | **Active.** Always uses `PatientDocumentRejected` template. RejectionNotes passed through verbatim. |
| `PackageDocumentQueueHandler` | `AppointmentApprovedEto` | `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail` `:564` (the row-creation half) | **Active.** Queues `AppointmentDocument` rows in Pending status from PackageDetail mapping. Does NOT send email - the email fires from `DocumentUploadedEmailHandler` when patient actually uploads. **Gap from OLD:** OLD sends ONE email with packet attachment to all stakeholders at queue time; NEW sends nothing here. |
| `PackageDocumentReminderEmailHandler` | `PackageDocumentReminderEto` | `SchedulerDomain.AppointmentPackageDocumentPendingNotification` `:146` AND `AppointmentDueDateDocumentApproaching` `:199` | **Wired but the Eto is never published.** No NEW recurring job that fires `PackageDocumentReminderEto`. Handler uses `AppointmentDocumentIncomplete` template (or `AppointmentDueDateUploadDocumentLeft` when `IsJointDeclaration=true`) - the doc-comment promises `PackageDocumentsReminder` and `JDFReminder` codes but those don't exist. **Documentation drift in the handler.** |
| `JdfAutoCancelledEmailHandler` | `AppointmentAutoCancelledEto` (filtered to `Reason == "JDF-not-uploaded"`) | `SchedulerDomain.AppointmentAutoCancelledNotification` `:257` | **Wired but the Eto is never published.** Uses `AppointmentCancelledDueDate` template. |
| `ChangeRequestSubmittedEmailHandler` | `AppointmentChangeRequestSubmittedEto` | `AppointmentChangeRequestDomain.SendEmailData(... rescheduleRequested=true)` `:767` | **Active.** Fires for both Cancel and Reschedule - OLD only fires for Reschedule (intentional NEW improvement). |
| `ChangeRequestApprovedEmailHandler` | `AppointmentChangeRequestApprovedEto` | `:730 (RescheduleApproved)` + `:747 (CancellationApproved)` | **Active.** **Gap:** Admin-override branch (OLD `:717` `PatientAppointmentRescheduleReqAdmin`) collapses into `AppointmentRescheduleRequestApproved` - 2 OLD templates conflated into 1 NEW. |
| `ChangeRequestRejectedEmailHandler` | `AppointmentChangeRequestRejectedEto` | `:740 (RescheduleRejected)` + cancellation-rejected (commented out in OLD `:749-755`) | **Active.** OLD's cancellation-rejected branch is commented out so NEW is more functional than OLD here. |

### 3.3 Etos (Distributed events)

11 Etos in `Domain.Shared/Notifications/Events/`. Each is a record carrying the minimum fields the matching handler needs:

- `UserRegisteredEto` - UserId, TenantId, Email, FirstName, LastName, RoleName
- `ExternalUserRegisteredEto` - duplicate? need to verify
- `AppointmentAccessorInvitedEto` - AppointmentId, InvitedUserId, Email, RoleName, TenantId
- `AppointmentAutoCancelledEto` - AppointmentId, Reason, TenantId
- `AppointmentChangeRequestSubmittedEto` - ChangeRequestId, AppointmentId, ChangeRequestType, TenantId
- `AppointmentChangeRequestApprovedEto` - same shape
- `AppointmentChangeRequestRejectedEto` - same shape + RejectionNotes
- `AppointmentDocumentUploadedEto` - AppointmentId, AppointmentDocumentId, UploadedByUserId, TenantId
- `AppointmentDocumentAcceptedEto` - AppointmentId, AppointmentDocumentId, TenantId
- `AppointmentDocumentRejectedEto` - same + RejectionNotes
- `PackageDocumentReminderEto` - AppointmentId, AppointmentDocumentId, IsJointDeclaration, TenantId

Plus Etos that already existed (not in the Notifications/Events folder):
- `AppointmentSubmittedEto` (in `Appointments/Events/`) - AppointmentId, TenantId, BookerUserId, PatientId, RequestConfirmationNumber, AppointmentDate, SubmittedAt
- `AppointmentStatusChangedEto` - AppointmentId, ToStatus, FromStatus, Reason, TenantId
- `AppointmentApprovedEto` - AppointmentId, AppointmentTypeId, PrimaryResponsibleUserId, TenantId, etc.

### 3.4 Seeded template bodies (real vs stub)

Counted from `Glob` of `src/.../Domain/NotificationTemplates/EmailBodies/*.html`:

**6 OLD-verbatim HTML bodies:**
- `UserRegistered.html`
- `PatientAppointmentApproveReject.html`
- `PatientAppointmentApprovedExt.html`
- `PatientAppointmentApprovedInternal.html`
- `PatientAppointmentPending.html`
- `PatientAppointmentRejected.html`

**53 codes carry stub bodies** (`<p>Stub body for {code}. Per-feature phases will replace with parity-correct content.</p>`). The seed contributor's `EmailBodyResources.TryLoadBody(code)` returns null for these, so the next session needs to:
1. Copy the OLD HTML from `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\EmailTemplates\<filename>.html`
2. Save under `src/.../Domain/NotificationTemplates/EmailBodies/<NewCodeName>.html` (filename = the C# enum constant)
3. Add the corresponding subject string to `EmailSubjects.ByCode`

---

## 4. Mapping table - OLD send -> NEW handler

| OLD source | OLD email | NEW handler | NEW template code | Parity status |
|---|---|---|---|---|
| `UserDomain.SendEmail(user, true)` | UserRegistered | `UserRegisteredEmailHandler` | `UserRegistered` | **Match** (typo fixed, ABP confirm-email token replaces VerificationCode GUID) |
| `UserAuthenticationDomain.PostLogin (unverified)` | UserRegistered (re-fired) | None | - | **Behavioral gap** - NEW does not re-send on every unverified login |
| `UserAuthenticationDomain.PostForgotPassword` | ResetPassword | None (ABP Account UI handles it) | `ResetPassword` (stub) | **Architectural divergence** - reset is via ABP Identity flow not dispatcher |
| `UserAuthenticationDomain.SendEmail (PasswordChange)` | PasswordChange | None | `PasswordChange` (stub) | **Gap** |
| `UserDomain.AddInternalUser` | AddInternalUser | None (ABP Identity Suite handles it) | `AddInternalUser` (stub) | Plaintext-password regression intentionally NOT replicated |
| `UserQueryDomain.Add (with appointment)` | UserQuery (to PrimaryResponsibleUser) | None | `UserQuery` (stub) | **Gap** - UserQuery feature deferred |
| `UserQueryDomain.Add (without appointment)` | UserQuery (to ItAdmin users) | None | `UserQuery` (stub) | **Gap** |
| `SchedulerDomain.AppointmentPendingDailyNotification` | PendingAppointmentDailyNotification | None | `PendingAppointmentDailyNotification` (stub) | **Gap** - no NEW recurring job |
| `SchedulerDomain.AppointmentApproveRejectInternalUserNotification` | AppointmentApproveRejectInternal | None | `AppointmentApproveRejectInternal` (stub) | **Gap** |
| `SchedulerDomain.AppointmentPackageDocumentPendingNotification` | UploadPendingDocuments | `PackageDocumentReminderEmailHandler` (Eto unpublished) | `AppointmentDocumentIncomplete` | **Gap** - handler exists but no scheduler publishes the Eto. Template wrong (uses Incomplete not UploadPendingDocuments). |
| `SchedulerDomain.AppointmentDueDateApproachingNotification` | AppointmentDueDateReminder | None | `AppointmentDueDateReminder` (stub) | **Gap** |
| `SchedulerDomain.AppointmentDueDateDocumentApproachingNotification` | AppointmentDocumentIncomplete | (handler reuses this template for a different trigger) | `AppointmentDocumentIncomplete` (stub) | **Template conflict** - same template used for two distinct OLD triggers |
| `SchedulerDomain.AppointmentJointDeclarationDocumentUploadNotification` | UploadPendingDocuments | None | `UploadPendingDocuments` (stub) | **Gap** |
| `SchedulerDomain.AppointmentAutoCancelledNotification` | AppointmentCancelledDueDate | `JdfAutoCancelledEmailHandler` (Eto unpublished, JDF-only filter) | `AppointmentCancelledDueDate` | **Partial gap** - generic auto-cancel reason unhandled |
| `SchedulerDomain.AppointmentPendingReminderStaffUsersNotification` | AppointmentPendingNextDay | None | `AppointmentPendingNextDay` (stub) | **OLD send commented out** - intentional in OLD; do not reactivate without confirming intent |
| `AppointmentDomain.SendEmail Pending stakeholder` | PatientAppointmentPending | `SubmissionEmailHandler` (inline HTML, NOT template) + `BookingSubmissionEmailHandler` (commented out) | `PatientAppointmentPending` (real body) | **Architectural divergence** - NEW uses per-recipient inline HTML branching; the template-driven dispatch is suppressed |
| `AppointmentDomain.SendEmail Pending staff blast` | PatientAppointmentApproveReject | `BookingSubmissionEmailHandler` | `PatientAppointmentApproveReject` (real body) | **Match** for external-user bookings; internal-user bookings skip the staff blast (matches OLD) |
| `AppointmentDomain.SendEmail Approved Internal` | PatientAppointmentApprovedInternal | `StatusChangeEmailHandler.DispatchApprovedAsync` (responsible-user leg) | `PatientAppointmentApprovedInternal` (real body) | **Match** but missing CC `clinicStaffEmail` |
| `AppointmentDomain.SendEmail Approved Ext` | PatientAppointmentApprovedExt | `StatusChangeEmailHandler.DispatchApprovedAsync` (stakeholder leg) | `PatientAppointmentApprovedExt` (real body) | **Match** but missing CC |
| `AppointmentDomain.SendEmail Rejected` | PatientAppointmentRejected | `StatusChangeEmailHandler.DispatchRejectedAsync` | `PatientAppointmentRejected` (real body) | **Match** (OLD `:990` has no CC, NEW correctly skips CC) |
| `AppointmentDomain.SendEmail CheckedIn` | PatientAppointmentCheckedIn | None | `PatientAppointmentCheckedIn` (stub) | **Gap** - Phase 5 deferred |
| `AppointmentDomain.SendEmail CheckedOut` | PatientAppointmentCheckedOut | None | (stub) | **Gap** |
| `AppointmentDomain.SendEmail NoShow` | PatientAppointmentNoShow | None | (stub) | **Gap** |
| `AppointmentDomain.SendEmail CancelledNoBill` | PatientAppointmentCancelledNoBill | None | (stub) | **Gap** |
| `AppointmentChangeRequestDomain SendEmailToClinicStaffForCancellation` | ClinicalStaffCancellation | None | (stub) | **Gap** |
| `AppointmentChangeRequestDomain isAdminReschedule` | PatientAppointmentRescheduleReqAdmin | (collapsed into `AppointmentRescheduleRequestApproved`) | `AppointmentRescheduleRequestApproved` | **Template conflation** |
| `AppointmentChangeRequestDomain isRescheduleRequestApproved` | PatientAppointmentRescheduleReqApproved | `ChangeRequestApprovedEmailHandler` | `AppointmentRescheduleRequestApproved` (DB-managed in OLD; stub body in NEW) | **Body migration needed** |
| `AppointmentChangeRequestDomain isRescheduleRequestRejected` | PatientAppointmentRescheduleReqRejected | `ChangeRequestRejectedEmailHandler` | `AppointmentRescheduleRequestRejected` (stub) | **Body migration needed** |
| `AppointmentChangeRequestDomain isCancellationRequestApprved` | PatientAppointmentCancellationApprvd | `ChangeRequestApprovedEmailHandler` | `AppointmentCancelledRequestApproved` (stub; typo in const fixed) | **Body migration needed** |
| `AppointmentChangeRequestDomain rescheduleRequested` | PatientAppointmentRescheduleReq | `ChangeRequestSubmittedEmailHandler` | `AppointmentRescheduleRequest` (stub) | **Body migration needed** |
| `AppointmentChangeRequestDomain SendEmailOfJointAggDocumentStatus Accepted` | JointAgreementLetterAccepted | None | `JointAgreementLetterAccepted` (stub) | **Gap** |
| `AppointmentChangeRequestDomain ... Uploaded` | JointAgreementLetterUploaded | None | `JointAgreementLetterUploaded` (stub) | **Gap** |
| `AppointmentChangeRequestDomain ... Rejected` | JointAgreementLetterRejected | None | `JointAgreementLetterRejected` (stub) | **Gap** |
| `AppointmentDocumentDomain SendDocumentEmail (package, Accepted)` | PatientDocumentAccepted | `DocumentAcceptedEmailHandler` | `PatientDocumentAccepted` (stub) | **Body migration needed** |
| `AppointmentDocumentDomain SendDocumentEmail (package, Rejected)` | PatientDocumentRejected | `DocumentRejectedEmailHandler` | `PatientDocumentRejected` (stub) | **Body migration needed** |
| `AppointmentDocumentDomain SendDocumentEmail (package, Uploaded)` | PatientDocumentUploaded | `DocumentUploadedEmailHandler` | `PatientDocumentUploaded` (stub) | **Body migration needed** |
| `AppointmentDocumentDomain SendDocumentEmail (NewDocument, Accepted)` | PatientNewDocumentAccepted | None (caller path conflated into `DocumentAcceptedEmailHandler` using Patient* template) | `PatientNewDocumentAccepted` (stub, unused) | **Behavioral gap** - NEW always uses Patient* template even for ad-hoc |
| `AppointmentDocumentDomain SendDocumentEmail (NewDocument, Rejected)` | PatientNewDocumentRejected | Same - conflated | `PatientNewDocumentRejected` (stub, unused) | Same |
| `AppointmentDocumentDomain SendDocumentEmail (NewDocument, Uploaded)` | PatientNewDocumentUploaded | Same | `PatientNewDocumentUploaded` (stub, unused) | Same |
| `AppointmentNewDocumentDomain SendDocumentEmail` | (uses Patient* templates instead of PatientNewDocument*) | (subsumed) | Patient* (stub) | OLD-bug or refactor leftover; NEW unification is the cleaner approach |
| `AppointmentJointDeclarationDomain SendDocumentEmail` | PatientDocument*Accepted/Rejected/AcceptedRemainingDocs/RejectedRemainingDocs | None directly | (stubs) | **Effectively unreachable in OLD** - method on interface but no caller |
| `AppointmentDocumentDomain SendSMTPMailWithAttachment x6` | AppointmentDocumentAddWithAttachment | **None** - see packet audit doc | `AppointmentDocumentAddWithAttachment` (stub) | **Major gap** - OLD's post-approval packet email to all stakeholders is unimplemented in NEW |
| `AppointmentAccessorDomain SendEmailToAccessor` | AccessorAppointmentBooked | `AccessorInvitedEmailHandler` | `AccessorAppointmentBooked` (stub body, real subject would help) | **Match** with security improvement |
| `AppointmentChangeLogDomain SendEmailForChangeLog` | AppointmentChangeLogs | None | `AppointmentChangeLogs` (stub) | **Gap** |
| `AppointmentChangeLogDomain SendEmailForAppointmentTimeChangedByAdmin` | AppointmentRescheduleRequestByAdmin | None | `AppointmentRescheduleRequestByAdmin` (stub) | **Gap** |

---

## 5. Phase 1 / 2 / 3 segmentation

Per Adrian 2026-05-06: only 3 email types are scope-active right now. Reasons given: (1) Azure ACS rate limits; (2) trigger / recipient ambiguity. **Documenting the deferred ones so they can be un-gated later.**

### Phase 1 (active right now - 3 email types)
1. **UserRegistered** - active via `UserRegisteredEmailHandler` + `UserRegisteredEto`. OLD-verbatim body shipped.
2. **PatientAppointmentPending / Booking submission** - active via Domain `SubmissionEmailHandler` (inline HTML, role-branched). NOT using the seeded template body.
3. **AppointmentApproved / Rejected** - active via `StatusChangeEmailHandler`. OLD-verbatim bodies shipped for both branches + responsible-user split.

### Phase 2 (gated off; un-gate when ACS limits + trigger ambiguity resolve)
Existing wired handlers waiting for their Eto to be published:

- `PackageDocumentReminderEmailHandler` (needs a Hangfire recurring job)
- `JdfAutoCancelledEmailHandler` (needs a Hangfire recurring job that publishes `AppointmentAutoCancelledEto` with `Reason="JDF-not-uploaded"`)
- `ChangeRequestSubmittedEmailHandler` / `ChangeRequestApprovedEmailHandler` / `ChangeRequestRejectedEmailHandler` (need the AppService methods that publish their Etos to be called)
- `BookingSubmissionEmailHandler` Pending dispatch (uncomment once duplicate-with-Domain-handler resolved)

### Phase 3 (no NEW handler exists yet)
Has to be built from scratch:

- ResetPassword + PasswordChange (decide: use ABP Account UI or pipe through dispatcher)
- AddInternalUser (decide: use ABP Identity Suite UI or pipe through dispatcher)
- UserQuery (deferred feature)
- All 9 SchedulerDomain reminder methods - need a NEW Hangfire / Coravel scheduler decision
- AppointmentDomain CheckedIn / CheckedOut / NoShow / CancelledNoBill (Phase 5 lifecycle deferred)
- ClinicalStaffCancellation
- PatientAppointmentRescheduleReqAdmin (admin-override branch)
- JointAgreementLetterAccepted / Uploaded / Rejected (separate from `AppointmentChangeRequestDomain` JDF status flow - investigate caller)
- AppointmentChangeLogs + AppointmentRescheduleRequestByAdmin (audit-log feature deferred)
- The post-approval **packet attachment email** (`AppointmentDocumentAddWithAttachment` template, OLD `:491, :683, :731, :777, :791, :838, :851`) - covered in the packet audit doc

---

## 6. Concrete gaps that need closure

### G1. CC behavior parity
**OLD (`:933, :966, :980`):** CC `clinicStaffEmail` on Pending-stakeholder, Approved-Internal, Approved-Ext.
**NEW:** `BookingSubmissionEmailHandler.AppendCcRecipientsAsync` correctly appends `SystemParameter.CcEmailIds` for Pending - **but the Pending stakeholder dispatch is commented out**. `StatusChangeEmailHandler` does NOT append CC for Approved.
**Action:** Lift `AppendCcRecipientsAsync` into a shared helper. Call it from `StatusChangeEmailHandler.DispatchApprovedAsync` for both Internal and Ext branches. Also un-comment the Pending dispatch in `BookingSubmissionEmailHandler` once Adrian decides whether the Domain `SubmissionEmailHandler` is replaced or kept.

### G2. Two-handler conflict on AppointmentSubmittedEto
The Domain `SubmissionEmailHandler` (inline HTML, per-recipient branching with login/register CTAs) and the Application `BookingSubmissionEmailHandler` (template-driven, OLD-parity Pending body) BOTH subscribe to `AppointmentSubmittedEto`. Adrian commented out the latter's Pending dispatch on 2026-05-07 (B15-followup) to prevent duplicate emails. Decision needed: keep the inline-HTML branching (modern, role-aware CTAs) OR switch to OLD-verbatim template (faithful parity). Currently: half-modern (Domain handler for stakeholders, Application handler for staff blast). Document the decision.

### G3. Ad-hoc vs package vs JDF document branching
NEW `AppointmentDocument` unifies 3 OLD tables via `IsAdHoc` + `IsJointDeclaration` flags. Current document handlers (`DocumentUploadedEmailHandler` etc.) ignore the flags and always use `PatientDocument*` templates. OLD has separate `PatientNewDocument*` and `JointAgreementLetter*` templates. **Action:** wire the flag-based branching the doc-comments promise but the code doesn't implement.

### G4. `PatientAppointmentRescheduleReqAdmin` template missing
NEW `ChangeRequestApprovedEmailHandler` uses `AppointmentRescheduleRequestApproved` for both standard and admin-override paths. OLD has TWO templates - the admin-override path uses `PatientAppointmentRescheduleReqAdmin` with `##AdminReScheduleReason##` variable. **Action:** branch on a new field in `AppointmentChangeRequestApprovedEto` (e.g. `IsAdminOverride`).

### G5. Reminder + scheduler infrastructure missing
OLD has 9 SchedulerDomain methods triggered by stored procs. NEW has 0 background scheduler integration for emails. Handlers exist for some (`PackageDocumentReminderEmailHandler`, `JdfAutoCancelledEmailHandler`) but no job ever publishes the Etos. **Action:** Decide between Hangfire `RecurringJob.AddOrUpdate(...)` OR Coravel OR pure Quartz. Implement 7 active reminder paths (skip the 2 commented out in OLD).

### G6. Body migration for 53 stub templates
6/59 templates have OLD-verbatim bodies. The other 53 have stub strings. **Action:** for each in-scope code, copy the OLD HTML from `wwwroot/EmailTemplates/<filename>.html` into `src/.../Domain/NotificationTemplates/EmailBodies/<NewCodeName>.html` and add the OLD-verbatim subject string to `EmailSubjects.ByCode`. Re-seed the tenant rows.

### G7. Verification subject typo
OLD subject `"Your have registered successfully - Patient Appointment portal"` - typo "Your" -> "You". NEW fixed in `EmailSubjects.UserRegistered` per "Clear bug - fix it" rule. Document so reviewers don't re-introduce.

### G8. Bracketed subject identity construction
OLD `:921` builds `(Patient: X Y - Claim: A - ADJ: B)` inline at every send. NEW expresses via `##EmailSubjectIdentity##` token in the subject template. **Action:** dispatcher must populate this variable per-recipient before substitution. `EmailSubjectBuilder` exists - confirm it's wired by every handler that uses an OLD subject containing the bracketed prefix.

### G9. Verify-email-on-every-unverified-login behavior
OLD `UserAuthenticationDomain.PostLogin:138-144` re-fires the registration email every time an unverified user attempts to log in. NEW relies on ABP's `RequireConfirmedEmail` gate which simply blocks login. Decide: replicate OLD's "we keep emailing them the link" behavior OR keep NEW's clean ABP behavior. Document the decision.

### G10. Failure semantics
OLD `SendSMTPMail` is `async void` with all exceptions silently swallowed (logging is commented out at L252-264). NEW `NotificationDispatcher` lets exceptions propagate (so the UoW rolls back and the gap surfaces in tests). This is an intentional NEW improvement; document so reviewers don't "fix" it back to silent failure.

### G11. Hardcoded developer emails in OLD
`SchedulerDomain:334` hardcodes `devendra.lohar@radixweb.com;karnavi.soni@radixweb.com` as test recipients (in commented-out code). `:769` (`AppointmentChangeRequestDomain`) similarly mentions `surbhi.acharya@radixweb.com,hardikgiri.goswami@radixweb.com` (also commented out). NEW does not replicate these - good. Document that these are OLD developer artifacts not production behavior.

---

## 7. Implementation contract for the next session

### 7.1 Order of work (recommended)

1. **G6 body migration for the 5 already-wired demo-critical handlers' status-cascade** - copy `Patient-Appointment-CheckedIn.html`, `CheckedOut.html`, `NoShow.html`, `CancelledNoBill.html`, `ClinicalStaffCancellation.html` into the EmailBodies folder + add subjects. Wire `StatusChangeEmailHandler` to handle the additional statuses. Cost: ~1 day.
2. **G6 body migration for change-request templates** - 6 codes (`AppointmentCancelledRequest`, `AppointmentCancelledRequestApproved`, `AppointmentCancelledRequestRejected`, `AppointmentRescheduleRequest`, `AppointmentRescheduleRequestApproved`, `AppointmentRescheduleRequestRejected`). Cost: ~1 day.
3. **G6 body migration for document templates** - 6 codes (`PatientDocument*` x3 + `PatientNewDocument*` x3) + the 3 `JointAgreementLetter*` codes. Cost: ~1 day.
4. **G3 flag-based template branching** - in `DocumentUploadedEmailHandler` etc., branch on `IsAdHoc` / `IsJointDeclaration` to pick the right template. Cost: ~half-day.
5. **G1 CC behavior** - lift `AppendCcRecipientsAsync` into a shared helper. Add to `StatusChangeEmailHandler`. Cost: ~half-day.
6. **G2 architectural decision** - which `AppointmentSubmittedEto` handler is canonical? Resolve. Cost: ~half-day.
7. **G4 admin-override template** - extend `AppointmentChangeRequestApprovedEto` with `IsAdminOverride`, branch in handler. Cost: ~half-day.
8. **G5 reminder scheduler** - decide framework, wire 7 active reminder paths. Cost: ~3 days (significant).
9. **Phase 3 from-scratch handlers** - ResetPassword + PasswordChange + UserQuery + ChangeLogs etc. Cost: variable.

### 7.2 Validation criteria per handler (next-session checklist)

For each handler the next session ships, verify:
- [ ] OLD source line cited as a doc comment
- [ ] Subject string in `EmailSubjects.ByCode` (or doc the deviation)
- [ ] Body HTML in `EmailBodies/<code>.html` (or doc the inline-HTML reason)
- [ ] All variables the OLD vEmailSenderViewModel populates are in the NEW handler's variables dict
- [ ] CC behavior matches OLD per-status (sections 2.5/2.6)
- [ ] Recipient resolution uses `IAppointmentRecipientResolver` for stakeholder fan-out (not ad-hoc queries)
- [ ] Tenant context wrapped via `_currentTenant.Change(eventData.TenantId, tenantName)` (2-arg overload, per the Wave-3 #17.4 fix - `tenantName` resolution via `ITenantStore.FindAsync`)
- [ ] Failure mode: exception propagates (matches NEW pattern, not OLD's silent swallow)
- [ ] Unit test that asserts the dispatcher was called with the expected (templateCode, recipients, variables) tuple
- [ ] Integration test that runs the AppService method and asserts a Hangfire job was enqueued

### 7.3 Commands to verify in dev stack

```bash
# Health check
curl -s http://localhost:44327/health-status                             # API
curl -s http://localhost:44368/.well-known/openid-configuration          # AuthServer
curl -s http://localhost:4200/                                           # Angular SPA (currently broken - dev-entrypoint.sh missing)
curl -s http://localhost:4202/                                           # OLD app

# Hangfire dashboard (the SendAppointmentEmailJob queue)
open http://localhost:44327/hangfire
```

---

## 8. Open questions for Adrian

1. **G2 / Section 5 Phase 1.2:** Keep the modern role-aware inline-HTML SubmissionEmailHandler OR switch to OLD-verbatim template? The two are mutually exclusive.
2. **G9:** Replicate OLD's "re-fire UserRegistered on every unverified login" behavior, or keep NEW's clean ABP `RequireConfirmedEmail` gate?
3. **G5 framework decision:** Hangfire RecurringJob, Coravel, or pure Quartz for the 7 active reminder paths?
4. **G3 template choice:** When `IsAdHoc=true`, use `PatientDocument*` (unified) OR `PatientNewDocument*` (OLD-verbatim per-table)? The latter requires authoring the bodies separately.
5. **OLD bug `RejectionNotes` on CheckedIn/CheckedOut emails:** the NoShow/CheckedIn/CheckedOut body `.html` files probably reference `##RejectionNotes##` from copy-paste. Render as empty string OR omit the variable entirely?
6. **AppointmentJointDeclarationDomain.SendDocumentEmail:** OLD has it on the public interface but no caller invokes it. Replicate the dead method in NEW (forward-compat) or skip?
7. **G11:** The hardcoded developer emails are commented out in OLD - NEW should not replicate. Confirm.

---

## 9. Errata against the prior wave-1 audit

The prior `wave-1-parity/email-coverage-audit.md` (dated 2026-05-05) is mostly correct on the high-level architecture but contains these errors:
- "**Total OLD emails: 59 template codes**" - true on PAPER (16 + 43), but in practice only 36 are actually invoked. The 14 extra HTML files on disk are dead.
- "**SystemParameter CC list auto-added to all outbound mail**" - WRONG. CC is appended only by the AWS-SES dead-code path, NOT by the SMTP path that all callers actually use. CC is appended only when callers pass `mailCC` explicitly.
- "**SignatureSize 0.6 in x 0.6 in**" (in the packet audit, but related): the `InsertAPicture` call uses `880000L EMU` = 0.962 inches, not 0.6.
- The "EmailTemplate static class -- 43 entries" was correct, but the audit said this matches 43 HTML files; in fact 57 HTML files exist (1 wrapper + 13 unreferenced + 43 referenced). The 14 unreferenced files are: `Appointment-Cancellation-Request-Accepted.html`, `Appointment-Cancellation-Request-Booked.html`, `Appointment-Cancellation-Request-Rejected.html`, `Appointment-Changes-Notify-User.html`, `Appointment-Documemt-Rejected.html` (filename typo), `Appointment-Join-Declaration-Document-Rejected.html`, `Appointment-Notify-Accessor.html`, `Appointment-Query.html`, `Appointment-Request-Approved.html`, `Appointment-Request-Booked.html`, `Appointment-Request-Cancelled.html`, `Appointment-Request-Rejected.html`, `Appointment-Reschedule-Request-Booked.html`, `CommonTemplate.html` (wrapper).

---

## 10. Source pointers

### OLD
- `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\SendMail.cs` - transport
- `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs:212-251` - `GetEmailTemplateFromHTML` reflection loop
- `P:\PatientPortalOld\PatientAppointment.Models\Enums\TemplateCode.cs` - 16 DB-managed codes
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs:26-71` - 43 disk-HTML codes
- `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\EmailTemplates\` - 57 HTML files
- `P:\PatientPortalOld\PatientAppointment.Api\server-settings.json` - `clinicStaffEmail`, branding tokens, the 89-token `documentMergeKeys.keys` list
- All Domain classes listed in section 2

### NEW
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs` - 59 codes
- `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/` - entity + repo + seed contributor + EmailBodyResources + EmailSubjects + 6 OLD-verbatim HTML bodies
- `src/HealthcareSupport.CaseEvaluation.Domain/Notifications/TemplateVariableSubstitutor.cs` - the `##Var##` replacer
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/AppointmentRecipientResolver.cs` - 7-source stakeholder walker
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Handlers/SubmissionEmailHandler.cs` - active stakeholder dispatch (inline HTML)
- `src/HealthcareSupport.CaseEvaluation.Application/Notifications/` - dispatcher + renderer + 11 handlers
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Notifications/Events/` - 11 Etos
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:85-97` - NullEmailSender swap gate
- Hangfire dashboard at `http://localhost:44327/hangfire`
- Tenant `SystemParameter.CcEmailIds` row (per-tenant)

---

## 11. Pass 2 corrections (2026-05-08) - read this section first

Pass 1 missed an entire AuthServer-side email path, mis-claimed handler liveness, missed 5 Hangfire jobs, mis-claimed body fidelity, and never opened the Angular SPA. This section is the corrected ground truth for those areas.

### 11.1 NEW has TWO parallel email pipelines, not one

Pass 1 documented only the `INotificationDispatcher` pipeline. NEW also runs an AuthServer-side pipeline:

**Pipeline A (AuthServer-side, `IAccountEmailer` override):**
- File: `src/HealthcareSupport.CaseEvaluation.AuthServer/Emailing/CaseEvaluationAccountEmailer.cs` (verified 2026-05-08, 290 lines).
- Registered via `[Dependency(ReplaceServices = true)] [ExposeServices(typeof(IAccountEmailer))]` so ABP's stock `Volo.Abp.Account.Emailing.AccountEmailer` is replaced.
- **Triggers (4 surfaces):**
  1. `SendEmailConfirmationLinkAsync(IdentityUser, token, ...)` -- fires from ABP `AccountAppService` whenever the verify-email link is sent (initial registration AND any "resend verification" click). Renders `NotificationTemplateConsts.Codes.UserRegistered` template, dispatches via `IBackgroundJobManager.EnqueueAsync(SendAppointmentEmailArgs)`.
  2. `SendPasswordResetLinkAsync(IdentityUser, token, ...)` -- fires from ABP forgot-password flow. Renders `Codes.ResetPassword` template.
  3. `SendEmailSecurityCodeAsync(IdentityUser, code)` -- email-based 2FA code. Reuses `Codes.UserRegistered` template (the URL slot carries the code). Not on demo critical-path (2FA off by default).
  4. `SendEmailConfirmationCodeAsync(emailAddress, code)` -- code-based pre-registration confirmation. Reuses `Codes.UserRegistered`. Not on demo critical-path.
- **Why the override exists** (verbatim from `CaseEvaluationAccountEmailer.cs:26-35`): ABP's stock `AccountEmailer` uses Scriban-backed `ITemplateRenderer`, but our build pins Scriban to 7.1.0 to clear NuGetAudit CVEs, which is binary-incompatible with ABP 10.0.2's expected Scriban 6.x `ParserOptions` layout. The override sidesteps Scriban entirely by rendering through `INotificationTemplateRepository` + `TemplateVariableSubstitutor`. Single template engine for the whole app.
- **URL construction:** verify links go to SPA `{base}/account/email-confirmation?userId={guid}&confirmationToken={url-encoded-token}`. Reset links go to SPA `{base}/account/reset-password?userId={guid}&resetToken={url-encoded-token}`. Base URL from per-tenant `Notifications.PortalBaseUrl` setting (default `http://falkinstein.localhost:4200`).

**Pipeline B (Domain-side, Eto handlers):**
- 13 handlers in `Application/Notifications/Handlers/` + `Domain/Appointments/Handlers/SubmissionEmailHandler.cs` (the Pass 1 "12 handlers" count was off by one because I missed one of these).
- These subscribe to `ILocalEventBus` Etos published by AppServices.

**The two pipelines share infrastructure** (`INotificationTemplateRepository`, `TemplateVariableSubstitutor`, `IBackgroundJobManager`, `SendAppointmentEmailJob`) but route through different triggers. Both end at the same Hangfire `SendAppointmentEmailJob` queue.

### 11.2 `UserRegisteredEmailHandler` is dead code

Verified 2026-05-08 by `grep -rnE 'PublishAsync.*UserRegisteredEto|new UserRegisteredEto'` across `src/`: **zero matches in source code.** Two comments at `ExternalSignupAppService.cs:53` ("R1 follow-up: publish UserRegisteredEto so the notification handler can send OLD's verify-your-email message") and `:554` ("publishing the UserRegisteredEto") describe an intended publish. **Neither exists.** `_localEventBus` is injected (L55, L84) but the publish call was never written.

This means `UserRegisteredEmailHandler.HandleEventAsync` never fires. The `UserRegisteredEto` type is defined but unused.

**Why the verification email still works for users in NEW:** the email goes out through Pipeline A (`CaseEvaluationAccountEmailer.SendEmailConfirmationLinkAsync`), which ABP's `AccountAppService` calls during registration AND any resend-verification click on `/Account/ConfirmUser`. Adrian's 2026-05-06 directive deferred the email to user-clicks-Verify (not auto-fire on register submit). That gating is implemented in `AccountAppService` and the SPA, not via the Eto handler.

**Pass 1 errata correction:** Section 3.2 row `UserRegisteredEmailHandler` listed as "**Active.** Subject typo fixed. Verify URL points at SPA `/account/email-confirmation`. Strict-role match." The first claim is wrong - the handler is wired but unreachable. The URL + role-match logic is correct but never executes. Ignore the row when planning.

**Implication for the next session:** decide one of (a) delete the dead handler + Eto, or (b) wire a publish in `ExternalSignupAppService.RegisterAsync` (or in a new `ResendVerificationAsync` method) and remove the AuthServer override. Currently both paths are present but only Pipeline A reaches users.

### 11.3 5 Hangfire RecurringJobs are registered

Pass 1 Section 6 G5 stated "no NEW recurring job that publishes the Eto" + "0 background scheduler integration for emails" + recommended a framework decision (Hangfire / Coravel / Quartz). **All wrong.** Verified 2026-05-08 by reading `CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs` (L729-771).

Five `Hangfire.RecurringJob.AddOrUpdate` registrations, all with `RecurringJobOptions { TimeZone = America/Los_Angeles }`:

| Job class | RecurringJobId | Cron | Source line |
|---|---|---|---|
| `RequestSchedulingReminderJob` | `appt-request-scheduling-reminder` | `0 8 * * *` (08:00 PT daily) | `Domain/Appointments/Notifications/Jobs/RequestSchedulingReminderJob.cs:55-56` |
| `CancellationRescheduleReminderJob` | `appt-cancellation-reschedule-reminder` | `0 8 * * *` | `Domain/Appointments/Notifications/Jobs/CancellationRescheduleReminderJob.cs:63-64` |
| `AppointmentDayReminderJob` | `appt-day-reminder` | `0 7 * * *` (07:00 PT) | `Domain/Appointments/Notifications/Jobs/AppointmentDayReminderJob.cs:53-54` |
| `JointDeclarationAutoCancelJob` | `appt-jdf-auto-cancel` | `0 6 * * *` (06:00 PT) | `Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs:47-48` |
| `PackageDocumentReminderJob` | `appt-package-doc-reminder` | `30 8 * * *` (08:30 PT) | `Domain/Notifications/Jobs/PackageDocumentReminderJob.cs:44-45` |

**Key behavioral differences from OLD:**

- **`RequestSchedulingReminderJob`** is CCR Title 8 Sec. 31.5-driven: Pending appointments at exactly 30 / 60 / 75 / 85 / 90 elapsed days from `CreationTime`. Inline-HTML body, NOT template-driven. OLD's equivalent was `SchedulerDomain.PendingAppointmentDailyNotification` which sent a single daily digest with no day-windowed targeting.
- **`CancellationRescheduleReminderJob`** is CCR Sec. 34(e)-driven: appointments in `CancellationRequested` / `RescheduleRequested` / `CancelledLate` at 45 / 55 elapsed days from `LastModificationTime`. Inline HTML. OLD has no equivalent - this is a NEW feature for legal compliance.
- **`AppointmentDayReminderJob`** fires for Approved at T-7 and T-1 days. Inline HTML. OLD's `AppointmentDueDateApproachingNotification` fires only ONE window.
- **`JointDeclarationAutoCancelJob`** at 06:00 PT: scans Approved AME appointments without an uploaded JDF (and DueDate at-or-past `SystemParameter.JointDeclarationUploadCutoffDays`). Sets status to `CancelledNoBill` directly + publishes BOTH `AppointmentStatusChangedEto` (which triggers SlotCascadeHandler) AND `AppointmentAutoCancelledEto` with `Reason="JDF-not-uploaded"`. Per-row try/catch so one failure doesn't block the rest. **OLD-bug-fix:** OLD `SchedulerDomain.AppointmentAutoCancelledNotification` fires the email but does NOT actually transition the appointment status - NEW does the status change too.
- **`PackageDocumentReminderJob`** at 08:30 PT: scans Approved appointments with DueDate at-or-past `Documents.PackageDocumentReminderDays` cutoff, finds outstanding non-ad-hoc package documents in Pending or Rejected status, publishes one `PackageDocumentReminderEto` per row. Single-cadence, not multi-cadence (T-7/T-3/T-1) per the source comment.

**Pass 1 errata correction:** Sections 4 (mapping table) and 6 (G5) claimed reminder handlers exist but Etos are not published. **Wrong.** All 5 jobs exist, are registered, and publish their respective Etos / direct-enqueue. The 3 inline-HTML reminder jobs (RequestScheduling, CancellationReschedule, AppointmentDay) bypass the `INotificationDispatcher` template path entirely; the 2 Eto-publishing jobs (JdfAutoCancel, PackageDocumentReminder) feed the template-driven handlers.

**Real gap for next session:** the 3 inline-HTML reminder jobs should be migrated to the dispatcher + per-tenant template path so subjects/bodies are IT-Admin editable. Body migration is the work, NOT scheduler infrastructure.

### 11.4 NEW HTML bodies are SIMPLIFIED, not OLD-verbatim

Pass 1 Section 1 + 3.4 said "6 OLD-verbatim HTML bodies" + the seed contributor's comment also says "OLD-verbatim". **Wrong.** Verified 2026-05-08 line-by-line read of all 6 NEW EmailBodies + their OLD equivalents.

| Code | NEW lines | OLD lines | Verdict |
|---|---|---|---|
| `UserRegistered.html` | 11 | 78 | NEW is plain `<p>Hello, ##PatientFirstName##</p><p>You have successfully registered to our system.</p><p><a href="##URL##">Click here to verify.</a></p>`. **No `##CompanyLogo##`, no `##lblHeaderTitle##`, no footer.** |
| `PatientAppointmentApprovedExt.html` | 11 | (TBD) | Same simplified pattern (verified by line count). |
| `PatientAppointmentApprovedInternal.html` | 11 | (TBD) | Same. |
| `PatientAppointmentApproveReject.html` | 11 | (TBD) | Same. |
| `PatientAppointmentPending.html` | 11 | (TBD) | Same. |
| `PatientAppointmentRejected.html` | 11 | (TBD) | Same. |

OLD's `User-Registed.html` (78 lines) is a 3-section table layout: a dark-blue header table with `##CompanyLogo##` image + `##lblHeaderTitle##` text, a body table with the greeting + body + verify link, and a green footer table with `##lblFooterText##` + `##Email##` + `##Skype##` + `##ph_US##` + `##imageInByte##` image. NEW strips all of that.

**Why this is intentional today:** the handlers (`StatusChangeEmailHandler.AddBrandPlaceholders`, `BookingSubmissionEmailHandler.AddBrandPlaceholders`, `UserRegisteredEmailHandler.AddBrandPlaceholders`) emit empty strings for all 8 brand tokens because per-tenant branding is not yet wired. Even if the full OLD HTML were copied, the rendered email would still have empty src/text in those positions (broken `<img src="">` references and blank header text), looking worse than the simplified version.

**Real gap for next session:** Adrian flagged emailing as "the most issue" - if visual fidelity to OLD is the goal, the path is (a) wire per-tenant branding (CompanyLogo blob, header title, footer contact info) THEN (b) replace the simplified bodies with OLD-verbatim HTML. Doing only (b) without (a) renders broken images.

### 11.5 NEW Angular has packet-download UI (Pass 1 missed this)

Pass 1 Section 1 row "Delivery" said "REST download via `AppointmentPacketsAppService.DownloadAsync`. **No email-with-attachment flow exists.**" The "No email" half is correct. The "REST download" half missed the actual UI: `angular/src/app/appointment-packet/appointment-packet.component.ts` (176 lines) + matching `.html` template. Verified 2026-05-08:

- `AppointmentPacketComponent` standalone Angular 20 component, takes `@Input() appointmentId` and shows status + Download + Regenerate buttons.
- Polls `AppointmentPacketService.getByAppointment(id)` every 5 seconds while `Status == Generating` so the office sees Failed/Generated transitions without manual refresh.
- Download routes through `AppointmentDocumentUrls.buildDownload(...)` helper which `window.open()`s the absolute API URL `{api}/api/app/appointment-packets/{id}/download` (per prior research at `docs/research/proxy-regen-doc-flow-fix.md`).
- Regenerate button gated on `CaseEvaluation.AppointmentPackets.Regenerate` permission.
- Toast notifications via `@abp/ng.theme.shared` ToasterService.

**No email-with-attachment UI** because OLD's email-with-attachment is the OFFICE staff fan-out to stakeholders, not a user-facing button. NEW has not yet built the email-with-attachment send (covered in companion `document-packets.md` audit P3).

**Pass 1 errata correction:** AppointmentPacketComponent already exists and is wired into the appointment view. Don't re-build it. Extend it for `PacketKind` once the multi-kind schema lands.

### 11.6 Eto publish sites - exact mapping

Pass 1 mapped Eto -> handler but didn't enumerate publish sites. Verified 2026-05-08 via `grep -rnE 'PublishAsync.*Eto'`:

| Eto | Publish line(s) | Trigger |
|---|---|---|
| `AppointmentSubmittedEto` | `Application/Appointments/AppointmentsAppService.cs:786` | `CreateAppointmentInternalAsync` after commit |
| `AppointmentStatusChangedEto` | `Application/Appointments/AppointmentsAppService.cs:538` (DeleteAsync), `:772` (CreateAppointmentInternalAsync), `:936` (UpdateAsync); `Domain/Appointments/AppointmentManager.cs:186` (TransitionAsync); `Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs:146, :290, :300, :372` (cancel/reschedule approval paths); `Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs:169` (auto-cancel) | Many - any status change |
| `AppointmentApprovedEto` | `Application/Appointments/AppointmentsAppService.Approval.cs:127` | `ApproveAppointmentAsync` |
| `AppointmentRejectedEto` | `Application/Appointments/AppointmentsAppService.Approval.cs:162` | `RejectAppointmentAsync` |
| `AppointmentAccessorInvitedEto` | `Domain/AppointmentAccessors/AppointmentAccessorManager.cs:150` | `CreateOrLinkAsync` |
| `AppointmentChangeRequestSubmittedEto` | `Domain/AppointmentChangeRequests/AppointmentChangeRequestManager.cs:150` (cancel), `:266` (reschedule) | Submit path |
| `AppointmentChangeRequestApprovedEto` | `Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs:157` (cancel-approve), `:319` (reschedule-approve) | Approve path |
| `AppointmentChangeRequestRejectedEto` | `Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs:202` (cancel-reject), `:388` (reschedule-reject) | Reject path |
| `AppointmentDocumentUploadedEto` | `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:144, :188, :262, :304` | 4 upload paths (package / ad-hoc / JDF / verification-code anonymous) |
| `AppointmentDocumentAcceptedEto` | `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:448` | `ApproveAsync` |
| `AppointmentDocumentRejectedEto` | `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:477` | `RejectAsync` |
| `AppointmentAutoCancelledEto` | `Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs:179` | JDF auto-cancel job |
| `PackageDocumentReminderEto` | `Domain/Notifications/Jobs/PackageDocumentReminderJob.cs:147` | Daily reminder job |
| `UserRegisteredEto` | **NONE - dead** | (see 11.2) |
| `ExternalUserRegisteredEto` | **NONE - dead** | (defined Phase 18, never published) |

**`StatusChangeEmailHandler` re-fan-out trap:** `AppointmentStatusChangedEto` is published from 7+ sites (incl. cancel/reschedule approval paths in `AppointmentChangeRequestsAppService.Approval.cs`). The handler filters to Approved + Rejected only (verified at `StatusChangeEmailHandler.cs:98-102`), so cancel/reschedule status flips won't double-fire emails alongside `ChangeRequestApprovedEmailHandler` / `ChangeRequestRejectedEmailHandler`. But this safety relies on the handler's status filter - any future change to broaden the handler must re-check for double-fire.

### 11.7 OLD `ApplicationUtility.ReplaceTextOfWordDocument` - the fragile renderer

Pass 1 cited the method but did not read it. Verified 2026-05-08 read of `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs:327-380`:

```csharp
public void ReplaceTextOfWordDocument(Dictionary<string, string> keyValuePairs, String documentPath)
{
    string fullFilePath = Path.Combine(Directory.GetCurrentDirectory(), documentPath);
    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(fullFilePath, true))
    {
        SimplifyMarkupSettings settings = new SimplifyMarkupSettings { ... };  // 16 aggressive flags below
        MarkupSimplifier.SimplifyMarkup(wordDoc, settings);    // PowerTools for OpenXml call

        string docText = null;
        using (StreamReader sr = new StreamReader(wordDoc.MainDocumentPart.GetStream()))
            docText = sr.ReadToEnd();    // Reads MainDocumentPart XML as plain text

        for (int i = 0; i < keyValuePairs.Keys.Count; i++)
        {
            var key = ...; var value = keyValuePairs[key];
            if (value.Contains('&'))      // BUG: mutates the source dict
                keyValuePairs[key] = value.Replace('&', ' ');
            docText = docText.Replace(key, keyValuePairs[key]);
        }

        using (StreamWriter sw = new StreamWriter(wordDoc.MainDocumentPart.GetStream(FileMode.Create)))
            sw.Write(docText);
    }
}
```

`SimplifyMarkupSettings` flags (16 active): `NormalizeXml=false, AcceptRevisions=true, RemoveBookmarks=true, RemoveComments=true, RemoveGoBackBookmark=true, RemoveWebHidden=true, RemoveContentControls=true, RemoveEndAndFootNotes=true, RemoveFieldCodes=true, RemoveLastRenderedPageBreak=true, RemovePermissions=true, RemoveProof=true, RemoveRsidInfo=true, RemoveSmartTags=true, RemoveSoftHyphens=true, ReplaceTabsWithSpaces=true`.

**Behavioral notes for the next-session implementer:**

- `MarkupSimplifier.SimplifyMarkup` is the critical step. Without it, Word's XML splits a token like `##Patients.FirstName##` across multiple `<w:r>`/`<w:t>` runs whenever Word inserts proofErr elements, smart-tags, content-controls, or revision marks - and a flat `Replace` would miss them. The simplifier collapses the markup so tokens become contiguous text.
- The `value.Contains('&')` branch silently strips ampersands by replacing them with spaces. This is to avoid breaking XML (where `&` must be `&amp;`) but it MUTATES the input dictionary. NEW must NOT replicate this bug. Either HTML-escape the value (`& -> &amp;`) or strip silently with a comment.
- **The implementation treats XML as a plain string.** If a token VALUE contains XML-significant characters (`<`, `>`, `&`, `"`, `'`), the resulting docx will have invalid XML and Word will fail to open it.
- Field codes are removed (`RemoveFieldCodes=true`). Templates that use Word's mail-merge fields lose them. Tokens must be plain text in the source DOCX.
- Bookmarks, content controls, end notes, and footnotes are all removed. Templates relying on those features must not use them.

**Implication for NEW packet renderer:** the next session needs to re-implement this method, but with proper XML handling. Use `WordprocessingDocument` + walk every `<w:t>` element + replace tokens at the run level (the right way), OR keep the OLD's simplifier-then-string approach but HTML-escape values. Recommend the former.

### 11.8 OLD's `GetNotificaionEmailAndPhoneNumers` (the stakeholder SP wrapper)

Verified 2026-05-08, `ApplicationUtility.cs:319-325`:

```csharp
public string GetNotificaionEmailAndPhoneNumers(int appointmentId)
{
    var spParameters = new object[1];
    spParameters[0] = new SqlParameter() { ParameterName = "AppointmentId", Value = appointmentId };
    var storeProcSearchResult = MainDbcontextManager.SqlQueryAsync<StoreProcSearchViewModel>(
        "EXEC spm.spAppointmentStackHoldersEmailAndPhone @AppointmentId", spParameters).Result;
    return storeProcSearchResult.SingleOrDefault()?.Result;
}
```

The stored proc `spm.spAppointmentStackHoldersEmailAndPhone` is the source of truth for OLD's stakeholder fan-out. Returns a JSON-serialized `[{ EmailList, PhoneList, PrimaryEmailList }]` array (deserialized into `AppointmentStackHoldersEmailPhone` objects in domain code, with `.FirstOrDefault()` taken). The proc body lives in the SQL database, NOT in the OLD repo source - so the exact stakeholder query is not reviewable from code. NEW's `AppointmentRecipientResolver` walks 7 in-memory sources to replicate this behavior; whether the resolver matches the proc's recipient set 1:1 is not verifiable from source alone. **Open question for the next session:** does Adrian have access to the OLD database to dump the proc body? If so, compare against `AppointmentRecipientResolver.ResolveAsync` behavior to confirm parity.

### 11.9 Pass 1 errata table (specific corrections)

| Pass 1 location | Pass 1 claim | Corrected claim |
|---|---|---|
| Section 1 row "Active handlers" | "12 wired handlers" | 13 handlers + the `IAccountEmailer` override = effectively 14 active triggers. |
| Section 1 row "Body migration" | "53 codes carry stub bodies" | More precisely: 6 codes have **simplified plain-HTML** bodies (NOT OLD-verbatim); 53 codes have stub bodies. The "OLD-verbatim" framing in the seed contributor + Pass 1 is wrong. |
| Section 3.2 row `UserRegisteredEmailHandler` | "Active. Subject typo fixed. Verify URL points at SPA" | **DEAD.** Handler wired, Eto defined, but Eto is never published. The actual user-registered email goes through `CaseEvaluationAccountEmailer` (Pipeline A). |
| Section 3.2 row `PackageDocumentReminderEmailHandler` | "Wired but the Eto is never published" | **Active.** `PackageDocumentReminderJob` runs at 08:30 PT daily and publishes the Eto. |
| Section 3.2 row `JdfAutoCancelledEmailHandler` | "Wired but the Eto is never published" | **Active.** `JointDeclarationAutoCancelJob` runs at 06:00 PT daily and publishes the Eto + flips appointment status to `CancelledNoBill`. |
| Section 4 mapping `UserAuthenticationDomain.PostForgotPassword -> ResetPassword` | "None (ABP Account UI handles it). `ResetPassword` (stub)" | **Active.** `CaseEvaluationAccountEmailer.SendPasswordResetLinkAsync` dispatches the `ResetPassword` template through the same template renderer + Hangfire pipeline. The template body is still a stub. |
| Section 4 mapping `UserDomain.SendEmail PasswordChange` | "None" | Need to verify - ABP's `IdentityUserAppService.ChangePasswordAsync` may NOT trigger an email send by default. Likely truly absent. |
| Section 4 mapping `SchedulerDomain.AppointmentDueDateApproachingNotification` | "None" | **Partial coverage** via `AppointmentDayReminderJob` at T-7 + T-1 (two windows vs OLD's single window). Inline HTML, not template-driven. |
| Section 4 mapping `SchedulerDomain.AppointmentPendingDailyNotification` | "None - no NEW recurring job" | **Partial coverage** via `RequestSchedulingReminderJob` at 08:00 PT, fires for Pending appointments at 30/60/75/85/90 elapsed days. CCR-driven, not a daily digest. |
| Section 6 G5 | "0 background scheduler integration for emails" | **Wrong.** 5 Hangfire RecurringJobs registered; problem is the 3 inline-HTML jobs bypass the dispatcher, not absence of infrastructure. |
| Section 7.1 effort | "G5 reminder scheduler ... ~3 days (significant)" | Substantially less - the scheduler already exists; the work is body migration to template-driven dispatch. ~1 day to migrate 3 inline-HTML jobs to dispatcher path. |
| Section 9 errata table | "57 HTML files (1 wrapper + 13 unreferenced + 43 referenced)" | Confirmed by `wc -l` - the count holds. But the wrapper `CommonTemplate.html` is NOT referenced anywhere in OLD code via `EmailTemplate` enum or `GetEmailTemplateFromHTML` - it's a CommonTemplate that the per-event HTML files structurally extend (each event template recreates the same outer table). Treat as branding scaffold for next-session body authoring. |

### 11.10 NEW Angular SPA - verified surface for email-related features

Verified 2026-05-08 by listing `angular/src/app/`:

| Feature | Folder exists? | Has email/notification UI? |
|---|---|---|
| account/register | Yes | Registration form. Submits to `api/public/external-signup` then bounces to `/Account/ConfirmUser?email=...`. |
| account/email-confirmation | Yes (route registered in `app.routes.ts:51`) | Confirms via `userId` + `confirmationToken` query string (matches the URL `CaseEvaluationAccountEmailer.SendEmailConfirmationLinkAsync` builds). |
| account/reset-password | Yes (per `app.routes.ts`) | Reset-password landing page (matches URL `SendPasswordResetLinkAsync` builds). |
| appointment-packet | Yes (`AppointmentPacketComponent`) | Download + Regenerate buttons + 5s polling. |
| appointment-documents | Yes (`AppointmentDocumentsComponent`) | Upload UI for the document workflow. |
| notifications / notification-templates | **No folder exists** | No IT-Admin template editor UI. Backend `NotificationTemplatesController` exists but the SPA has no consumer. |
| user-query / contact-us | **No folder exists** | OLD's UserQuery feature has no NEW SPA. |
| change-requests / cancel / reschedule | TBD - needs verification | OLD has dedicated UI; verify NEW. |

### 11.11 OLD frontend - templates available for next-session reference

`P:\PatientPortalOld\patientappointment-portal\src\app\components\` has these email/packet-touching folders (verified 2026-05-08):

- `template-management/templates/` - the IT-Admin template editor UI
- `system-parameter/` - the `SystemParameter.CcEmailIds` editor (where the per-tenant CC email list is configured)
- `user-query/` - the contact-us / user-query feature
- `appointment-request/upload-document-by-pass/` - the verification-code-based anonymous upload UI (a non-logged-in user clicks the email link, lands here, uploads without authenticating)
- `appointment-request/appointment-documents/` - package documents
- `appointment-request/appointment-new-documents/` - ad-hoc documents
- `appointment-request/appointment-joint-declarations/` - JDF
- `appointment-request/appointment-change-requests/` - cancel/reschedule
- `appointment-request/appointment-accessors/` - accessor invite UI
- `appointment-request/appointment-request-report/` - report screen (may include packet downloads)

**The next session implementing template editor UI** should reference OLD `template-management/templates/` for shape (subject + body editor with `##Variable##` placeholder reference list).

**The next session implementing system-parameter editing** should reference `system-parameter/` for the CC-emails field shape.

### 11.12 Final corrected gap punchlist

Replace Pass 1 Section 7.1 with this corrected ordering:

1. **Body migration: 6 simplified -> OLD-verbatim** (only AFTER per-tenant branding ships, otherwise the templates render with broken `<img src="">` for `##CompanyLogo##`, blank `##lblHeaderTitle##`, etc.). Effort: 1-2 days per template once branding is wired.
2. **Wire per-tenant branding** - new `Tenant.CompanyLogoBlobName`, `Tenant.HeaderTitle`, `Tenant.FooterText`, `Tenant.SupportEmail`, `Tenant.SupportPhone`, `Tenant.SupportFax`. Per-tenant editor UI in NEW SPA (does not exist today). Effort: ~3 days.
3. **Decide UserRegisteredEmailHandler fate** - delete the dead handler + Eto, OR remove the AuthServer override and route registration through the Eto. Either choice, document in code. Effort: half-day.
4. **Body migration for Phase 1 already-wired handlers** - copy the simplified-HTML pattern (or full OLD HTML if branding is wired) for `AppointmentApprovedExt/Internal`, `AppointmentRejected`, `PatientAppointmentApproveReject`, `PatientAppointmentPending`. Currently these have OLD subjects but very stripped bodies. Effort: 1 day.
5. **Inline-HTML reminder jobs -> dispatcher** - migrate `RequestSchedulingReminderJob`, `CancellationRescheduleReminderJob`, `AppointmentDayReminderJob` to use `INotificationDispatcher` so subjects/bodies are IT-Admin editable. Pick a code (or add new ones to `NotificationTemplateConsts.Codes`). Effort: ~1 day.
6. **CC behavior parity (G1 from Pass 1)** - lift `AppendCcRecipientsAsync` from `BookingSubmissionEmailHandler` into a shared helper, call from `StatusChangeEmailHandler.DispatchApprovedAsync`. Pass 1 G1 was correct. Effort: half-day.
7. **G2 architectural decision** - which `AppointmentSubmittedEto` handler wins, the Domain inline-HTML or the Application template-driven? Pass 1 G2 was correct. Effort: half-day to decide + half-day to remove the loser.
8. **G3 doc handler flag-based template branching** - `IsAdHoc` and `IsJointDeclaration` should pick `PatientNewDocument*` and `JointAgreementLetter*` templates respectively. Currently all 3 doc handlers always use `PatientDocument*`. Effort: half-day.
9. **G4 admin-override reschedule template** - `PatientAppointmentRescheduleReqAdmin` is currently collapsed into `AppointmentRescheduleRequestApproved`. Branch in handler. Effort: half-day.
10. **Body migration for change-request templates** - 6 codes with stub bodies. Effort: 1 day.
11. **Body migration for document templates** - 9 codes (3 PatientDocument + 3 PatientNewDocument + 3 JointAgreementLetter). Effort: 1 day.
12. **Phase-3 from-scratch handlers** for: PasswordChange (verify the AppEmailer covers it; if not, add), AddInternalUser, UserQuery, CheckedIn / CheckedOut / NoShow / CancelledNoBill, ClinicalStaffCancellation, AppointmentChangeLogs, AppointmentRescheduleRequestByAdmin, post-approval packet email (covered in `document-packets.md` audit). Effort: variable, probably 5+ days.

**Total Phase 1 (parity-close) effort:** ~7-10 dev-days (down from Pass 1's ~12-15).

---

**End of email parity audit.** Companion document for packet generation lives in `docs/parity/email-packet-parity/document-packets.md`.
