# 04. Emails & SMS -- OLD vs NEW behavioral parity

Auditor area: email notifications + SMS. Source of truth is CODE on both
sides. OLD is read-only at `P:\PatientPortalOld`; NEW is this repo. Cited
as `file:line`. Equivalence (right recipient, right message, right
trigger) is NOT a gap even when the transport differs (SES -> ABP
IEmailSender/SMTP, disk-HTML -> DB `NotificationTemplate`, MimeKit ->
`MailMessage`, `##x##` engine reimplemented faithfully).

---

## Coverage

OLD email/SMS surface examined:
- `PatientAppointment.Infrastructure\Utilities\SendMail.cs` -- 4 send
  overloads (SES `SendSMTPMailAWS`, SMTP `SendSMTPMail`, +attachment
  variants). SMTP variant is the live one; `smtpConfiguration` from
  `SMTPConfiguration` table; CC pulled from
  `SystemParameter.CcEmailIds`; BCC supported.
- `PatientAppointment.Infrastructure\Utilities\TwilioSmsService.cs` --
  the only SMS path. Twilio 5.24.0. Gated by `isSMSEnable` ServerSetting.
- `PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs`
  :103-251 -- `GetEmailTemplate`, `GetEmailTemplateFromHTML`,
  `GetNotificationTemplateBody` (`##Var##` reflection-based substitution).
- `PatientAppointment.DbEntities\Models\Template.cs` (spm.Templates:
  Subject + BodyEmail + BodySms + TemplateCode + TemplateTypeId).
- `PatientAppointment.Models\Enums\TemplateType.cs` (Email=1, SMS=2).
- `PatientAppointment.DbEntities\Constants\ApplicationConstants.cs`
  :26-70 -- `EmailTemplate` static class, 43 `.html` filename constants.
- All trigger call sites: `AppointmentDomain`, `AppointmentDocumentDomain`,
  `AppointmentNewDocumentDomain`, `AppointmentJointDeclarationDomain`,
  `AppointmentChangeRequestDomain`, `AppointmentChangeLogDomain`,
  `AppointmentAccessorDomain`, `SchedulerDomain`, `UserDomain`,
  `UserAuthenticationDomain`, `UserQueryDomain`.
- 58 `.html` files on disk under
  `PatientAppointment.Api\wwwroot\EmailTemplates\` (brief estimated ~59).

NEW email/SMS surface examined:
- `Domain.Shared\NotificationTemplates\NotificationTemplateConsts.cs` --
  59 unified template codes (`Codes.All`).
- `Domain\NotificationTemplates\NotificationTemplateDataSeedContributor.cs`
  -- seeds 59 per-tenant rows + 2 type rows (Email/SMS).
- `Domain\NotificationTemplates\EmailSubjects.cs` (subject map),
  `EmailBodyResources.cs` (41 embedded `.html` bodies; rest are stubs).
- `Domain\Notifications\TemplateVariableSubstitutor.cs` (`##Var##` engine).
- `Application\Notifications\NotificationDispatcher.cs`,
  `NotificationTemplateRenderer.cs`, `EmailSubjectBuilder.cs`,
  `CcRecipientAppender.cs`.
- `Application\Notifications\Handlers\*` -- 20 `ILocalEventHandler<*Eto>`
  handlers.
- `Domain\Appointments\Jobs\SendAppointmentEmailJob.cs` (Hangfire worker).
- `Application\Emailing\CaseEvaluationAccountEmailer.cs` (IAccountEmailer
  override -- registration / reset / confirmation).
- `Domain\Notifications\Jobs\*` -- 5 scheduler jobs that publish the
  reminder/digest ETOs.

---

## Summary counts

| Class | Count |
| --- | --- |
| Missing behavior | 4 |
| Partial behavior | 3 |
| Intent deviation | 3 |
| Equivalent (different implementation) | 8 |
| OLD-bug (do not port) | 4 |

Template wiring of the 59 unified codes (OLD's 43 disk + 16 DB codes
collapsed into one table): 36 wired (a handler / emailer / appservice
dispatches them at a real trigger), 23 seeded-but-unwired (row exists,
nothing dispatches them yet). Of OLD's 58 disk `.html` files, ~13 were
already dead in OLD (no `EmailTemplate.` constant references them) and
correctly not carried as live triggers.

---

## Template coverage map

OLD template (disk `.html` or DB `TemplateCode`) -> OLD trigger -> NEW
status. NEW code names are `NotificationTemplateConsts.Codes.*`.

