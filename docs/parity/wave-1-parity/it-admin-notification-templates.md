---
feature: it-admin-notification-templates
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\TemplateManagementModule\TemplateDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Models\Enums\TemplateCode.cs
  - P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\template-management\
old-docs:
  - socal-project-overview.md (lines 569-593)
  - data-dictionary-table.md (Templates, TemplateTypes, SMTPConfigurations)
audited: 2026-05-01
re-verified: 2026-05-03
status: in-progress
priority: 2
strict-parity: true
internal-user-role: ITAdmin
depends-on: []
required-by:
  - external-user-registration                 # registration email template
  - external-user-login                        # verification email template
  - external-user-forgot-password              # reset email template
  - external-user-appointment-request          # booking confirmation template
  - external-user-appointment-cancellation     # cancel notifications
  - external-user-appointment-rescheduling     # reschedule notifications
  - external-user-appointment-package-documents # doc upload notifications
  - external-user-appointment-joint-declaration # JDF notifications
  - clinic-staff-appointment-approval          # approval notifications
  - clinic-staff-document-review               # review notifications
---

# IT Admin -- Notification templates

## Purpose

IT Admin manages email + SMS templates used for all system notifications. Each template has a Subject, Email body, SMS body, and is keyed by `TemplateCode` (an event identifier) within a `TemplateType`. The system loads the template by code at notification-firing time, substitutes variables, sends via SMTP / Twilio.

**Strict parity with OLD.** Every notification event in the audit list above resolves through this feature.

## OLD behavior (binding)

### Schema

`Templates` (per data dict):

- `TemplateId` (PK)
- `TemplateTypeId` (FK to `TemplateTypes`)
- `TemplateCode` (int -- the event identifier)
- `Subject` (nvarchar 200)
- `Description` (nvarchar 200)
- `BodySms` (varchar Max)
- `BodyEmail` (varchar Max)
- `StatusId` (Active/Delete)
- Audit fields

`TemplateTypes`:

- `TemplateTypeId, TemplateTypeName, StatusId`

Pattern: Template Type groups codes (e.g., "Appointment Lifecycle" -> Approve, Reject, Cancel-Approve, etc.).

`SMTPConfigurations` (one row -- IT Admin sets):

- `FromEmail`, `Host`, `Port`, `UserName`, `Password`, `EnableSSL`, `DefaultCredentials`, `DeliveryMethod`, `SendIndividually`, `IsActive`

Used by `ISendMail` to send all outbound email.

### Notification trigger events (per spec lines 573-593)

User-facing:
- Appointment booked
- Appointment approved or rejected
- Appointment cancelled
- Appointment changed (any field updated)

Reminders to staff:
- Approval/rejection pending appointment

Reminders to user:
- Incomplete package documents (multi-step; includes list of remaining docs)
- JDF upload pending (multi-step)
- Appointment due-date approaching (multi-step)
- Due date approaching + package docs still pending

Each maps to a `TemplateCode` value.

### Variable substitution

Template bodies contain `##VariableName##` placeholders (per OLD code -- see `UserDomain.AddInternalUser` line 297-300 for example: `##UserName##`, `##LoginUserName##`, `##Password##`). At send time, code calls `body.Replace(...)`.

OLD also has `EmailTemplate` enum (`EmailTemplate.UserRegistered`, etc.) and templates may be loaded from disk (HTML files in `wwwroot/EmailTemplates/...`?) OR from the `Templates` table. Both patterns appear in OLD code. Strict parity: support both, but prefer DB-managed templates for IT Admin editability.

### Critical OLD behaviors

