---
id: E1
title: Appointment Request email -- one message To booker, parties CC'd (umbrella email-model change)
type: enhancement
components:
  - src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/SendAppointmentEmailArgs.cs
  - src/HealthcareSupport.CaseEvaluation.Application/Notifications/NotificationDispatcher.cs
  - src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs
  - src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/BookingSubmissionEmailHandler.cs
  - src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/AppointmentRecipientResolver.cs
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/Notifications/NotificationRecipient.cs
  - src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/AppointmentRequested*.html
related_known_bugs: [OBS-14, BUG-014, BUG-029, OBS-36, "Decision 2.1 (2026-05-08, SUPERSEDED)"]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

The Appointment Request email currently fans out as N separate single-recipient messages
(one per party, each with a role-specific body). The change: send ONE message addressed
literal-SMTP `To:` the booker, with every other party in literal-SMTP `CC:`, sharing one
non-role-aware body that says "log in or register to view" plus a login link.

E1 is the UMBRELLA for the whole ex-parte email-model change. The same To+CC single-message
shape applies to every multi-party notification (Appointment Request, Reschedule, Approval,
Cancellation, Document upload/approval/rejection). This note carries the shared CC plumbing
and addressing-model reasoning; per-notification To-party assignment is restated below.

## Current behavior (from investigation)

- Submission handler does per-recipient fan-out: `BookingSubmissionEmailHandler`
  `DispatchAppointmentRequestedAsync` makes one `DispatchAsync` call per recipient
  (BookingSubmissionEmailHandler.cs:225-321; per-recipient send loop 271-303). A doc comment
  at lines 219-223 records "NO CC on this fan-out per Adrian directive 2026-05-08 (Decision 2.1)".
- The recipient resolver already produces the full party set: `AppointmentRecipientResolver.ResolveAsync`
  returns booker + applicant attorney + defense attorney + claim examiner + office mailbox as a
  flat, email-deduped list (AppointmentRecipientResolver.cs:90-248); the booker is tagged
  `RecipientRole.Patient` via `appointment.IdentityUserId` (line 235-236).
- The send pipeline has NO CC concept anywhere:
  - `SendAppointmentEmailArgs` carries a single `To` string and no `Cc`/`Bcc` field
    (SendAppointmentEmailArgs.cs:15 -- verified; the class has To/Subject/Body/Role/IsRegistered/
    TenantName/TenantId/PacketRef only).
  - `NotificationDispatcher.DispatchAsync` loops the recipient collection and enqueues ONE
    `SendAppointmentEmailArgs` per recipient with `To = recipient.Email`
    (NotificationDispatcher.cs:85-88 loop, 107-125 enqueue -- verified).
  - `SendAppointmentEmailJob` sends `_emailSender.SendAsync(args.To, ...)` (SendAppointmentEmailJob.cs:94)
    and the attachment path does only `mail.To.Add(args.To)` with no `mail.CC.Add` (line 135).
- Bodies are role-aware per recipient: three template variants (Registered = "log in" CTA,
  Unregistered = "register as [role]" CTA, Office = portal-queue link), branched on
  `IsRegistered` and `Role`. `LoginUrl`/`RegisterUrl` are pre-filled per recipient via
  `IAccountUrlBuilder` (BUG-014, BUG-029).
- `CcRecipientAppender` exists but only appends extra `To`-recipients tagged OfficeAdmin; it is
  NOT applied to the AppointmentRequested fan-out (per Decision 2.1).

## Relevant code locations

- `SendAppointmentEmailArgs.cs:15` -- add a `Cc` collection (net-new field).
- `NotificationDispatcher.cs:61-126` -- new single-message dispatch path that sets To + Cc once
  instead of looping one enqueue per recipient.
- `SendAppointmentEmailJob.cs:90-176` -- both the plain (`SendAsync`) and attachment
  (`mail.To.Add`) paths must add `mail.CC.Add(...)` for each Cc address.
- `BookingSubmissionEmailHandler.cs:225-321` -- replace the fan-out loop with To=booker /
  CC=rest assembly; drop the Decision-2.1 "no CC" comment.