| OLD template | OLD trigger (file:line) | NEW status |
| --- | --- | --- |
| User-Registed.html | UserDomain:331 / UserAuthenticationDomain:138 (register) | WIRED -- `CaseEvaluationAccountEmailer.SendEmailConfirmationLinkAsync` -> `UserRegistered` |
| ResetPassword.html | UserAuthenticationDomain:201 (forgot pw) | WIRED -- `CaseEvaluationAccountEmailer.SendPasswordResetLinkAsync` -> `ResetPassword` |
| Password-Changed.html | UserAuthenticationDomain:311 / UserDomain:344 | SEEDED, UNWIRED -- `PasswordChange` body exists; no NEW send site found |
| Add-Internal-User.html | UserDomain:308 ("Welcome to socal") | REPLACED -- `InternalUsersAppService:379` dispatches `InternalUserCreated` (new welcome+temp-pw email) |
| Patient-Appointment-Pending.html | AppointmentDomain:930 (booking submit, to patient/booker) | REPLACED by 3 per-recipient codes -- `AppointmentRequestedOffice/Registered/Unregistered` via `BookingSubmissionEmailHandler`. `PatientAppointmentPending` row seeded but never dispatched |
| Patient-Appointment-ApproveReject.html | AppointmentDomain:944 (booking by external user -> notify staff) | WIRED -- `BookingSubmissionEmailHandler:551` -> `PatientAppointmentApproveReject` |
| Patient-Appointment-ApprovedInternal.html | AppointmentDomain:965 (approve, internal updater) | WIRED -- `StatusChangeEmailHandler:335` -> `PatientAppointmentApprovedInternal` |
| Patient-Appointment-ApprovedExternal.html | AppointmentDomain:979 (approve, external updater) | WIRED -- `StatusChangeEmailHandler:280` -> `PatientAppointmentApprovedExt` |
| Patient-Appointment-Rejected.html | AppointmentDomain:989 (reject) | WIRED -- `StatusChangeEmailHandler:371` -> `PatientAppointmentRejected` |
| Patient-Appointment-CheckedIn.html | AppointmentDomain:1001 (check-in) | WIRED -- `StatusChangeEmailHandler:412` -> `PatientAppointmentCheckedIn` |
| Patient-Appointment-CheckedOut.html | AppointmentDomain:1013 (check-out) | WIRED -- `StatusChangeEmailHandler:448` -> `PatientAppointmentCheckedOut` |
| Patient-Appointment-NoShow.html | AppointmentDomain:1019 (no-show) | WIRED -- `StatusChangeEmailHandler:487` -> `PatientAppointmentNoShow` (internal staff only) |
| Patient-Appointment-CancelledNoBill.html | AppointmentDomain:1032 (cancel, no bill) | WIRED -- `StatusChangeEmailHandler:533` -> `PatientAppointmentCancelledNoBill` |
| Accessor-Appointment-Booked.html | AppointmentAccessorDomain:257 (accessor added) | WIRED -- `AccessorInvitedEmailHandler:149` -> `AccessorAppointmentBooked` |
| Patient-Document-Accepted.html | AppointmentDocumentDomain:260 / AppointmentNewDocumentDomain:204 / JointDeclaration:248 (pkg doc accepted) | WIRED -- `DocumentAcceptedEmailHandler` -> `PatientDocumentAccepted` |
| Patient-Document-Rejected.html | AppointmentDocumentDomain:279 / others (pkg doc rejected) | WIRED -- `DocumentRejectedEmailHandler` -> `PatientDocumentRejected` |
| Patient-Document-Uploaded.html | AppointmentDocumentDomain:293 (pkg doc uploaded) | WIRED -- `DocumentUploadedEmailHandler` -> `PatientDocumentUploaded` |
| Patient-New-Document-Accepted.html | AppointmentDocumentDomain:332 (ad-hoc doc accepted) | WIRED -- `DocumentNotificationContext:125` -> `PatientNewDocumentAccepted` |
| Patient-New-Document-Rejected.html | AppointmentDocumentDomain:355 (ad-hoc doc rejected) | WIRED -- `DocumentNotificationContext:126` -> `PatientNewDocumentRejected` |
| Patient-New-Document-Uploaded.html | AppointmentDocumentDomain:374 (ad-hoc doc uploaded) | WIRED -- `DocumentNotificationContext:124` -> `PatientNewDocumentUploaded` |
| Patient-Document-Accepted-With-Remaining-Documents.html | JointDeclaration:244 (accepted, docs remain) | WIRED -- `DocumentAcceptedEmailHandler:138` -> `PatientDocumentAcceptedRemainingDocs` |
| Patient-Document-Rejected-With-Remaining-Documents.html | JointDeclaration:269 (rejected, docs remain) | WIRED -- `DocumentRejectedEmailHandler:138` -> `PatientDocumentRejectedRemainingDocs` |
| Joint-Agreement-Letter-Accepted.html | AppointmentChangeRequestDomain:978 (JDF accepted) | WIRED -- `DocumentNotificationContext:115` -> `JointAgreementLetterAccepted` |
| Joint-Agreement-Letter-Uploaded.html | AppointmentChangeRequestDomain:989 (JDF uploaded) | WIRED -- `DocumentNotificationContext:114` -> `JointAgreementLetterUploaded` |
| Joint-Agreement-Letter-Rejected.html | AppointmentChangeRequestDomain:996 (JDF rejected) | WIRED -- `DocumentNotificationContext:116` -> `JointAgreementLetterRejected` |
| Appointment-Document-Add-With-Attachment.html | AppointmentDocumentDomain:457/491+ (approved packet w/ attachment) | WIRED -- `PatientPacketEmailHandler:121` + `AttyCEPacketEmailHandler:150` -> `AppointmentDocumentAddWithAttachment` |
| Clinical-Staff-Cancellation.html | AppointmentChangeRequestDomain:656 (clinic-staff cancel) | WIRED -- `ClinicalStaffCancellationEmailHandler:126` -> `ClinicalStaffCancellation` |
| Patient-Appointment-Reschedule-Request-Admin.html | AppointmentChangeRequestDomain:717 (reschedule req submitted) | WIRED -- `ChangeRequestSubmittedEmailHandler:116` -> `AppointmentRescheduleRequest` |
| Patient-Appointment-Reschedule-Request-Approved.html | AppointmentChangeRequestDomain:730 (reschedule approved) | WIRED -- `ChangeRequestApprovedEmailHandler:119` -> `AppointmentRescheduleRequestApproved` |
| Patient-Appointment-Reschedule-Request-Rejected.html | AppointmentChangeRequestDomain:740 (reschedule rejected) | WIRED -- `ChangeRequestRejectedEmailHandler:95` -> `AppointmentRescheduleRequestRejected` |
| Patient-Appointment-Cancellation-Apporved.html | AppointmentChangeRequestDomain:747 (cancel-req approved) | WIRED -- `ChangeRequestApprovedEmailHandler:118` -> `AppointmentCancelledRequestApproved` |
| (cancel-req submitted) | ChangeRequestSubmittedEmailHandler:115 | WIRED (NEW-only) -- `AppointmentCancelledRequest`; OLD did not email on cancel submit |
| (cancel-req rejected) | ChangeRequestRejectedEmailHandler:94 | WIRED (NEW-only) -- `AppointmentCancelledRequestRejected`; OLD path commented out |
| PendingAppointmentDailyNotification.html | SchedulerDomain:83 (daily pending digest) | WIRED -- `PendingDailyDigestJob` -> `PendingDailyDigestEmailHandler:94` -> `PendingAppointmentDailyNotification` |
| Appointment-ApproveReject-Internal.html | SchedulerDomain:112 (internal staff queue digest) | WIRED -- `InternalStaffQueueDigestJob` -> `InternalStaffQueueDigestEmailHandler:79` -> `AppointmentApproveRejectInternal` |
| Upload-Pending-Documents.html | SchedulerDomain:145/228 (package-doc reminder) | WIRED -- `PackageDocumentReminderJob` -> `PackageDocumentReminderEmailHandler:139` -> `UploadPendingDocuments` |
| Appointment-DueDate-Reminder.html | SchedulerDomain:170 (due-date approaching) | WIRED -- `DueDateApproachingJob` -> `DueDateApproachingEmailHandler:108` -> `AppointmentDueDateReminder` |
| Appointment-Document-Incomplete.html | SchedulerDomain:198 (due-date doc incomplete) | WIRED -- `DueDateDocumentIncompleteJob` -> `DueDateDocumentIncompleteEmailHandler:108` -> `AppointmentDocumentIncomplete` |
| Appointment-Cancelled-With-DueDate.html | SchedulerDomain:256 (auto-cancel at due date) | WIRED -- `JdfAutoCancelledEmailHandler:117` -> `AppointmentCancelledDueDate` |
| User-Query.html | UserQueryDomain:86/102 (Submit Query / Contact Us) | SEEDED, UNWIRED -- `UserQuery` + `SubmitQuery` rows seeded; no NEW dispatch |
| Appointment-Change-Logs.html | AppointmentChangeLogDomain:307 (appointment field changed) | SEEDED, UNWIRED -- `AppointmentChangeLogs` row seeded; no NEW dispatch |
| Appointment-Reschedule-Request-Changed-By-Admin.html | AppointmentChangeLogDomain:324 (admin changed a reschedule) | SEEDED, UNWIRED -- `PatientAppointmentRescheduleReqAdmin` / `AppointmentRescheduleRequestByAdmin` seeded; no NEW dispatch |
| Appointment-Pending-Next-Day.html | SchedulerDomain:308 (commented out in OLD) | SEEDED, UNWIRED -- never fired in OLD either |
| PatientDocumentAcceptedAttachment (no disk file) | referenced ApplicationConstants:52 but file absent in OLD | SEEDED, UNWIRED -- code carried; dead in OLD (missing file) |
| Appointment-Request-Booked/Approved/Rejected/Cancelled.html | none (no `EmailTemplate.` constant) | DEAD IN OLD -- not carried as a live trigger |
| Appointment-Cancellation-Request-Booked/Accepted/Rejected.html | none (call site commented at AppointmentChangeRequestDomain:289) | DEAD IN OLD -- not carried |
| Appointment-Reschedule-Request-Booked/Approved/Rejected.html | none referenced | DEAD IN OLD -- not carried |
| Appointment-Notify-Accessor.html / Appointment-Changes-Notify-User.html / Appointment-Query.html / Appointment-Documemt-Rejected.html / Appointment-Join-Declaration-Document-Rejected.html / Patient-Appointment-Reschedule-Request.html / CommonTemplate.html | none referenced | DEAD IN OLD -- orphan disk files |
| AppointmentBooked / AppointmentApproved / AppointmentRejected / AppointmentApprovedStakeholderEmails / AppointmentCancelledByAdmin / RejectedPackageDocument / RejectedJointDeclarationDocument / AppointmentDueDate / AppointmentDueDateUploadDocumentLeft (DB TemplateCode enum) | OLD `GetNotificationTemplateBody(code)` -- all call sites commented out (SMS bodies) | SEEDED, UNWIRED -- DB-code rows carried; never sent in OLD (see G-04-01) |