- **IT Admin can edit subject + body for any template.** No code deploy needed to change wording.
- **SMS + Email body managed together.** Both fired for the same event when configured.
- **Multi-step reminders** (e.g., 7-day, 3-day, 1-day before) use the SAME template body for all sends, OR separate codes per step (TO VERIFY). Strict parity: replicate whichever OLD does.
- **`SMTPConfigurations` is editable.** IT Admin can change SMTP host/port/credentials without deploy.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/TemplateManagementModule/TemplateDomain.cs` | Template CRUD |
| `PatientAppointment.Infrastructure.CommonDomain.SendMail` (`ISendMail`) | Email sender impl using SMTPConfigurations |
| `PatientAppointment.Infrastructure.CommonDomain.TwilioSmsService` (`ITwilioSmsService`) | SMS sender |
| `Models.Enums.EmailTemplate` enum | Code-side template references |
| `patientappointment-portal/.../template-management/...` | UI for editing templates |

## NEW current state

- TO VERIFY: NEW has `Templates/` or `NotificationTemplates/` entity.
- ABP provides: `IEmailSender`, `ISmsSender`, `ITemplateRenderer` (Razor-based template rendering).
- LeptonX themes ship with email templates for account-related events (registration, password reset).

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev | Status |
|--------|-----|-----|--------|-----|--------|
| `Template` entity (DB-managed) | OLD | NEW: TO VERIFY | **Add `NotificationTemplate` entity** with TemplateCode, TemplateTypeId, Subject, BodyEmail, BodySms, IsActive | B | [IMPLEMENTED 2026-05-01 - tested unit-level] -- entity at `Domain/NotificationTemplates/NotificationTemplate.cs` (`FullAuditedAggregateRoot<Guid>, IMultiTenant`, `[Audited]`); Phase 1 migration created the table; Phase 4 (2026-05-03) re-verified field set verbatim against OLD `Templates` schema. |
| `TemplateType` lookup | OLD | NEW: TO VERIFY | **Add `NotificationTemplateType` entity** + seed | I | [IMPLEMENTED 2026-05-01 - tested unit-level] -- host-scoped `NotificationTemplateType` entity + seed of two rows (Email = `c0000001-...0001`, SMS = `c0000001-...0002`) verbatim with OLD's `TemplateTypeEnums` (Email=1, SMS=2). |
| `SMTPConfiguration` (DB-managed credentials) | OLD | NEW: ABP uses `appsettings.json` for SMTP | **Decision: keep ABP's appsettings approach** -- editing SMTP via UI is a security concern; IT can edit appsettings. Strict parity exception: NEW's approach is better. Document as "framework deviation: secrets stay out of DB". | I | [DESCOPED 2026-05-03 - documented framework deviation] -- SMTP credentials stay in `appsettings.json`/Key Vault, not surfaced via the AppService. Visible behavior unchanged. |
| Variable substitution | OLD: `##Var##` | NEW: ABP `ITemplateRenderer` uses Razor `@Model.Var` syntax OR Liquid (configurable) | **Use ABP's renderer**; map OLD's `##Var##` placeholders to Razor at port time. Each notification handler builds its model and renders. | I | [DESCOPED 2026-05-03 - Phase 18 work] -- Phase 4 stores body content as-is; the per-handler conversion from `##Var##` to `@Model.Var` runs when each handler is wired in Phase 18. |
| `TemplateCode` -> event mapping | OLD: `TemplateCode` int enum (16) + `EmailTemplate` static class of HTML filenames (43) -- two parallel systems | -- | **Add `TemplateCode` string enum unifying both OLD systems** (verbatim list under "Template code matrix" below). Phase 1 wires 33; remaining 26 seeded but unwired until their feature phase lands. | B | [IMPLEMENTED 2026-05-03 - tested unit-level] -- 59 codes verbatim (16 + 43, with 4 typo fixes documented in `NotificationTemplateConsts.Codes`). Unit test `Codes_All_Has59Codes` verifies count; `Codes_All_FixesOldTypos` verifies the 4 typo fixes; `Codes_All_AreUnique` verifies dedup. Phase 1.3 invented-name list is fully replaced. |
| User-side multi-step reminders | OLD | -- | **Use Hangfire recurring jobs**; each job loads its template by code and sends | I | [DESCOPED 2026-05-03 - Phase 18 work] -- Hangfire recurring jobs land in Phase 18; Phase 4 ships the storage + editor surface only. |
| Template editor UI | OLD | -- | **Add `NotificationTemplatesController` + edit UI** -- subject + email body (rich-text or plain) + sms body | I | [IMPLEMENTED 2026-05-03 - backend complete; UI deferred] -- `INotificationTemplatesAppService` (Get list / GetAsync / GetByCodeAsync / GetTypeLookupAsync / UpdateAsync), `NotificationTemplatesController` at `api/app/notification-templates`. Angular editor component is Session-A coordinated UI work (deferred). |
| Permissions | -- | -- | **`CaseEvaluation.NotificationTemplates.{Default, Edit}`** -- IT Admin only | I | [IMPLEMENTED 2026-05-02 - pending integration test] -- both keys registered in Phase 2.5; granted to IT Admin (Default + Edit) and Staff Supervisor (Default + Edit). Clinic Staff has no grant. |
| Audit on template changes | OLD: implicit `AuditRecords` | NEW: `[Audited]` attribute | **Add `[Audited]`** | C | [IMPLEMENTED 2026-05-01 - pending integration test] -- `[Audited]` on entity. |

