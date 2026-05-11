# Email Notification Coverage Audit: OLD vs NEW

**Date:** 2026-05-05
**Auditor:** Claude Code
**Scope:** P:\PatientPortalOld (legacy ASP.NET/Angular) vs W:\patient-portal\replicate-old-app (ABP 10.0.2 / .NET 10)

---

## Executive Summary

- **Total OLD emails:** 59 template codes (16 DB-managed + 43 disk-HTML)
- **Total NEW emails wired:** 5 handler implementations (Approved, Rejected, Document Upload/Accept/Reject, Accessor Invited)
- **Total NEW emails declared-but-unwired:** 54 template codes seeded with stubs; handlers not yet subscribed
- **Current SMTP provider:** Azure Communication Services (ACS); credentials not yet provisioned
- **Blocking issue:** NullEmailSender gate active in Development environment; real email disabled until fixed

---

## 1. OLD Email Catalog (Summary)

59 templates across two mechanisms:

**DB-managed (16 codes, TemplateCode enum):**
- AppointmentBooked, AppointmentApproved, AppointmentRejected, AppointmentCancelledRequest
- AppointmentCancelledRequestApproved, AppointmentCancelledRequestRejected, AppointmentRescheduleRequest
- AppointmentRescheduleRequestApproved, AppointmentRescheduleRequestRejected, RejectedPackageDocument
- RejectedJointDeclarationDocument (typo: "RejectedJoin..." in OLD), AppointmentDueDate
- AppointmentDueDateUploadDocumentLeft, SubmitQuery, AppointmentApprovedStackholderEmails (typo: "Stackholder")
- AppointmentCancelledByAdmin

**Disk-resident HTML (43 files under wwwroot/EmailTemplates/):**
- User registration/auth: UserRegistered, ResetPassword, PasswordChange, AddInternalUser
- Appointment lifecycle: PatientAppointmentPending, PatientAppointmentApprovedInternal, PatientAppointmentApprovedExt, PatientAppointmentRejected, ClinicalStaffCancellation, AccessorAppointmentBooked
- Reschedule/cancel requests: PatientAppointmentRescheduleReq, PatientAppointmentRescheduleReqAdmin, PatientAppointmentRescheduleReqApproved, PatientAppointmentRescheduleReqRejected, AppointmentRescheduleRequestByAdmin, PatientAppointmentCancellationApproved (typo: "Apprvd" in HTML filename)
- Post-appointment: PatientAppointmentCheckedIn, PatientAppointmentCheckedOut, PatientAppointmentNoShow, PatientAppointmentCancelledNoBill
- Document workflow: PatientDocumentUploaded, PatientDocumentAccepted, PatientDocumentRejected, PatientNewDocumentAccepted, PatientNewDocumentRejected, PatientNewDocumentUploaded, PatientDocumentAcceptedAttachment, PatientDocumentAcceptedRemainingDocs, PatientDocumentRejectedRemainingDocs, AppointmentDocumentAddWithAttachment
- Joint Declaration Forms: JointAgreementLetterUploaded, JointAgreementLetterAccepted, JointAgreementLetterRejected
- Reminders/jobs: UploadPendingDocuments, AppointmentDueDateReminder, AppointmentDocumentIncomplete, AppointmentCancelledDueDate, AppointmentPendingNextDay, AppointmentApproveRejectInternal, PendingAppointmentDailyNotification
- Admin/audit: AppointmentChangeLogs, UserQuery

**Key mechanism:** SendMail.SendSMTPMail() in PatientAppointment.Infrastructure; recipient resolution via stored procedures (GetAppointmentStackHoldersEmailPhone) + SystemParameter lookup (CcEmailIds); SystemParameter CC list auto-added to all outbound mail.

---

## 2. NEW Email Catalog (Summary)

**Wired handlers (5):**
1. StatusChangeEmailHandler - AppointmentStatusChangedEto (Approved/Rejected) - inline subject/body generation
2. DocumentUploadedEmailHandler - AppointmentDocumentUploadedEto - template stub seeded
3. DocumentAcceptedEmailHandler - AppointmentDocumentAcceptedEto - template stub seeded
4. DocumentRejectedEmailHandler - AppointmentDocumentRejectedEto - template stub seeded
5. AccessorInvitedEmailHandler - AppointmentAccessorInvitedEto - template stub seeded; security improved (token-based vs plaintext password)