---

## Behavioral gaps

### G-04-01 -- SMS channel entirely absent in NEW (and almost dead in OLD)

- **Class:** Intent deviation (verging on no-op).
- **OLD:** `TwilioSmsService.SendSms` (`TwilioSmsService.cs:23-43`),
  gated by `isSMSEnable` ServerSetting. Live SMS call sites are ONLY in
  `SchedulerDomain` (`:105, :137, :191, :220, :249`) -- the daily
  background digests. Every interactive SMS path is commented out:
  `AppointmentDomain.SendSMS` (`:846-880`) has all three switch cases
  commented so `isSendSMS` stays `false` and `SendSms` at `:879` is
  unreachable; `AppointmentChangeRequestDomain` SMS block
  (`:783-841`) is likewise fully commented.
- **NEW:** No SMS delivery anywhere. `NotificationDispatcher` renders
  `BodySms` (`RenderAsync` populates it) but never sends it -- the
  dispatcher's class doc explicitly defers the "SMS leg"
  (`NotificationDispatcher.cs:19-28`). No Twilio/ACS-SMS provider is
  wired (the only `Volo.Abp.Sms.dll` present is the ABP base abstraction
  pulled in by Account.Pro; no concrete sender is registered). Seeder
  writes `bodySms: "Stub SMS for {code}."` for every code
  (`NotificationTemplateDataSeedContributor.cs:126`).