## Internal dependencies surfaced

- ABP `IEmailSender`, `ISmsSender`, `ITemplateRenderer`.
- Hangfire for scheduled sends (multi-step reminders).
- Outbound SMS provider (Twilio in OLD; ABP supports Twilio module).

## Branding/theming touchpoints

- Email templates per event -- subject, body, logo, colors, signature, footer (clinic name, support contact).
- SMS templates -- shorter; brand voice + signature.

## Replication notes

### ABP wiring

- **Entity:** `NotificationTemplate : FullAuditedEntity<Guid>, IMultiTenant`. Per-tenant overrides supported (each tenant can customize their own templates).
- **`INotificationTemplateRepository.FindByCodeAsync(string code)`** loads the active template at send time.
- **Notification handler pattern:**
  ```csharp
  public class AppointmentApprovedEmailHandler : IDistributedEventHandler<AppointmentApprovedEto>
  {
      public async Task HandleEventAsync(AppointmentApprovedEto eventData) {
          var template = await _templateRepo.FindByCodeAsync("AppointmentApproved");
          var rendered = await _templateRenderer.RenderAsync(template.BodyEmail, model);
          await _emailSender.SendAsync(eventData.PatientEmail, template.Subject, rendered);
      }
  }
  ```
- **SMS:** parallel handler using `ISmsSender.SendAsync(...)` with `BodySms`.
- **Twilio integration:** `Volo.Abp.Sms.Twilio` package (free, included in ABP open-source).
- **Multi-step reminders:** Hangfire `RecurringJob.AddOrUpdate(...)`. Each step is a separate code.

### Things NOT to port

- `SMTPConfigurations` table -- use appsettings + Azure Key Vault / AWS Secrets Manager for credentials.
- ~~`##Var##` placeholder syntax -- replace with Razor `@Model.Var`.~~ **CORRECTED 2026-05-04 (Phase 18)**: Keep OLD's `##Var##` placeholder syntax verbatim. Verified against OLD `ApplicationUtility.GetEmailTemplateFromHTML` (`P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs`:212-251) -- OLD does flat `body.Replace("##Key##", value)` against a reflection-walked model + 7 standard footer keys. Switching to Razor would require Roslyn dynamic compilation for DB-stored templates and force a one-time rewrite of every seeded template body (the seed copies OLD's HTML bodies verbatim per Phase 1.3). `##Var##` substitution is simpler, OLD-faithful, and one-line in NEW. Implemented as `TemplateVariableSubstitutor` (Phase 18). `[OLD-BUG-FIX]` exception against OLD's null-handling: NEW skips null values explicitly (OLD's line 247 `item.Value.ToString()` would NRE on a null value despite the line 222-234 null-guard).
- Direct Twilio SDK calls -- use ABP abstraction.