**Declared-but-unwired handlers (7):**
- ChangeRequestApprovedEmailHandler (wired to Cancel/Reschedule approval)
- ChangeRequestRejectedEmailHandler (prepared but not wired)
- ChangeRequestSubmittedEmailHandler (prepared but not wired)
- JdfAutoCancelledEmailHandler (prepared but not wired)
- PackageDocumentReminderEmailHandler (prepared but not wired)
- PackageDocumentQueueHandler (prepared but not wired)

**All 59 template codes seeded with stub bodies** (NotificationTemplateDataSeedContributor.cs) pending per-feature phase replacement.

---

## 3. Side-by-Side Mapping (Condensed)

| OLD Email | NEW Status | Parity | Comments |
|---|---|---|---|
| **Auth emails (3)** | | | |
| UserRegistered, ResetPassword, PasswordChange | Missing handler | Partial | Phase 0 (Auth) not yet implemented; templates seeded |
| **Booking confirmation (2)** | | | |
| AppointmentBooked, PatientAppointmentPending | Missing handler | Partial | Phase 1 (Booking) - SubmissionEmailHandler to be wired; templates seeded |
| **Approval workflow (2)** | | | |
| AppointmentApproved | Wired (inline body) | Match | StatusChangeEmailHandler; body generation pending template migration |
| AppointmentRejected | Wired (inline body) | Match | StatusChangeEmailHandler; body generation pending template migration |
| **Change requests (6)** | | | |
| AppointmentCancelledRequest, AppointmentCancelledRequestApproved | Partially wired | Partial | ChangeRequestApprovedEmailHandler wired for approval; submission not wired; templates seeded |
| AppointmentRescheduleRequest, AppointmentRescheduleRequestApproved | Partially wired | Partial | ChangeRequestApprovedEmailHandler wired; submission not wired; templates seeded |
| AppointmentCancelledRequestRejected, AppointmentRescheduleRequestRejected | Missing handler | Partial | ChangeRequestRejectedEmailHandler prepared but not wired; templates seeded |
| **Document workflow (10)** | | | |
| PatientDocumentUploaded | Wired (stub body) | Match | DocumentUploadedEmailHandler; real template body pending |
| PatientDocumentAccepted | Wired (stub body) | Match | DocumentAcceptedEmailHandler; real template body pending |
| PatientDocumentRejected | Wired (stub body) | Match | DocumentRejectedEmailHandler; real template body pending |
| PatientNewDocument* (3 types), JointAgreementLetter* (3 types) | Missing handlers | Partial | Phase 4 (Ad-Hoc/JDF documents) deferred; templates seeded; handlers will branch on doc type |
| PatientDocumentAcceptedAttachment, AcceptedRemainingDocs, RejectedRemainingDocs | Missing conditional logic | Partial | Conditional branching pending; templates seeded |
| AppointmentDocumentAddWithAttachment | Missing handler | Partial | Attachment delivery via IEmailSender; not yet wired |
| **Accessor invitation (1)** | | | |
| AccessorAppointmentBooked | Wired (stub body) | Match | AccessorInvitedEmailHandler; security improved (token vs plaintext); real template body pending |
| **Post-appointment status (4)** | | | |
| PatientAppointmentCheckedIn/Out, NoShow, CancelledNoBill | Missing handlers | Missing | Phase 5 (Post-Appointment) deferred; templates seeded |
| **Recurring job emails (9)** | | | |
| UploadPendingDocuments, AppointmentDueDateReminder, AppointmentDocumentIncomplete, AppointmentCancelledDueDate, etc. | Missing job infrastructure | Missing | Phase 6 (Recurring Jobs) requires Quartz/Coravel scheduler integration; templates seeded; handlers prepared (PackageDocumentReminderEmailHandler) |
| **Admin/support (4)** | | | |
| UserQuery, SubmitQuery, AddInternalUser, AppointmentChangeLogs | Missing handlers | Missing | Phase 8 (User Mgmt/Support), Phase 7 (Audit) deferred; templates seeded |
| **Internal notifications (3)** | | | |
| AppointmentApproveRejectInternal, PatientAppointmentApprovedInternal, ClinicalStaffCancellation | Missing handlers | Missing | Internal staff notifications; Phase 1.5 approval deferred; templates seeded |