- **What it is + user impact:** In OLD, internal staff received SMS for
  the 4-5 scheduled digests (pending queue, package-doc reminder,
  due-date approaching, doc-incomplete) when `isSMSEnable` was on. NEW
  sends none of these as SMS; the equivalent information goes out as the
  email digest only. No patient/external SMS existed in OLD (those
  paths were commented), so NO patient-facing SMS is lost.
- **Why it existed:** OLD shipped a Twilio integration but the team had
  disabled most of it (commented-out call sites + a feature flag).
- **Plain-English:** OLD could text the clinic's staff a daily summary
  if SMS was switched on. The new app does not text anyone yet; it sends
  the same summaries by email.
- **Keep in NEW?** Defer. SMS is a real OLD behavior for staff digests
  only, behind a flag that may have been off in production. When ACS/
  Twilio SMS creds land, wire `BodySms` delivery for the 4 scheduler
  digests. Patient SMS was never live -- do not build it.

### G-04-02 -- "Submit Query" / "Contact Us" email not wired

- **Class:** Missing behavior.
- **OLD:** `UserQueryDomain.cs:86-104` -- on a user query submission OLD
  loads `User-Query.html` and emails the configured recipient
  (subject `"Patient Appointment Portal - <patient> - User query"`).
  Fires for both authenticated (`:86`) and anonymous (`:102`) queries.