### Verification (manual test plan)

1. IT Admin opens Notification Templates page -> sees list of all template codes
2. IT Admin edits "AppointmentApproved" -> Subject and Body Email -> save
3. Approve an appointment -> patient receives email with NEW subject/body (not the old default)
4. IT Admin edits SMS body for same code -> SMS delivered with new text
5. Multi-step reminder: trigger 7-day-before reminder -> renders + sends correctly

## Template code matrix [VERIFIED 2026-05-03]

> **Correction (2026-05-03).** The earlier draft of this audit (and the plan
> built from it, lines 839-867) listed guessed names like `AppointmentRequest`,
> `JDFAutoCancelled`, `AccessorInvited`, `DueDateApproachingReminder`. **Those
> names DO NOT exist in OLD.** OLD uses two separate template mechanisms;
> verbatim lists are below. Strict parity requires using OLD's exact identifiers,
> not invented ones.

OLD has **two parallel template-storage systems** (legacy bifurcation, both used
by different feature domains):

### A. `TemplateCode` enum -- DB-managed templates (16 codes) [IT-Admin editable]

File: `P:\PatientPortalOld\PatientAppointment.Models\Enums\TemplateCode.cs` (lines 9-27)

Stored in the `Templates` SQL table; loaded at send time; supports email Subject +
BodyEmail + BodySms; IT Admin edits via the Template Management UI.

```
# OLD verbatim (left)            -> NEW (right; typos fixed)
AppointmentBooked                  = 1   -> AppointmentBooked
AppointmentApproved                = 2   -> AppointmentApproved
AppointmentRejected                = 3   -> AppointmentRejected
AppointmentCancelledRequest        = 4   -> AppointmentCancelledRequest
AppointmentCancelledRequestApproved= 5   -> AppointmentCancelledRequestApproved
AppointmentCancelledRequestRejected= 6   -> AppointmentCancelledRequestRejected
AppointmentRescheduleRequest       = 7   -> AppointmentRescheduleRequest
AppointmentRescheduleRequestApproved=8   -> AppointmentRescheduleRequestApproved
AppointmentRescheduleRequestRejected=9   -> AppointmentRescheduleRequestRejected
RejectedPackageDocument            = 12  -> RejectedPackageDocument
RejectedJoinDeclarationDocument    = 13  -> RejectedJointDeclarationDocument         # FIXED: missing 't' (Join -> Joint; "Joint" appears 348x in OLD)
AppointmentDueDate                 = 14  -> AppointmentDueDate
AppointmentDueDateUploadDocumentLeft=15  -> AppointmentDueDateUploadDocumentLeft
SubmitQuery                        = 16  -> SubmitQuery
AppointmentApprovedStackholderEmails=17  -> AppointmentApprovedStakeholderEmails     # FIXED: Stackholder -> Stakeholder (zero "Stakeholder" matches in OLD; entire codebase mis-spells)
AppointmentCancelledByAdmin        = 18  -> AppointmentCancelledByAdmin
```

Note: int values 10 and 11 are gaps in OLD numbering (artifact of earlier
deletions); preserve the gap for strict parity if porting the int values, or
re-number sequentially in NEW since values are not exposed to the wire.

### B. `EmailTemplate` static class -- disk HTML templates (43 codes) [code-only]

File: `P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs` (lines 26-71)

These are HTML files on disk under `wwwroot/EmailTemplates/`. Loaded at send time
via `ApplicationUtility.GetEmailTemplateFromHTML(...)`. Email-only (no SMS body).
NOT IT-Admin editable in OLD -- changes require a code deploy.