- `AppointmentRecipientResolver.cs:90-248` -- reused as-is to produce the party set; To-party is
  selected from this set, the remainder become CC.
- `NotificationRecipient.cs` (Application.Contracts/Notifications) -- recipient row; no To-vs-CC
  marker today. The To-vs-CC split is decided in the handler, not on this row.
- `NotificationTemplates/EmailBodies/AppointmentRequested*.html` -- collapse 3 role variants into
  ONE shared body (E2 covers the body wording).

## Phase 3 cross-reference

- E2 -- the shared single body MUST offer both "Register OR Login" affordances (a CC'd
  unregistered party no longer gets a per-recipient Register CTA). E1's CC model forces E2's
  one-body shape; build them together.
- E3 -- document upload/approval/rejection emails get the SAME To-uploader/CC-parties treatment
  on the SAME new CC plumbing. Land the plumbing once in E1, then E3 reuses it. E3 also needs the
  document handlers to start calling `AppointmentRecipientResolver` (they do not today).
- OBS-14 -- confirms the shared To-only emailer + job queue with no CC header; this is the
  constraint E1 removes.
- BUG-014 / BUG-029 -- LoginUrl/RegisterUrl already config-driven and tenant-aware via
  `IAccountUrlBuilder`; the shared body reuses those builders (no regression risk if the single
  body keeps one login link).
- OBS-36 -- 23 stub templates pending parity; collapsing the 3 AppointmentRequested variants
  touches the same template governance, worth a glance while editing bodies.

## Research findings

- Internal patterns / prior art:
  - The resolver already returns the complete deduped party list (AppointmentRecipientResolver.cs:90-248),
    so E1 is an addressing-model change, not greenfield recipient discovery.
  - `CcRecipientAppender` is the closest prior art for "extra recipients," but it mislabels extra
    To-recipients as CC. The real fix supersedes it with genuine SMTP CC headers; the appender can
    be retired or repurposed once true CC exists.
  - Tenant re-entry already flows through `SendAppointmentEmailArgs.TenantId` for the packet path
    (SendAppointmentEmailArgs.cs:60-70); the single-message path inherits that unchanged.
- External docs (HIGH confidence -- official):
  - .NET `System.Net.Mail.MailMessage` exposes `.To` and `.CC` as `MailAddressCollection`; CC is
    added via `mail.CC.Add(address)` (Microsoft Learn, System.Net.Mail.MailMessage). The
    attachment path in SendAppointmentEmailJob already constructs a `MailMessage`, so adding
    `mail.CC.Add` per Cc entry is a direct, supported call.
  - ABP `IEmailSender.SendAsync` has overloads taking either a plain `to` string or a fully built
    `MailMessage` (ABP docs, Email Sending). The plain-string overload has no CC parameter, so the
    To+CC path must build a `MailMessage` and pass that overload -- the same `MailMessage` route the
    attachment branch already uses today.
  - Confidence on the exact ABP overload signature: MEDIUM until verified against the installed
    10.0.2 `IEmailSender`; flag at build time.

## Approaches considered (with tradeoffs)

1. Keep per-recipient fan-out, relabel as "intent satisfied" (REJECTED).
   - Pro: zero plumbing change. Con: does not deliver the literal To+CC the feedback asks for;
     parties cannot see who else was notified (the whole point of CC for a multi-party request).
     Fails the explicit ask.
2. Fake CC by appending extra To-recipients (the existing CcRecipientAppender shape) (REJECTED).
   - Pro: reuses existing code. Con: every party shows up in the To: line -- there is no visible
     To-vs-CC distinction, and it muddies "who is the primary addressee." Not real CC.
3. Net-new `Cc` field on args/job/dispatcher + single addressed message (CHOSEN).
   - Pro: delivers literal SMTP To+CC; one shared body; one email per event instead of N;
     reusable by E3 and all future multi-party notifications. Con: touches the shared send
     pipeline and reverses Decision 2.1 (role-aware per-recipient bodies). The reversal is
     already approved 2026-06-03; the role-aware body loss is accepted in favor of one
     "log in or register" body (E2).