- **NEW:** Codes `UserQuery` and `SubmitQuery` are seeded (rows exist),
  but no handler or appservice dispatches them. No query feature exists
  yet in NEW.
- **What it is + user impact:** A patient/visitor who submits a question
  through the portal generates no email to staff. The message is lost
  (or the feature is simply absent).
- **Why it existed:** OLD had a public "Submit Query / Contact Us" form.
- **Plain-English:** OLD emailed staff when someone used the contact
  form. New app does not have that yet.
- **Keep in NEW?** Yes, when the Submit-Query feature is built. Template
  already seeded.

### G-04-03 -- Appointment change-log email not wired

- **Class:** Missing behavior.
- **OLD:** `AppointmentChangeLogDomain.cs:307-309` -- when an appointment
  field is edited, OLD loads `Appointment-Change-Logs.html` and emails
  stakeholders a diff of what changed. `:324-325` covers the
  admin-changed-a-reschedule variant
  (`Appointment-Reschedule-Request-Changed-By-Admin.html`).
- **NEW:** Codes `AppointmentChangeLogs` and
  `PatientAppointmentRescheduleReqAdmin` /
  `AppointmentRescheduleRequestByAdmin` are seeded but unwired. No
  NEW dispatch on appointment edit.
- **What it is + user impact:** Editing appointment details in OLD
  notified stakeholders of the change; NEW edits are silent.
- **Why it existed:** OLD audit-logged appointment changes and emailed a
  summary.
- **Plain-English:** OLD told everyone when an appointment's details were
  changed. New app does not email on edits.
- **Keep in NEW?** Yes, pair with the appointment change-log / audit
  feature when it lands.

### G-04-04 -- Password-changed security-receipt email not wired

- **Class:** Missing behavior.
- **OLD:** `UserAuthenticationDomain.cs:311-317` + `UserDomain.cs:344-345`
  -- after a password change (in-app or post-reset) OLD emails a
  "your password was changed" receipt using `Password-Changed.html`.
- **NEW:** `PasswordChange` code is seeded with a real HTML body
  (`EmailBodies/PasswordChange.html` exists) and a subject, but no send
  site dispatches it. `CaseEvaluationAccountEmailer` covers
  confirmation + reset link + 2FA code, but not the post-change receipt.
  ABP's own password-change flow does not call this template.
- **What it is + user impact:** Users get no confirmation email after
  changing their password -- a minor security-hygiene regression.
- **Why it existed:** Standard "your password changed" notification.
- **Plain-English:** OLD emailed you after you changed your password.
  New app stays quiet.
- **Keep in NEW?** Yes -- low effort; hook the template into ABP's
  password-changed event. Template + subject already exist.

### G-04-05 -- Booking "Pending" email replaced by 3-way "Requested" fan-out

- **Class:** Partial behavior (intent largely preserved; recipient split
  and copy changed by directive).
- **OLD:** `AppointmentDomain.cs:930-933` -- on booking submit OLD sends
  ONE `Patient-Appointment-Pending.html` email to the booker/patient
  (subject "...has been Pending."), CC `clinicStaffEmail`, then if the
  submitter is an external user, a second `Patient-Appointment-
  ApproveReject.html` to Staff Supervisor + Clinic Staff (`:935-950`).