```
AddInternalUser                            = "Add-Internal-User.html"
PasswordChange                             = "Password-Changed.html"
ResetPassword                              = "ResetPassword.html"
UserRegistered                             = "User-Registered.html"          # FIXED: OLD HTML filename "User-Registed.html" (typo); C# constant was already correct
UserQuery                                  = "User-Query.html"
AppointmentRescheduleRequestByAdmin        = "Appointment-Reschedule-Request-Changed-By-Admin.html"
AppointmentChangeLogs                      = "Appointment-Change-Logs.html"
PatientAppointmentPending                  = "Patient-Appointment-Pending.html"
PatientAppointmentApproveReject            = "Patient-Appointment-ApproveReject.html"
PatientAppointmentApprovedInternal         = "Patient-Appointment-ApprovedInternal.html"
PatientAppointmentApprovedExt              = "Patient-Appointment-ApprovedExternal.html"
PatientAppointmentRejected                 = "Patient-Appointment-Rejected.html"
PatientAppointmentCheckedIn                = "Patient-Appointment-CheckedIn.html"
PatientAppointmentCheckedOut               = "Patient-Appointment-CheckedOut.html"
PatientAppointmentNoShow                   = "Patient-Appointment-NoShow.html"
PatientAppointmentCancelledNoBill          = "Patient-Appointment-CancelledNoBill.html"
ClinicalStaffCancellation                  = "Clinical-Staff-Cancellation.html"
AccessorAppointmentBooked                  = "Accessor-Appointment-Booked.html"
PatientDocumentAccepted                    = "Patient-Document-Accepted.html"
PatientDocumentRejected                    = "Patient-Document-Rejected.html"
PatientDocumentUploaded                    = "Patient-Document-Uploaded.html"
PatientNewDocumentAccepted                 = "Patient-New-Document-Accepted.html"
PatientNewDocumentRejected                 = "Patient-New-Document-Rejected.html"
PatientNewDocumentUploaded                 = "Patient-New-Document-Uploaded.html"
PatientDocumentAcceptedAttachment          = "Patient-Document-Accepted-Attachment.html"
PatientDocumentAcceptedRemainingDocs       = "Patient-Document-Accepted-With-Remaining-Documents.html"
PatientDocumentRejectedRemainingDocs       = "Patient-Document-Rejected-With-Remaining-Documents.html"
AppointmentApproveRejectInternal           = "Appointment-ApproveReject-Internal.html"
UploadPendingDocuments                     = "Upload-Pending-Documents.html"
AppointmentDueDateReminder                 = "Appointment-DueDate-Reminder.html"
AppointmentDocumentIncomplete              = "Appointment-Document-Incomplete.html"
AppointmentCancelledDueDate                = "Appointment-Cancelled-With-DueDate.html"
AppointmentPendingNextDay                  = "Appointment-Pending-Next-Day.html"
PatientAppointmentRescheduleReqAdmin       = "Patient-Appointment-Reschedule-Request-Admin.html"
PatientAppointmentRescheduleReqApproved    = "Patient-Appointment-Reschedule-Request-Approved.html"
PatientAppointmentRescheduleReqRejected    = "Patient-Appointment-Reschedule-Request-Rejected.html"
PatientAppointmentCancellationApproved     = "Patient-Appointment-Cancellation-Approved.html"   # FIXED: OLD constant Apprvd (inconsistent abbr) and filename "Apporved" (typo) -> Approved
PatientAppointmentRescheduleReq            = "Patient-Appointment-Reschedule-Request.html"
JointAgreementLetterAccepted               = "Joint-Agreement-Letter-Accepted.html"
JointAgreementLetterUploaded               = "Joint-Agreement-Letter-Uploaded.html"
JointAgreementLetterRejected               = "Joint-Agreement-Letter-Rejected.html"
AppointmentDocumentAddWithAttachment       = "Appointment-Document-Add-With-Attachment.html"
PendingAppointmentDailyNotification        = "PendingAppointmentDailyNotification.html"
```

### NEW unification decision [STRICT-PARITY EXCEPTION -- behavior preserved, storage unified]