Why the chosen direction wins: it is the only option that produces real To+CC headers AND a
single message, and the plumbing is built once and reused across every multi-party notification.
The cost (one shared body, no per-recipient Register CTA) is an explicit, approved tradeoff.

## Decision (locked 2026-06-03)

- One message per multi-party event: literal SMTP `To` = the designated party, literal SMTP `CC`
  = the rest. One shared body (NOT role-aware) saying "log in or register to view" with a login
  link. SUPERSEDES Decision 2.1 (2026-05-08).
- Net-new CC plumbing: add a `Cc` field to `SendAppointmentEmailArgs`, thread it through
  `NotificationDispatcher` and `SendAppointmentEmailJob` (both plain and attachment send paths).
- To-party matrix (the rest are CC'd):
  - Appointment Request -> To = booker (this item's specific case).
  - Appointment approval/rejection -> To = patient; CC = AA/DA/CE (office EXCLUDED).
  - Document upload -> To = uploader; CC = full stakeholder set INCLUDING office (E3).
  - Document approval AND rejection -> To = uploader; CC = patient/AA/DA/CE (office EXCLUDED) (E3).
- The patient packet stays a SEPARATE email to the patient (do not fold into the To+CC message).
- DEFERRED to a later phase: the AA-intermediary refinement (if AA present, patient receives only
  patient-relevant emails; intermediary mail among AA/DA/CE). Do NOT build now.

## Implementation outline (no code)

1. Domain.Shared: add a `Cc` collection (e.g. `List<string>`) to `SendAppointmentEmailArgs`
   (SendAppointmentEmailArgs.cs). Default empty for backward-compat with single-recipient callers.
2. Domain (job): in `SendAppointmentEmailJob`, populate `mail.CC.Add(...)` for each Cc entry on
   BOTH the plain send and the attachment path; verify the ABP `IEmailSender` overload that accepts
   a `MailMessage` (so CC is honored) -- the plain-string overload cannot carry CC.
3. Application (dispatcher): add a single-message dispatch path on `NotificationDispatcher` that
   takes one To recipient + a CC list and enqueues ONE `SendAppointmentEmailArgs`, instead of the
   current per-recipient loop. Keep the existing fan-out method for any callers that still need it
   (or migrate all callers).
4. Application (handler): rewrite `BookingSubmissionEmailHandler.DispatchAppointmentRequestedAsync`
   to resolve the party set (reuse `AppointmentRecipientResolver`), pick the booker as To, route
   the rest to CC, render ONE shared body, enqueue once. Remove the Decision-2.1 "no CC" comment.
5. Templates: collapse the three `AppointmentRequested*.html` variants into one shared body
   (E2 owns the wording: single "log in or register" CTA + login link). Re-seed via
   `NotificationTemplateDataSeedContributor` (it overwrites DB bodies from the HTML files).
6. Server-vs-UI enforcement: this is a backend-only notification change -- NO UI surface and NO
   client validation. To/CC assignment is server-decided; there is nothing to mirror in Angular.
7. Migration / proxy: no EF migration (no entity/schema change). No Angular proxy regen needed
   (`SendAppointmentEmailArgs` is a Hangfire job payload, not an exposed DTO) -- confirm none of
   the changed types appear in `angular/src/app/proxy/` before skipping regen.
8. Verify packet email remains a separate send (do not attach the packet to the To+CC message).

## Dependencies

- BLOCKS: E2 (shared single body requires E1's one-body addressing model) and E3 (reuses E1's
  CC plumbing on the document handlers).
- DEPENDS ON: none. E1 lands the foundational CC plumbing first; E2 and E3 build on it.

## Residual open questions

- "The booker" for To: = the `IdentityUser` who submitted (`appointment.IdentityUserId`), even when
  internal staff books on behalf of a patient. Confirm this is the intended addressee vs. the patient
  in the staff-booking case (minor; default to the submitter per the resolver's current tagging).
- Whether `CcRecipientAppender` is retired or repurposed once real CC exists (cleanup, not blocking).
- Verify the exact ABP 10.0.2 `IEmailSender` `MailMessage` overload signature at build time
  (MEDIUM confidence on the API surface today).