- **NEW:** `BookingSubmissionEmailHandler` fans out to three
  per-recipient codes by recipient class -- `AppointmentRequestedOffice`
  (office mailbox), `AppointmentRequestedRegistered` (registered party),
  `AppointmentRequestedUnregistered` (unregistered party, with a
  register-link), plus `PatientAppointmentApproveReject` to staff
  (`:551`). The OLD `PatientAppointmentPending` row is seeded with a
  real body but never dispatched. Subject reworded "Pending" ->
  "Requested" (Adrian directive 2026-05-08) and the Pending-path CC of
  `clinicStaffEmail` is dropped on the requested fan-out.
- **What it is + user impact:** Same trigger (booking submitted), same
  staff approve/reject notification, but the patient-facing email is now
  recipient-aware and the wording changed. A registered party gets a
  log-in CTA; an unregistered party gets a register CTA -- richer than
  OLD's single template.
- **Why it existed:** OLD used one generic patient email + a global CC.
- **Plain-English:** Same "we got your request" email, now tailored to
  whether the recipient has an account, and the word "Pending" became
  "Requested." Staff still get the approve/reject email.
- **Keep in NEW?** Keep NEW behavior (it is an intentional, approved
  improvement). Flag only because it is a deliberate deviation, not 1:1.

### G-04-06 -- Approved-email CC (`clinicStaffEmail`) not reproduced

- **Class:** Partial behavior.
- **OLD:** On the Approved branch, `AppointmentDomain.cs:954` sets
  `emailCC = clinicStaffEmail` and passes it as the 4th arg on both the
  internal (`:966`) and external (`:980`) approval sends, so the clinic
  staff mailbox is CC'd on every approval.
- **NEW:** `StatusChangeEmailHandler` approval dispatch does NOT add the
  clinic-staff CC. The handler's own comments (`:46-47, :265, :320`)
  acknowledge "OLD CC behavior (clinicStaffEmail global) for Approved:
  not yet reproduced here." Per-tenant `SystemParameter.CcEmailIds` is
  the intended replacement (and IS applied on the booking path via
  `CcRecipientAppender`), but the approval path does not call it.
- **What it is + user impact:** Clinic staff are not CC'd on approval
  emails in NEW. If the office relied on that CC to track approvals,
  they lose that copy (unless they set `CcEmailIds`, which still is not
  wired into the approval path).
- **Why it existed:** OLD CC'd a global clinic-staff inbox on approvals.
- **Plain-English:** OLD copied the clinic's shared inbox on every
  approval email; the new app does not.
- **Keep in NEW?** Yes -- apply `CcRecipientAppender` to the approval
  dispatch in `StatusChangeEmailHandler` for parity.

### G-04-07 -- 9 DB-managed `TemplateCode` events seeded but never sent

- **Class:** Partial behavior.
- **OLD:** OLD's `TemplateCode` int enum (DB-managed `spm.Templates`
  rows, read via `GetNotificationTemplateBody`) covered events like
  `AppointmentBooked`, `AppointmentApproved`, `AppointmentRejected`,
  `AppointmentApprovedStakeholderEmails`, `AppointmentCancelledByAdmin`,
  `RejectedPackageDocument`, `RejectedJointDeclarationDocument`,
  `AppointmentDueDate`, `AppointmentDueDateUploadDocumentLeft`. In OLD
  these were used ONLY for SMS bodies, and every call site that read
  them is commented out (e.g. `AppointmentDomain.cs:849-872`,
  `AppointmentChangeRequestDomain.cs:783-835`). They never sent anything
  in OLD's live code.
- **NEW:** All 9 codes are seeded as rows
  (`NotificationTemplateConsts.Codes` A-block, lines 70-91) but no
  handler dispatches them. The user-visible email behavior they would
  have driven is covered by the disk-HTML-derived codes that ARE wired
  (e.g. approval emails fire via `PatientAppointmentApprovedExt`, not
  `AppointmentApproved`).
- **What it is + user impact:** None at runtime -- these were dead in OLD
  too. Carrying them as seeded rows is harmless and keeps the door open
  for IT-Admin editing.
- **Why it existed:** OLD's two parallel template systems (DB + disk)
  overlapped; the DB set was the SMS half, mostly disabled.
- **Plain-English:** A set of old "template slots" that were never
  actually used to send anything are carried over as empty editable
  rows. Nobody loses an email.
- **Keep in NEW?** Keep seeded (cheap, IT-Admin-editable) but do not
  build dispatch -- they would duplicate emails the wired codes already
  send. Revisit only if SMS (G-04-01) is implemented.

### G-04-08 -- BCC support dropped