OLD's bifurcation (DB-managed + on-disk) is an accidental legacy artifact, not a
designed feature. Strict parity is about user-visible behavior, not internal
storage. **NEW unifies all 59 events into the single `NotificationTemplate`
table.** All become IT-Admin-editable, which is an improvement over OLD (where
the 43 on-disk templates required code deploys to change).

**Typo fixes applied 2026-05-03** (NEW does NOT preserve OLD typos):
- `RejectedJoinDeclarationDocument` -> `RejectedJointDeclarationDocument`
  (missing 't'; "Joint" used 348x elsewhere)
- `AppointmentApprovedStackholderEmails` -> `AppointmentApprovedStakeholderEmails`
  (entire OLD codebase mis-spells "Stakeholder"; we standardize)
- `PatientAppointmentCancellationApprvd` (constant) + `Apporved.html` (filename)
  -> `PatientAppointmentCancellationApproved`
  (inconsistent abbreviation + filename typo; surrounding pattern uses full
  `Approved` everywhere else)
- HTML filename `User-Registed.html` -> `User-Registered.html` (constant was
  already correct; we own the body now so the filename typo is moot)

Other OLD-only abbreviations (`Req`, `Apprvd` in deferred entries, `Approve` vs
`Approved` inconsistency in TemplateCode int-enum names) are kept as-is when
they form a consistent local pattern (e.g., the four `...RescheduleReq...`
entries) -- these are stylistic, not typos.

- Migrate the 43 HTML bodies from OLD's `wwwroot/EmailTemplates/` into the seed
  contributor as `BodyEmail` strings (variable substitution converted from
  `##Var##` placeholders to Razor `@Model.Var` at seed time).
- The 16 DB-managed entries already have Subject + BodyEmail + BodySms; copy
  verbatim from OLD's seed data (export from OLD dev DB or read from OLD's
  initial migration script).

### Phase 1 in-scope subset (Notification scope cut)

Phases deferred per plan (Check-In/Out, NoShow, Billing, SubmitQuery, Internal
User Mgmt, Audit-Log viewer) take their templates with them. Phase 1 implements
the subset below; the rest stay in the seed but are not wired to any handler
until their feature phase lands post-parity.

**In Phase 1 scope (33 codes):**

