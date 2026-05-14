# Email handlers — demo-critical lifecycle (OLD research + NEW gap + plan)

Verified 2026-05-05. Source citations are OLD `path:line` (read-only at
`P:\PatientPortalOld\`). NEW citations are this repo.

## Architecture in OLD vs NEW

OLD has no event bus -- emails fire from inside the domain method that
mutates the appointment. Two reusable helpers do the heavy lifting:

| OLD helper | What it does | Path |
|---|---|---|
| `GetAppointmentStackHoldersEmailPhone(appointmentId)` | Returns `EmailList` (semicolon-joined recipients) + `PhoneList`. Stored proc `spm.spAppointmentStackHoldersEmailAndPhone`. | `AppointmentDomain.cs:1052-1057` |
| `vEmailSender` view | Per-appointment denormalized view: PatientFirstName/LastName/Email, ConfirmationNumber, AvailableDate, FromTime, ToTime, CancellationReason, RejectionNotes, InternalUserComments. | referenced at `AppointmentDomain.cs:899` |
| `SendMail.SendSMTPMail(to, subject, body, [cc])` | Raw SMTP send with optional CC. Re-tried with backoff via Hangfire-equivalent. | `Infrastructure/SmtpUtility.cs` (not in this audit; signature inferred from call sites) |

NEW has the equivalent of both helpers already wired:

| NEW helper | Replacement for OLD | Path |
|---|---|---|
| `IAppointmentRecipientResolver.ResolveAsync(id, kind)` | `GetAppointmentStackHoldersEmailPhone`. Returns one `SendAppointmentEmailArgs` per addressable stakeholder, deduplicated, with role tags. | `Domain/Appointments/Notifications/AppointmentRecipientResolver.cs` |
| `INotificationDispatcher.DispatchAsync(code, recipients, vars, contextTag)` | Subject + body lookup from `NotificationTemplate` row (per-tenant, IT-Admin editable in NEW), `##Var##` substitution, fan-out to one Hangfire job per recipient. | `Application/Notifications/NotificationDispatcher.cs` |
| `AppointmentRecipientResolver.AddIfPresent` | OLD's "skip if email empty" dedupe + log. | same file |
| `AppointmentSubmittedEto`, `AppointmentStatusChangedEto`, `AppointmentApprovedEto`, `AppointmentRejectedEto` | OLD's inline-after-`Commit` calls. | `Domain/Appointments/Events/` |

Both helpers exist; they're just not wired for the demo-critical templates.
The remaining work is per-handler glue.

---

## 1. UserRegistered (registration verification)

### OLD trigger

`UserDomain.SendEmail(user, isNewUser=true)` at `UserModule/UserDomain.cs:314-333`,
called from `UserDomain.Add(user)` at the end of registration. Synchronous;
in the same transaction as the user insert.

### OLD recipient

Single recipient: `user.EmailId` (the freshly-registered user). No CC.

### OLD subject

`"Your have registered successfully - Patient Appointment portal"` (typo
"Your"; we fix to "You" in NEW per CLAUDE.md "Clear bug -- fix it" rule).

### OLD variables

```
PatientFirstName = user.FirstName
PatientLastName  = user.LastName
URL              = clientUrl + "/verify-email/" + user.UserId + "?query=" + user.VerificationCode
```

### OLD body

`User-Registed.html` (typo in filename only; constant is `UserRegistered`).
Uses tokens `##PatientFirstName##` (only first name; last name is in the
viewmodel but unused in the body) and `##URL##`. Plus the brand tokens
(`##CompanyLogo##`, `##lblHeaderTitle##`, `##lblFooterText##`,
`##Email##`, `##Skype##`, `##ph_US##`, `##imageInByte##`).

### NEW state

- Template body + subject: seeded (commit `81d6563`).
- Eto: **not published yet.** `ExternalSignupAppService.RegisterAsync`
  creates the IdentityUser via `_userManager.CreateAsync` and assigns the
  role; no event fires.
- Handler: **does not exist.**
- Verification token: ABP's `IdentityUser.SetEmailConfirmed` flow is the
  NEW-side equivalent of OLD's `VerificationCode` Guid. Email-confirm
  tokens are generated via `_userManager.GenerateEmailConfirmationTokenAsync(user)`
  (already used elsewhere). The link target is the AuthServer's
  `/Account/ConfirmEmail` page (ABP's stock).

### Gap to close

1. Publish `UserRegisteredEto { UserId, TenantId, Email, FirstName, RoleName }`
   from `ExternalSignupAppService.RegisterAsync` after the user + role +
   downstream entity (Patient/AA) are committed.
2. Add `UserRegisteredEmailHandler : IDistributedEventHandler<UserRegisteredEto>`
   under `Application/Notifications/Handlers/`. Generates an
   email-confirmation token, builds the AuthServer-side confirm URL,
   dispatches via `INotificationDispatcher.DispatchAsync(
     code: NotificationTemplateConsts.Codes.UserRegistered,
     recipients: [the registered user],
     variables: { PatientFirstName, URL, ...brand defaults },
     contextTag: $"UserRegistered/{userId}")`.

### Variables map

| Token | Source |
|---|---|
| `##PatientFirstName##` | `eventData.FirstName` |
| `##URL##` | `authServerBaseUrl + "/Account/ConfirmEmail?userId={userId}&confirmationToken={token}"` |
| Brand tokens (`##CompanyLogo##` etc.) | empty strings until per-tenant branding ships |

---

## 2. PatientAppointmentPending (booker confirmation on submission)

### OLD trigger

Inside `SendEmail(statusId=Pending, appointment, emails, false)` at
`AppointmentDomain.cs:925-933`. Called from `AppointmentDomain.Add(appointment)`
at line 290 -- after the appointment row is committed and after the
SMS leg fires.

### OLD recipient

`emailTos = appointmentStackHoldersEmailPhone.EmailList` -- the full
stakeholder list returned by the stored proc. Includes patient, applicant
attorney, defense attorney, claim examiner, accessors -- whoever the
appointment row references with a non-null email.

CC: `ServerSetting.Get<string>("clinicStaffEmail")` -- a single global
clinic-staff inbox configured per deployment. NEW equivalent: the
`SystemParameter.CcEmailIds` per-tenant value (already a column on the
`SystemParameter` row, see `Domain.Shared/SystemParameters/SystemParameterConsts.cs`).

### OLD subject

`"Patient Appointment Portal - (Patient: {first} {last} - Claim: {claim} - ADJ: {adj}) - Your appointment request has been Pending."`
(line 926). The bracketed `(Patient: ... - Claim: ... - ADJ: ...)` is
constructed from `vEmailSender` + `AppointmentInjuryDetail` at line 916-921
with `String.IsNullOrEmpty` guards that drop empty segments.

### OLD variables

```
PatientFirstName, AppointmentDate (MM-dd-yyyy), AppointmentFromTime (hh:mm tt),
AppointmentRequestConfirmationNumber
```

### NEW state

- Template body + subject: seeded.
- Eto: **already published.** `AppointmentsAppService.CreateAppointmentInternalAsync`
  publishes `AppointmentSubmittedEto { AppointmentId, TenantId, BookerUserId, PatientId, RequestConfirmationNumber, AppointmentDate, SubmittedAt }`
  at line 763.
- Handler: **does not exist.** No subscriber to `AppointmentSubmittedEto`
  for the patient-side email yet.
- Recipient resolver: `IAppointmentRecipientResolver.ResolveAsync(id, NotificationKind.Submitted)`
  is the right call -- the resolver covers all stakeholders with role tags.
- CC list: `SystemParameter.CcEmailIds` per-tenant. The dispatcher takes
  recipients but not CC; need to either (a) add CC support to dispatcher
  or (b) add CC recipients as additional rows with a `RecipientRole.CcStaff`
  tag. Option (b) is cleaner -- treats CC as just-another-recipient and
  matches OLD's "everyone gets the same email body" behavior.

### Gap to close

Add `BookingSubmissionEmailHandler : ILocalEventHandler<AppointmentSubmittedEto>`
under `Application/Notifications/Handlers/`. Pulls stakeholders via the
resolver, appends CC recipients from `SystemParameter.CcEmailIds`,
dispatches via `INotificationDispatcher.DispatchAsync(
  code: NotificationTemplateConsts.Codes.PatientAppointmentPending, ...)`.

### Variables map

| Token | Source |
|---|---|
| `##PatientFirstName##` | resolver context (Patient row) |
| `##AppointmentDate##` | `appointment.AppointmentDate.ToString("MM-dd-yyyy")` |
| `##AppointmentFromTime##` | `availability.FromTime` formatted `hh:mm tt` |
| `##AppointmentRequestConfirmationNumber##` | `appointment.RequestConfirmationNumber` |
| `##PatientDetailsSubject##` | `(Patient: {first} {last}{ - Claim: X}{ - ADJ: Y})` per OLD construct |
| Brand tokens | empty until branding |

---

## 3. PatientAppointmentApproveReject (notify clinic staff to action)

### OLD trigger

Same code path as Pending. `AppointmentDomain.cs:935-951`. **Only fires when
`currentUserTypeId == UserType.ExternalUser`** -- when an external user
submits, internal staff need notification; when an internal user submits,
they don't email themselves.

### OLD recipient

```csharp
var vInternalUserEmails = AppointmentRequestUow
    .Repository<vInternalUserEmail>().All()
    .Where(x => x.RoleId == (int)Roles.StaffSupervisor || x.RoleId == (int)Roles.ClinicStaff)
    .ToList();
foreach (var item in vInternalUserEmails)
    email += item.EmailId + ";";
```

All Staff Supervisor + Clinic Staff users in the (single-tenant) system.

### OLD subject

`"Patient Appointment Portal - (Patient: ...) - Approve or Reject New Appointment Request"` (line 940).

### OLD variables

Same as Pending plus `##AppointmentToTime##`.

### NEW state

- Template body + subject: seeded.
- Eto: same `AppointmentSubmittedEto`.
- Handler: **does not exist.**
- Recipient resolver: NEW's `IAppointmentRecipientResolver` does NOT
  return internal staff (it walks the appointment's linked parties). We
  need a separate query: tenant users in the `Staff Supervisor` or
  `Clinic Staff` roles. ABP's `IdentityUserManager.GetUsersInRoleAsync(roleName)`
  is the right primitive; we already added an internal-user lookup in
  `AppointmentApprovalAppService.GetInternalUserLookupAsync` (commit
  `4b42575`) -- factor that lookup into a small shared helper or call
  it from the handler.

### Gap to close

Same handler as #2 (BookingSubmissionEmailHandler) -- since both fire from
`AppointmentSubmittedEto` and OLD batches the two sends adjacently. The
handler:

1. Pulls stakeholder list -> dispatches Pending to them (template #2).
2. Checks if the booker (CurrentUser at submit time) is external. The
   `AppointmentSubmittedEto` already carries `BookerUserId`; the handler
   queries that user's roles and decides external-vs-internal.
3. If external: pulls Staff Supervisor + Clinic Staff users in tenant ->
   dispatches PatientAppointmentApproveReject to them (template #3).

### Variables map

Same as Pending plus `##AppointmentToTime##` resolved from the
`DoctorAvailability.ToTime` field.

---

## 4. PatientAppointmentApprovedInternal (notify responsible user)

### OLD trigger

Inside `SendEmail(statusId=Approved, appointment, emails, internalUserUpdateStatus=true)`
at `AppointmentDomain.cs:953-967`. Called from `Update()` at line 563:

```csharp
if (appointment.AppointmentStatusId == Approved && internalUserUpdateStatus) {
    User user = ...Where(x => x.UserId == appointment.PrimaryResponsibleUserId).FirstOrDefault();
    SendEmail(Approved, appointment, user.EmailId, internalUserUpdateStatus=true);
    AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail(appointment);
}
```

This is the **second** send on approve -- AFTER the all-stakeholders send
at line 559. Recipient is the single primary responsible user.

### OLD recipient

`appointment.PrimaryResponsibleUserId` -> `User.EmailId`. CC = clinic
staff (`ServerSetting clinicStaffEmail`).

### OLD subject

`"Patient Appointment Portal - (Patient: ...) - Your appointment request has been approved successfully."`
(line 957).

### OLD variables

```
PatientFirstName, AppointmentRequestConfirmationNumber, AppointmentDate,
AppointmentFromTime, InternalUserComments (wrapped: "<b> Staff comments for an appointment: </b>" + value, only when present)
```

### NEW state

- Template body + subject: seeded.
- Eto: NEW publishes `AppointmentApprovedEto { AppointmentId, TenantId,
  AppointmentTypeId, PrimaryResponsibleUserId, PatientMatchOverridden,
  ApprovedByUserId, OccurredAt }` from `AppointmentApprovalAppService.ApproveAppointmentAsync`
  at line 108 (commit `4b42575` includes the InternalUserComments
  persistence). `AppointmentStatusChangedEto` also fires for the slot
  cascade.
- Handler: existing `StatusChangeEmailHandler` subscribes to
  `AppointmentStatusChangedEto`, but it currently sends only ONE inline-body
  email per status change (to all stakeholders, no responsible-user split).
- Recipient resolver: `IAppointmentRecipientResolver.ResolveAsync(id, NotificationKind.Approved)`
  returns stakeholders. For the responsible-user single-recipient send we
  need a direct lookup of `appointment.PrimaryResponsibleUserId.Email`.

### Gap to close

Migrate `StatusChangeEmailHandler` from inline-body construction to two
template-driven dispatches on Approved:

1. `INotificationDispatcher.DispatchAsync(code: PatientAppointmentApprovedExt,
   recipients: stakeholder list, ...)` -- the booker + parties, mirrors
   OLD line 980.
2. `INotificationDispatcher.DispatchAsync(code: PatientAppointmentApprovedInternal,
   recipients: [responsible user only], ...)` -- mirrors OLD line 966.

OLD's two sends use the SAME variable map (line 974 only differs by
"Please note" vs "Staff comments..." prefix on `##InternalUserComments##`).
Implement that prefix as a per-template variable transform inside the
handler so the templates stay clean.

### Variables map

| Token | Source |
|---|---|
| `##PatientFirstName##` | resolver context |
| `##AppointmentRequestConfirmationNumber##` | `appointment.RequestConfirmationNumber` |
| `##AppointmentDate##` | `appointment.AppointmentDate.ToString("MM-dd-yyyy")` |
| `##AppointmentFromTime##` | `availability.FromTime` formatted |
| `##InternalUserComments##` | empty, OR `<b>Staff comments for an appointment:</b> {value}` (Internal template) OR `<b>Please note:</b> {value}` (Ext template) |

---

## 5. PatientAppointmentApprovedExt (notify all stakeholders)

### OLD trigger

Inside the same `SendEmail(Approved, ...)` switch at `AppointmentDomain.cs:968-981`.
The "else" branch when `internalUserUpdateStatus = false` -- i.e. when an
external user (Patient via the public API) approved or when the system
auto-approved.

In NEW (Phase 12), all approves are internal by definition (only Clinic
Staff / Staff Supervisor / IT Admin / Doctor / admin can approve). So this
template's audience is the **stakeholder fan-out** that always fires
regardless of who approved -- per OLD line 559 the stakeholder send fires
unconditionally and the responsible-user send fires only when internal.

### OLD recipient

Stakeholder list (resolver). CC = clinic staff.

### OLD subject

Same as Internal: `"Patient Appointment Portal - (Patient: ...) - Your appointment request has been approved successfully."`

### OLD variables

Same as Internal but `##InternalUserComments##` prefixed `"<b> Please note: </b>"` instead of `"<b> Staff comments for an appointment: </b>"`.

### NEW state

Same as #4 -- one handler covers both. Stakeholder fan-out is the always-on
send; responsible-user is the second additional send when an internal user
approved (which in NEW Phase 12 = always).

### Gap to close

Same as #4. Two dispatches per approve:
1. Stakeholders -> `PatientAppointmentApprovedExt` with "Please note" prefix.
2. Responsible user -> `PatientAppointmentApprovedInternal` with "Staff comments" prefix.

---

## 6. PatientAppointmentRejected (notify all stakeholders)

### OLD trigger

`SendEmail(Rejected, ...)` switch at `AppointmentDomain.cs:984-991`.

### OLD recipient

Stakeholder list (resolver). NO CC -- line 990 calls the 3-arg overload.

### OLD subject

`"Patient Appointment Portal - (Patient: ...) - Your appointment request has been rejected by our clinic staff."`
(line 985).

### OLD variables

```
PatientFirstName, AppointmentDate, AppointmentFromTime, RejectionNotes,
AppointmentRequestConfirmationNumber
```

### NEW state

Same handler (`StatusChangeEmailHandler`) catches rejection via
`AppointmentStatusChangedEto.ToStatus = Rejected`. Currently inline body.

### Gap to close

Migration in the same `StatusChangeEmailHandler` rewrite:

```csharp
INotificationDispatcher.DispatchAsync(
  code: PatientAppointmentRejected,
  recipients: stakeholder list,
  variables: { ..., RejectionNotes = appointment.RejectionNotes ?? "" },
  contextTag: $"Rejected/{appointmentId}");
```

No CC for rejection (matches OLD line 990).

---

## Implementation plan

Three commits, each independently reviewable:

### Commit A: `StatusChangeEmailHandler` migration to templates

Touches:
- `Domain/Appointments/Handlers/StatusChangeEmailHandler.cs` -- replace
  `BuildEmail` inline-body switch with three `INotificationDispatcher.DispatchAsync`
  calls (Approved-stakeholders, Approved-responsible-user, Rejected).
- Inject `INotificationDispatcher` (Application contract -- the handler
  lives in Domain; check the project reference direction. If Domain can't
  reference Application.Contracts, flip the handler to live in
  `Application/Notifications/Handlers/StatusChangeEmailHandler.cs` and
  retire the Domain copy. Confirm during implementation.)
- Build a small `EmailVariableBuilder` static helper that composes the
  variables dict from the appointment + resolver context + "Please note"
  / "Staff comments..." `InternalUserComments` prefix. Lives next to the
  handler.

Acceptance:
- `dotnet build` clean.
- Approve fires exactly two templated emails (Ext to stakeholders +
  Internal to responsible user).
- Reject fires one templated email (Rejected to stakeholders).
- No inline body construction remains in the handler.

### Commit B: `BookingSubmissionEmailHandler` (new)

Touches:
- `Application/Notifications/Handlers/BookingSubmissionEmailHandler.cs` (new).
  Subscribes to `AppointmentSubmittedEto`. Fans out:
  1. Stakeholders + CC list -> `PatientAppointmentPending`.
  2. If booker is external: Staff Supervisor + Clinic Staff users in
     tenant -> `PatientAppointmentApproveReject`.
- Reuse `IAppointmentRecipientResolver`. Add a tiny
  `IInternalStaffEmailLookup` interface or inline the
  `IdentityUserManager.GetUsersInRoleAsync` calls.
- Add `RecipientRole.CcStaff` enum value if not present, OR pass CC
  recipients with the existing `RecipientRole.OfficeMailbox` tag.

Acceptance:
- `dotnet build` clean.
- Booking by external user fires both emails.
- Booking by internal user fires only Pending (no Approve-or-Reject staff
  blast).
- `SystemParameter.CcEmailIds` recipients receive the Pending email.

### Commit C: `UserRegisteredEmailHandler` (new) + Eto publish

Touches:
- `Application.Contracts/Notifications/Events/UserRegisteredEto.cs` (new).
- `Application/ExternalSignups/ExternalSignupAppService.cs` -- publish
  the Eto at the end of `RegisterAsync` after the user + role + downstream
  entity commit.
- `Application/Notifications/Handlers/UserRegisteredEmailHandler.cs` (new).
  Subscribes to the new Eto. Generates an email-confirmation token via
  `_userManager.GenerateEmailConfirmationTokenAsync`, builds the
  AuthServer `Account/ConfirmEmail` URL, dispatches
  `NotificationTemplateConsts.Codes.UserRegistered`.

Acceptance:
- `dotnet build` clean.
- Registering an external user produces one email to the user with a
  working verify link.
- Clicking the link confirms the email (ABP's stock flow); subsequent
  login no longer hits the `RequireConfirmedEmail` gate.

---

## Risks / open questions

1. **Domain -> Application reference direction.** `StatusChangeEmailHandler`
   currently lives in `Domain/Appointments/Handlers/`. `INotificationDispatcher`
   lives in `Application.Contracts`. ABP layered architecture allows
   Application -> Domain but not Domain -> Application or
   Application.Contracts. Resolution: move the handler to
   `Application/Notifications/Handlers/StatusChangeEmailHandler.cs`. The
   new home matches the other handlers.

2. **CC list shape.** `INotificationDispatcher` takes `recipients` as a
   list of `NotificationRecipient`; no CC slot. Treating CC as additional
   recipients with role `CcStaff` is the cleanest fix. Verified: that's
   how the AccessorInvited handler treats secondary recipients.

3. **Brand tokens (`##CompanyLogo##` etc.).** Per-tenant branding is its
   own feature. For demo: dispatcher substitutes empty strings for these
   tokens (so the templates render but show no logo / no tagline).
   Mark with a TODO row in `_parity-flags.md` so we don't ship to
   production with empty footer text.

4. **`PatientDetailsSubject` token.** OLD constructs
   `(Patient: {first} {last} - Claim: {x} - ADJ: {y})` inline at line 921
   with empty-segment dropping. NEW computes once in the handler from the
   appointment + first injury + dedup logic, passes as a single
   `##PatientDetailsSubject##` variable. Centralise the construction in
   the new `EmailVariableBuilder` so all three handlers share it.

5. **Tenant-scope guarantees.** All three handlers run inside ABP's
   distributed-event UoW which restores `CurrentTenant.Id` from the Eto.
   The recipient resolver and dispatcher both observe the
   `IMultiTenant` filter automatically, so a wrong tenant returns
   nothing. Verified pattern in existing AccessorInvited /
   ChangeRequest handlers.

---

## Sources

OLD:
- `P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs:295-353`
- `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs:280-300, 540-570, 575-595, 883-1052`
- `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs:319-330` (stakeholder stored proc call)

NEW:
- `Application/ExternalSignups/ExternalSignupAppService.cs:306-413` (RegisterAsync)
- `Application/Appointments/AppointmentsAppService.cs:606-773` (CreateAppointmentInternalAsync, including AppointmentSubmittedEto publish)
- `Application/Appointments/AppointmentsAppService.Approval.cs:78-127` (ApproveAppointmentAsync, including AppointmentApprovedEto publish + InternalUserComments persistence)
- `Domain/Appointments/Handlers/StatusChangeEmailHandler.cs` (current inline-body impl)
- `Domain/Appointments/Notifications/AppointmentRecipientResolver.cs` (stakeholder fan-out)
- `Application/Notifications/NotificationDispatcher.cs` + contract (template + token render)