- **Class:** Intent deviation (low impact).
- **OLD:** Every `SendMail` overload accepts a `mailBCC` parameter and
  adds BCC recipients (`SendMail.cs:221-232, :435-446`). In practice no
  live call site passes a non-empty BCC, so it was a latent capability.
- **NEW:** `SendAppointmentEmailJob` / `IEmailSender.SendAsync` path has
  no BCC argument; the dispatcher models recipients as To + CC (via
  `CcRecipientAppender`) only.
- **What it is + user impact:** None observed -- OLD never used BCC in
  live triggers.
- **Why it existed:** Generic mailer flexibility.
- **Plain-English:** OLD's mailer could blind-copy people; nothing ever
  used it. New app does not offer BCC.
- **Keep in NEW?** No, unless a feature later needs it. Not a real
  regression.

### G-04-09 -- Subject-line identity suffix: parity preserved (deviation note)

- **Class:** Intent deviation (cosmetic; deliberate).
- **OLD:** Subjects built as `"Patient Appointment Portal - (" +
  patientName + injuryDetails + ") - <event>"`, where `injuryDetails`
  appends `" - Claim: <n>"` and `" - ADJ: <wcab>"` when present
  (`AppointmentDomain.cs:916-921`, `AppointmentDocumentDomain.cs:818`).
- **NEW:** `EmailSubjectBuilder.BuildIdentitySuffix` reproduces the
  `"(Patient: <name> - Claim: <n> - ADJ: <wcab>)"` suffix verbatim
  (`EmailSubjectBuilder.cs:41-81`) and exposes it as the
  `##EmailSubjectIdentity##` token used in `EmailSubjects.cs`. The fixed
  literal prefix changed from "Patient Appointment Portal" to
  "Appointment Portal" (shorter) across the seeded subjects.
- **What it is + user impact:** Subject lines carry the same
  patient/claim/ADJ context; only the leading brand string is shortened.
- **Plain-English:** Email subjects still show patient name, claim and
  ADJ numbers; the leading product name was trimmed.
- **Keep in NEW?** Keep -- intentional cosmetic shortening; identity
  suffix is faithfully reproduced.

### G-04-10 -- Failed sends are swallowed (no retry) by design

- **Class:** Intent deviation (parity-matched, both swallow).
- **OLD:** `SendMail` catches all send exceptions and logs into
  `ApplicationExceptionLog` -- BUT the log writes are commented out
  (`SendMail.cs:156-157, :262-263`), so OLD silently swallows mail
  failures with no record.
- **NEW:** `SendAppointmentEmailJob` catches SMTP exceptions and logs a
  warning, then lets Hangfire mark the job "Succeeded" so it never
  retries (`SendAppointmentEmailJob.cs:100-107`). Intentional while ACS
  placeholder creds are in use; documented to be removed when real creds
  land.
- **What it is + user impact:** A failed email is lost on both sides; NEW
  at least logs a warning (OLD logged nothing). No user-visible parity
  gap.
- **Plain-English:** If an email fails to send, neither app keeps trying;
  the new app at least writes a log line.