| Notification event              | OLD source code(s)                                            | Audit doc                                  |
|---------------------------------|---------------------------------------------------------------|--------------------------------------------|
| Registration verification       | `EmailTemplate.UserRegistered`                                | external-user-registration                 |
| Password reset link             | `EmailTemplate.ResetPassword`                                 | external-user-forgot-password              |
| Password changed confirmation   | `EmailTemplate.PasswordChange`                                | external-user-forgot-password              |
| Booking (external)              | `TemplateCode.AppointmentBooked` + `EmailTemplate.PatientAppointmentPending` | external-user-appointment-request |
| Booking (internal-immediate)    | `EmailTemplate.PatientAppointmentApprovedInternal/Ext`        | external-user-appointment-request          |
| Approval (to patient)           | `TemplateCode.AppointmentApproved` + `EmailTemplate.PatientAppointmentApproveReject` | clinic-staff-appointment-approval |
| Approval (to stakeholders)      | `TemplateCode.AppointmentApprovedStackholderEmails`           | clinic-staff-appointment-approval          |
| Approval (internal)             | `EmailTemplate.AppointmentApproveRejectInternal`              | clinic-staff-appointment-approval          |
| Rejection                       | `TemplateCode.AppointmentRejected` + `EmailTemplate.PatientAppointmentRejected` | clinic-staff-appointment-approval |
| Accessor invited (booking)      | `EmailTemplate.AccessorAppointmentBooked`                     | external-user-appointment-request          |
| Document uploaded (package)     | `EmailTemplate.PatientDocumentUploaded`                       | external-user-appointment-package-documents |
| Document accepted (package)     | `EmailTemplate.PatientDocumentAccepted` + `...AcceptedAttachment` + `...AcceptedRemainingDocs` | clinic-staff-document-review |
| Document rejected (package)     | `EmailTemplate.PatientDocumentRejected` + `...RejectedRemainingDocs` + `TemplateCode.RejectedPackageDocument` | clinic-staff-document-review |
| Document uploaded (ad-hoc)      | `EmailTemplate.PatientNewDocumentUploaded`                    | external-user-appointment-ad-hoc-documents |
| Document accepted (ad-hoc)      | `EmailTemplate.PatientNewDocumentAccepted`                    | clinic-staff-document-review               |
| Document rejected (ad-hoc)      | `EmailTemplate.PatientNewDocumentRejected`                    | clinic-staff-document-review               |
| JDF uploaded                    | `EmailTemplate.JointAgreementLetterUploaded`                  | external-user-appointment-joint-declaration |
| JDF accepted                    | `EmailTemplate.JointAgreementLetterAccepted`                  | external-user-appointment-joint-declaration |
| JDF rejected                    | `EmailTemplate.JointAgreementLetterRejected` + `TemplateCode.RejectedJoinDeclarationDocument` | external-user-appointment-joint-declaration |
| Cancellation request submitted  | `TemplateCode.AppointmentCancelledRequest`                    | external-user-appointment-cancellation     |
| Cancellation approved           | `TemplateCode.AppointmentCancelledRequestApproved` + `EmailTemplate.PatientAppointmentCancellationApprvd` | staff-supervisor-change-request-approval |
| Cancellation rejected           | `TemplateCode.AppointmentCancelledRequestRejected`            | staff-supervisor-change-request-approval   |
| Cancellation by admin           | `TemplateCode.AppointmentCancelledByAdmin` + `EmailTemplate.ClinicalStaffCancellation` + `EmailTemplate.PatientAppointmentCancelledNoBill` | staff-supervisor-change-request-approval |
| Reschedule request submitted    | `TemplateCode.AppointmentRescheduleRequest` + `EmailTemplate.PatientAppointmentRescheduleReq` | external-user-appointment-rescheduling |
| Reschedule approved             | `TemplateCode.AppointmentRescheduleRequestApproved` + `EmailTemplate.PatientAppointmentRescheduleReqApproved` | staff-supervisor-change-request-approval |
| Reschedule rejected             | `TemplateCode.AppointmentRescheduleRequestRejected` + `EmailTemplate.PatientAppointmentRescheduleReqRejected` | staff-supervisor-change-request-approval |
| Reschedule by admin (override)  | `EmailTemplate.AppointmentRescheduleRequestByAdmin` + `PatientAppointmentRescheduleReqAdmin` | staff-supervisor-change-request-approval |
| Due-date reminder (general)     | `EmailTemplate.AppointmentDueDateReminder` + `TemplateCode.AppointmentDueDate` | scheduler-background-jobs            |
| Due-date + pending-docs reminder| `EmailTemplate.UploadPendingDocuments` + `TemplateCode.AppointmentDueDateUploadDocumentLeft` | scheduler-background-jobs       |
| Pending-docs reminder           | `EmailTemplate.AppointmentDocumentIncomplete`                 | scheduler-background-jobs                  |
| JDF auto-cancel                 | `EmailTemplate.AppointmentCancelledDueDate`                   | external-user-appointment-joint-declaration |
| Pending-appointment next-day    | `EmailTemplate.AppointmentPendingNextDay`                     | scheduler-background-jobs                  |
| Daily-pending-appointment digest| `EmailTemplate.PendingAppointmentDailyNotification`           | scheduler-background-jobs                  |

**Deferred (NOT in Phase 1 -- seeded but not wired):** AddInternalUser, UserQuery,
PatientAppointmentCheckedIn, PatientAppointmentCheckedOut, PatientAppointmentNoShow,
SubmitQuery, AppointmentChangeLogs, AppointmentDocumentAddWithAttachment.