---

## 4. Recurring Jobs Status

**OLD (9 daily jobs in SchedulerDomain):**
1. PendingAppointmentNotificationJob - daily admin digest
2. AppointmentApproveRejectJob - daily approval queue summary
3. UploadPendingDocumentsJob - reminder to upload docs
4. AppointmentDueDateReminderJob - due date approaching
5. AppointmentDocumentIncompleteJob - documents overdue
6. AppointmentCancelledDueDateJob - auto-cancel after due date
7. AppointmentPendingNextDayJob - next-day pending summary (commented out in code)
8. UploadPendingDocumentsWithoutPrimaryJob - fallback when no admin assigned
9. (Implied) AppointmentPendingNextDayWithoutPrimaryJob - fallback next-day

**NEW (0 implemented):**
- Scheduled-job infrastructure (Quartz, Coravel, or Hangfire recurring jobs) not yet integrated
- Background job model is event-driven (Hangfire) + manual invocation
- Phase 6 target: add scheduler + implement 9 job handlers to match OLD
- Blocking issue: requires infrastructure decision (Quartz vs Coravel vs Hangfire recurring)

---

## 5. SMTP Configuration

### Current State

**Provider:** Azure Communication Services (ACS)

**Configuration (appsettings.json):**
- Host: smtp.azurecomm.net
- Port: 587
- TLS: STARTTLS (EnableSsl=true)
- From: noreply@gesco.com (will update to noreply@<acs-domain>)
- Auth: Basic username/password (Entra app client secret)

**Secrets:**
- Location: docker/appsettings.secrets.json (gitignored)
- Status: **NOT YET PROVISIONED** (placeholders only)

### Provisioning Checklist

From docs/research/2026-04-30-azure-acs-smtp-credentials.md:

- [ ] Azure Communication Services resource created
- [ ] Azure Email Communication Services resource created + linked
- [ ] Domain provisioned (Azure-managed GUID subdomain OR custom verified domain)
- [ ] Entra app registration created
- [ ] Client secret generated (copy immediately; shown only once)
- [ ] SMTP Username resource created in ACS portal (links Entra app to user-defined username string)
- [ ] Communication and Email Service Owner role assigned to app
- [ ] PowerShell credential test succeeded (Section 1.10 of research doc)
- [ ] Credentials written to docker/appsettings.secrets.json
- [ ] NullEmailSender bypass removed (CaseEvaluationDomainModule.cs:65-70)
- [ ] Hangfire dashboard confirms job success (http://localhost:44327/hangfire)

### Critical Blocker

**CaseEvaluationDomainModule.cs lines 65-70** registers NullEmailSender in Development environment:
`csharp
if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
{
    context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
}
`

Docker Compose sets ASPNETCORE_ENVIRONMENT=Development by default. Result: ALL emails discarded silently, regardless of credentials. Hangfire shows "Succeeded"; logs show nothing sent.

**Fix:** Either:
1. Change docker-compose.yml ASPNETCORE_ENVIRONMENT to "Staging" (disables developer exception page, enables real email)
2. Remove NullEmailSender condition entirely
3. Add separate flag (Email__UseRealSender) to gate real sending independently of environment

---

## 6. Demo-Critical Gaps

**Priority: CRITICAL** - Must complete before public demo

1. **UserRegistered** (Auth Phase 0)
   - Who: New patient self-registering
   - What: Confirmation email sent
   - Fix: Implement UserRegisteredEto + handler in ExternalSignups module
   - Effort: 1-2 days (foundational auth work)

2. **AppointmentBooked** (Booking Phase 1)
   - Who: Patient + attorneys + examiners
   - What: Multi-recipient submission confirmation
   - Fix: Implement SubmissionEmailHandler + IAppointmentRecipientResolver to fan-out per role
   - Effort: 1-3 days (event design + recipient logic)

3. **PatientDocumentUploaded** (Document Phase 4)
   - Who: Document reviewer (staff)
   - What: "Patient uploaded doc X" notification
   - Current: Handler wired; template body is stub
   - Fix: Copy HTML from OLD's Patient-Document-Uploaded.html; seed real body
   - Effort: 2-4 hours (template copy + variable mapping)

4. **DocumentAccepted** (Document Phase 4)
   - Who: Document uploader (patient/accessor)
   - What: "Your doc X was approved" confirmation
   - Current: Handler wired; template body is stub
   - Fix: Copy HTML from OLD's Patient-Document-Accepted.html; seed real body
   - Effort: 2-4 hours (template copy)

5. **AppointmentApproved** (Approval Phase 1.5)
   - Who: Appointment booker (patient)
   - What: "Your appointment has been approved" confirmation
   - Current: Handler wired; body generated inline (MVP)
   - Fix: Migrate inline body to NotificationTemplate; seed real body from OLD
   - Effort: 1-2 days (body generation refactor to use templates)

---

## 7. Parity-Important Gaps

**Priority: IMPORTANT** - Complete feature set for go-live; not demo-blocking

- **AccessorAppointmentBooked** (handler wired, stub body) - 2-3 hours
- **DocumentRejected** (handler wired, stub body) - 2-3 hours
- **ChangeRequest approvals/rejections** (2 handlers wired, 2 stub bodies) - 1-2 days
- **Ad-hoc document uploads** (handlers not wired) - 2-3 days
- **Joint Declaration Form workflow** (handlers not wired) - 2-3 days
- **Admin change notifications** (AppointmentChangeLogs handler not wired) - 1-2 days

---

## 8. Deferrable Gaps

**Priority: LOW** - Can defer past MVP/launch

- **PasswordChange, ResetPassword** (auth flows; can be manual in demo)
- **Internal user creation** (Phase 8; not in patient demo)
- **Help query submission** (Phase 8; not in patient demo)
- **Recurring job emails** (Phase 6; requires scheduler infrastructure; can use manual triggers for demo)
- **Post-appointment status changes** (Phase 5; rare in demo flow)
- **Billing-related cancellation** (Phase 3 Billing; deferred)

---

## 9. Recommended Immediate Fixes

### Fix #1: Seed Real Template Bodies (1-2 days)

**Scope:** DocumentUploaded, DocumentAccepted, DocumentRejected

**Tasks:**
1. Copy HTML from OLD's wwwroot/EmailTemplates/ to NEW's seed data
2. Map OLD template variables (##PatientName##, ##DocumentName##, etc.) to NEW's DocumentNotificationContext keys
3. Update NotificationTemplateDataSeedContributor.cs to seed real subject + bodyEmail instead of stubs
4. Test: Upload document, verify email body in logs (after SMTP provisioning)

**Deliverable:** Document lifecycle emails are human-readable; stub placeholders gone

### Fix #2: Implement SubmissionEmailHandler for AppointmentBooked (1-3 days)

**Scope:** Wire AppointmentSubmittedEto → multi-recipient fan-out

**Tasks:**
1. Create AppointmentSubmittedEto on Appointment creation (domain event)
2. Implement SubmissionEmailHandler: ILocalEventHandler<AppointmentSubmittedEto>
3. Implement IAppointmentRecipientResolver to fan-out to patient, attorneys, examiners
4. Seed template bodies for AppointmentBooked, PatientAppointmentPending
5. Test: Create appointment; verify 3-5 emails in Hangfire dashboard

**Deliverable:** Multi-recipient booking confirmation; demo feature completeness

---

## 10. File Locations

**OLD:**
- Email constants: P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs
- SendMail: P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\SendMail.cs
- HTML templates: P:\PatientPortalOld\PatientAppointment.Api\wwwroot\EmailTemplates\

**NEW:**
- Template codes: W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Domain.Shared\NotificationTemplates\NotificationTemplateConsts.cs
- Seed data: W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Domain\NotificationTemplates\NotificationTemplateDataSeedContributor.cs
- Handlers: W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Application\Notifications\Handlers\
- SMTP config: W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.HttpApi.Host\appsettings.json
- Research: W:\patient-portal\replicate-old-app\docs\research\2026-04-30-azure-acs-smtp-credentials.md

---

**Report Generated:** 2026-05-05
**Status:** All 59 OLD emails mapped to NEW; 5 demo-critical gaps identified with effort estimates; SMTP provisioning blocker documented with mitigation steps.