- **Keep in NEW?** Keep for now; remove the try/catch when ACS creds are
  real so Hangfire retries (already noted in the job's doc comment).

---

## Equivalent (different implementation)

These are NOT gaps -- same outcome, different mechanism.

| Concern | OLD | NEW | Why equivalent |
| --- | --- | --- | --- |
| Transport | AWS SES (`SendSMTPMailAWS`) + System.Net SMTP (`SendSMTPMail`) | ABP `IEmailSender.SendAsync` over SMTP/ACS | Same email delivered; provider swap is expected and explicitly out of parity scope |
| Template storage | Disk HTML (`wwwroot/EmailTemplates/*.html`) + DB `spm.Templates` | Single per-tenant DB `NotificationTemplate` table | NEW unifies the two OLD stores; bodies seeded OLD-verbatim from embedded `.html` |
| Placeholder engine | `GetEmailTemplateFromHTML` reflection `##Prop##` flat-replace (`ApplicationUtility.cs:212-251`) | `TemplateVariableSubstitutor.Substitute` `##Key##` flat-replace (`TemplateVariableSubstitutor.cs:54-76`) | Reimplemented faithfully; same `##x##` syntax, same unknown-token-left-in-place behavior, OLD null-NRE risk fixed |
| Attachment send | MimeKit `BodyBuilder` (`SendMail.AddFiles`) | `System.Net.Mail.MailMessage` + `IEmailSender.SendAsync(mail)` (`SendAppointmentEmailJob:129-146`) | Same attached document delivered (OLD DOCX packet -> NEW DOCX/PDF packet) |
| CC source | `SystemParameter.CcEmailIds` split on `;` (`SendMail.cs:49-65`) + global `clinicStaffEmail` ServerSetting | `CcRecipientAppender` reads `SystemParameter.CcEmailIds` (`CcRecipientAppender.cs:67-76`) | Same per-tenant CC list; `clinicStaffEmail` global folded into per-tenant `CcEmailIds` (except approval path, see G-04-06) |
| Async dispatch | `async void` fire-and-forget on the request thread | Hangfire `SendAppointmentEmailJob` enqueue (`NotificationDispatcher.cs:107-125`) | Both decouple send from the request; NEW is more robust (durable queue) |
| Registration email | `User-Registed.html` via `SendSMTPMail` (`UserDomain.cs:331`) | `CaseEvaluationAccountEmailer.SendEmailConfirmationLinkAsync` -> `UserRegistered` (`:97-119`) | Same "confirm your account" email on register; NEW routes through ABP `IAccountEmailer` |
| Reset-password email | `ResetPassword.html` via `SendSMTPMail` (`UserAuthenticationDomain.cs:201-209`) | `CaseEvaluationAccountEmailer.SendPasswordResetLinkAsync` -> `ResetPassword` (`:121-139`) | Same reset-link email; NEW link points at AuthServer Razor pages |
| Confirmation-number format | `"A" + id.ToString("D5")` (`ApplicationUtility.cs:307`) | `"A" + n:D5` in `AppointmentsAppService` | Identical `A#####` format used in subjects/bodies |

---

## OLD bugs (do not port)

| # | Bug | OLD source | NEW handling |
| --- | --- | --- | --- |
| B-1 | Registration subject typo "Your have registered successfully" | `UserDomain.cs:321` | FIXED in NEW to "You have registered successfully" (`EmailSubjects.cs:28`) |
| B-2 | Reschedule subject typo "Your have successfully requested for reschedule" | `AppointmentChangeRequestDomain.cs:760` | FIXED to "You have successfully requested a reschedule" (`EmailSubjects.cs:243`) |
| B-3 | JDF subject wording "Joint Agreement Letter Uploaded Accepted" / "Uploaded Rejected" (double verb) | `AppointmentChangeRequestDomain.cs:976/994` | FIXED to "...Accepted." / "...Rejected." (`EmailSubjects.cs:135/143`) |
| B-4 | Template-code/filename typos: `RejectedJoinDeclarationDocument` (missing 't'), `AppointmentApprovedStackholderEmails` ("Stackholder"), `Apporved.html` / `PatientAppointmentCancellationApprvd` (misspelled "Approved") | `TemplateCode.cs`, `ApplicationConstants.cs:64`, disk filename | FIXED in code constants (`NotificationTemplateConsts.cs:81-43`); rendered subjects use corrected English |

Additional OLD smell (replicated, not a send bug): OLD's
`GetEmailTemplateFromHTML` calls `item.Value.ToString()` unconditionally
(`ApplicationUtility.cs:247`); the dictionary is pre-guarded to insert
`""` for nulls so it never NREs in OLD, but a direct null would throw.
NEW's substitutor renders null as empty explicitly
(`TemplateVariableSubstitutor.cs:90-92`) -- hardened, behavior-equivalent.

---

## Open questions

1. **Was `isSMSEnable` ever ON in OLD production?** Determines whether
   the staff-digest SMS (G-04-01) is a real lost behavior or a dormant
   flag. If it was off, SMS parity is moot for Phase 1.
2. **Approval-email CC (G-04-06):** should NEW reproduce the
   clinic-staff CC on approvals via `SystemParameter.CcEmailIds`, or is
   the office's own mailbox already a stakeholder recipient (making the
   CC redundant)? Needs office workflow confirmation.
3. **Password-changed receipt (G-04-04):** acceptable to defer, or is a
   security-receipt email required for HIPAA hygiene before go-live?
4. **Submit-Query / Contact-Us (G-04-02):** is the public query form in
   scope for parity Phase 1, or deferred with the feature?
5. **9 DB `TemplateCode` rows (G-04-07):** keep seeding the dead codes
   for IT-Admin visibility, or prune them to avoid confusing empty
   editable rows in the Template Management UI?