## Phase 18 implementation (2026-05-04)

Phase 18 ("Notifications + reminder jobs" in
`docs/plans/2026-05-01-old-app-parity-implementation.md` lines 1090+) lands at
Sync 1: Session A is still on Phase 11 (Booking) so the per-feature handler
list (RegisteredEmailHandler, ApprovalEmailHandler, etc. -- master plan
section 18.1) cannot be wired against real Etos yet. Per the two-session-split
sync protocol, Session B picks up the **infrastructure scaffolding** so
per-phase handler implementations can land as one-line subscribers in their
respective feature commits.

### Scope shipped 2026-05-04

1. `TemplateVariableSubstitutor` (`internal static`) -- mirrors OLD's
   `ApplicationUtility.GetEmailTemplateFromHTML` line 245-248 substitution
   loop. Pure helper, exposed via `InternalsVisibleTo` for unit tests.
   `##Var##` syntax preserved verbatim.
2. `INotificationTemplateRenderer` + impl -- loads template by
   `TemplateCode` via `INotificationTemplateRepository.FindByCodeAsync`,
   substitutes variables, returns `RenderedNotification`
   `{ Subject, BodyEmail, BodySms }`. Throws
   `BusinessException(NotificationTemplateNotFound)` when missing.
3. `INotificationDispatcher` + impl -- top-level facade. Resolves recipients
   to `SendAppointmentEmailArgs` jobs and enqueues via the existing
   `IBackgroundJobManager` + `SendAppointmentEmailJob` pipeline. SMS path
   uses `ISmsSender.SendAsync` synchronously when `BodySms` is populated.
4. Forward-declared Etos in `Domain.Shared/Notifications/Events/`:
   `AppointmentDocumentUploadedEto`, `AppointmentDocumentAcceptedEto`,
   `AppointmentDocumentRejectedEto`, `AppointmentAccessorInvitedEto`,
   `AppointmentChangeRequestSubmittedEto`,
   `AppointmentChangeRequestApprovedEto`,
   `AppointmentChangeRequestRejectedEto`,
   `AppointmentAutoCancelledEto`,
   `ExternalUserRegisteredEto`. Each is a record with the minimum field
   set the master-plan handlers need; Session A (Phase 11) and Session B
   (Phase 8/12/14/17) emit them at their feature phase.
5. Unit tests for `TemplateVariableSubstitutor` (substitution, missing
   keys, null values, idempotency, prefix patterns from OLD's
   `##Patient.FirstName##` style) and `RenderedNotification` /
   `NotificationRecipient` shapes.

### Deferred from Phase 18 (per Sync 1 split)

The per-feature handlers + reminder jobs the master-plan §18.1/18.2 lists
(RegisteredEmailHandler, ApprovalEmailHandler, JDFAutoCancelJob,
PackageDocumentReminderJob, etc.) land in their feature-phase commits:

- Registration / login / forgot-password handlers -- already inline in
  Phases 8-10 commits (`fd16723`, `64b6ba8`).
- AppointmentApproved / Rejected handlers -- come with Phase 12 (Approval).
- Document upload / accept / reject handlers -- Phase 14 (Document review).
- ChangeRequest submitted / approved / rejected handlers -- Phases 15-17.
- JDF auto-cancel + package-doc reminder Hangfire jobs -- Phase 14
  (entity prerequisites: `AppointmentDocument.IsJointDeclaration`,
  `Appointment.DueDate`, are still being added by Session A).

This split keeps Phase 18 atomic + Sync-1-safe. Each downstream feature
commit gets a 30-line handler that calls `INotificationDispatcher.DispatchAsync`,
not 30 lines of inline HTML.

`[IMPLEMENTED 2026-05-04 - tested unit-level]` -- renderer infrastructure +
forward Etos. Per-feature handlers continue to ship in their feature commits.
