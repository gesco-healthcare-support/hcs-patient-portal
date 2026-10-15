---
id: E3
title: Document upload + approval/rejection emails -- To uploader, rest of parties CC'd
type: enhancement
components: [src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/DocumentUploadedEmailHandler.cs, src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/DocumentAcceptedEmailHandler.cs, src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/DocumentRejectedEmailHandler.cs, src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/DocumentEmailContextResolver.cs, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/AppointmentRecipientResolver.cs, src/HealthcareSupport.CaseEvaluation.Application/Notifications/NotificationDispatcher.cs, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs, src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/SendAppointmentEmailArgs.cs]
related_known_bugs: [OBS-14, BUG-014, BUG-029, BUG-033, BUG-036]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

Document-flow emails must become single addressed-To-plus-CC messages, matching the
E1/E2/E3 email model locked 2026-06-03:

- Document UPLOAD email: To the uploader, CC the FULL stakeholder set (booker + AA + DA +
  CE + office).
- Document APPROVAL and REJECTION emails: To the uploader, CC patient + AA + DA + CE
  (office EXCLUDED, since office staff performed the approve/reject).

Today none of the three document handlers include "the rest of the parties," and the send
pipeline has no real SMTP CC header at all. This item shares net-new CC plumbing with E1
and reuses the existing AppointmentRecipientResolver.

## Current behavior (from investigation)

- UPLOAD: `DocumentUploadedEmailHandler` resolves `uploaderEmail`
  (`ResolveUploaderEmailAsync`, falls back to patient/booker email) and adds it as a
  recipient (role Patient), then adds ONLY the appointment's responsible/office user
  (`ResolveResponsibleUserEmailAsync`, role OfficeAdmin) if present. No AA/DA/CE, no CC
  (DocumentUploadedEmailHandler.cs:82-102).
- APPROVAL: `DocumentAcceptedEmailHandler` sends to `uploaderEmail` ONLY -- single
  recipient, role Patient (DocumentAcceptedEmailHandler.cs:86-105).
- REJECTION: `DocumentRejectedEmailHandler` sends to `uploaderEmail` ONLY
  (DocumentRejectedEmailHandler.cs:68-149, recipient list at :100-106).
- None of the three handlers call `AppointmentRecipientResolver` -- the "all parties"
  resolver -- so AA/DA/CE/office are never reached on document events
  (AppointmentRecipientResolver.cs is the existing resolver; document handlers do not
  reference it).
- Each handler builds a `List<NotificationRecipient>` and calls
  `_dispatcher.DispatchAsync(...)` (DocumentUploadedEmailHandler.cs:135-139,
  DocumentAcceptedEmailHandler.cs:144-149).
- NO CC header anywhere in the send path: `NotificationDispatcher.DispatchAsync` enqueues
  ONE separate email per recipient (NotificationDispatcher.cs:85-89);
  `SendAppointmentEmailArgs` carries only a single `To` string with no Cc/Bcc
  (SendAppointmentEmailArgs.cs:15); `SendAppointmentEmailJob.SendPlainAsync` calls
  `_emailSender.SendAsync(args.To, ...)` (SendAppointmentEmailJob.cs:94) and the attachment
  path does only `mail.To.Add(args.To)` (:135) with no `mail.CC.Add`. Today's model is
  fan-out (N separate single-recipient emails), not To+CC.
- Approve entrypoint: `AppointmentDocumentsAppService.ApproveAsync` (permission
  AppointmentDocuments.Approve) publishes `AppointmentDocumentAcceptedEto`
  (AppointmentDocumentsAppService.cs:533-563); RejectAsync publishes
  `AppointmentDocumentRejectedEto` (:565-596); upload publishes
  `AppointmentDocumentUploadedEto` (:218-392).

## Relevant code locations

- DocumentUploadedEmailHandler.cs:64-139 -- upload addressing (uploader + responsible only)
- DocumentAcceptedEmailHandler.cs:67-149 -- approval addressing (uploader only)
- DocumentRejectedEmailHandler.cs:68-149 -- rejection addressing (uploader only)
- DocumentEmailContextResolver.cs:134-145 -- `ResolveUploaderEmailAsync` (uploader, falls
  back to patient/booker)
- AppointmentRecipientResolver.cs:90 -- `ResolveAsync(Guid appointmentId, NotificationKind
  kind)` returns `List<SendAppointmentEmailArgs>`
- NotificationDispatcher.cs:61-126 -- fan-out enqueue (needs CC-aware send)
- SendAppointmentEmailArgs.cs:15 -- single To field (needs Cc)
- SendAppointmentEmailJob.cs:90-176 -- plain + attachment send (needs `mail.CC.Add`)
- NotificationKind.cs:19-21 -- DocumentUploaded=8, DocumentAccepted=9, DocumentRejected=10
  (the resolver kinds already exist for document flow)

## Phase 3 cross-reference

- OBS-14 -- shared To-only send path, no CC header. The SAME constraint blocks E1; fix the
  CC plumbing once and both items consume it.
- BUG-014 / BUG-029 -- portal/login URL composition via IAccountUrlBuilder + tenant
  subdomain. Already migrated in these document handlers; the shared body for CC'd parties
  must keep carrying __tenant on its login link (do not regress).
- BUG-033 / BUG-036 -- packet-generation cascade fires on document approval (the Approve
  path also triggers packet/attachment emails). Verify recipient overlap so a CC'd party
  does not get a duplicate packet plus approval notice; bundle the recipient-overlap check
  while touching the approval handler.

## Research findings

- Internal patterns / prior art:
  - `AppointmentRecipientResolver.ResolveAsync(appointmentId, kind)` already resolves the
    full stakeholder set (booker + AA + DA + CE + office) per `NotificationKind`, and the
    document kinds (DocumentUploaded/Accepted/Rejected = 8/9/10) already exist in the enum
    (NotificationKind.cs:19-21). The resolver returns `List<SendAppointmentEmailArgs>`,
    whereas the document handlers build `List<NotificationRecipient>` and call
    `NotificationDispatcher.DispatchAsync` -- so the resolver output is one abstraction
    level removed from how the document handlers dispatch. Reuse the resolver for WHO,
    keep DispatchAsync for HOW (see Implementation outline).
  - `CcRecipientAppender` calls its entries "CC" but they are merely extra To-recipients
    with role OfficeAdmin; it is NOT a header CC. E3 requires real header CC, identical to
    E1's net-new plumbing.
  - The uploader is already correctly the To party in all three handlers; the gap is only
    the CC set plus the literal CC header.
- External docs (ABP / Angular / EF Core): none needed beyond the existing
  `IEmailSender` / `MailMessage` send path; `mail.CC.Add` is the standard
  System.Net.Mail surface already used for `mail.To.Add` in SendAppointmentEmailJob.

## Approaches considered (with tradeoffs)

1. Real SMTP To+CC on one message (CHOSEN). Add a `Cc` field to SendAppointmentEmailArgs,
   thread it through NotificationDispatcher and SendAppointmentEmailJob (both plain +
   attachment legs), and have each document handler resolve parties via
   AppointmentRecipientResolver, pick the uploader as To, and pass the rest as Cc.
   - Pro: matches the locked E1/E2/E3 model literally (one message, visible CC), avoids N
     duplicate inboxes, single shared body, reuses the resolver.
   - Con: net-new plumbing across args/dispatcher/job; shared by E1 (one-time cost).
2. Keep per-recipient fan-out, just widen the recipient list. Add AA/DA/CE to the existing
   List<NotificationRecipient> and keep N separate To-only emails.
   - Rejected: violates the locked decision (recipients would not SEE who else was
     notified; not a real CC), and re-sends the same body N times. The whole point of the
     2026-06-03 model is one addressed message with the rest CC'd.
3. New document-specific resolver. Build a parallel resolver for document audiences.
   - Rejected: duplicates AppointmentRecipientResolver logic; the locked decision says
     "document handlers must call the existing AppointmentRecipientResolver." The
     office-include/exclude difference is a per-event filter, not a new resolver.

## Decision (locked 2026-06-03)

Document emails become single To+CC messages reusing AppointmentRecipientResolver:

- UPLOAD -> To uploader; CC full stakeholder set (booker + AA + DA + CE + office).
- APPROVAL and REJECTION -> To uploader; CC patient + AA + DA + CE; office EXCLUDED.
- Add real SMTP CC plumbing (Cc field on SendAppointmentEmailArgs -> NotificationDispatcher
  -> SendAppointmentEmailJob), shared with E1.
- One shared, non-role-aware body that says "log in or register to view" with a login link
  (consistent with E1/E2). The patient packet stays a SEPARATE email to the patient.
- Uploader stays To in all three handlers (already true). Dedupe the uploader out of the
  CC set so they are not both To and CC.

## Implementation outline (no code)

1. Plumbing (shared with E1, do once):
   - Add a `Cc` collection field to `SendAppointmentEmailArgs` (SendAppointmentEmailArgs.cs).
     This is a Domain.Shared change; flag for proxy/serialization review of the job args.
   - Thread Cc through `NotificationDispatcher` so a single dispatch can carry one To plus a
     CC list instead of fanning out (NotificationDispatcher.cs:61-126). Add a To+CC dispatch
     path; keep the existing fan-out path for callers that still need it.
   - In `SendAppointmentEmailJob`, add `mail.CC.Add(...)` for each Cc entry on BOTH the
     plain and attachment legs (SendAppointmentEmailJob.cs:90-176). Server-side send concern
     only (no UI).
2. Resolver reuse + per-event filter (server-side):
   - UPLOAD handler: call `AppointmentRecipientResolver.ResolveAsync(appointmentId,
     NotificationKind.DocumentUploaded)` for the full set; choose uploaderEmail as To; the
     remainder (including office) becomes CC. Replace the responsible-user-only logic
     (DocumentUploadedEmailHandler.cs:88-102).
   - APPROVAL handler: resolve with `NotificationKind.DocumentAccepted`; To uploader; CC
     patient + AA + DA + CE; FILTER OUT the office/OfficeAdmin recipient
     (DocumentAcceptedEmailHandler.cs:99-105).
   - REJECTION handler: resolve with `NotificationKind.DocumentRejected`; same To/CC shape
     as approval; office excluded (DocumentRejectedEmailHandler.cs:100-106).
   - Bridge the type gap: the resolver returns `List<SendAppointmentEmailArgs>` while
     handlers build `List<NotificationRecipient>` -- map resolver output to the dispatch
     recipient shape (or extend the dispatcher to accept resolver args directly). Confirm
     the office-vs-non-office tag the resolver emits so the approve/reject filter keys off a
     stable marker (RecipientRole.OfficeAdmin) rather than an email guess.
   - Dedupe the uploader from the CC list (first-wins by email) so To and CC do not overlap.
3. Anonymous-uploader case: when uploader has no IdentityUser, To falls back to
   patient/booker email (existing ResolveUploaderEmailAsync). Keep that as To and still CC
   the parties.
4. Body: switch the three document templates to the shared "log in or register to view"
   body with a login link (consistent with E2). The packet email to the patient stays
   separate (BUG-033/BUG-036 cascade) -- do not merge it into the To+CC message.
5. Enforcement: addressing is server-side only (notification handlers); no UI surface and
   no client mirror. No EF migration. No Angular proxy regen unless the SendAppointmentEmailArgs
   shape is exposed through a proxy (it is a Hangfire job arg, not a DTO -- verify, but
   expected NO proxy regen).

## Dependencies

- DEPENDS ON E1's CC plumbing (Cc field on SendAppointmentEmailArgs + dispatcher + job).
  Build the plumbing once under E1 (or E3, whichever lands first) and have the other consume
  it. Sequencing: ship the shared To+CC send path before wiring either handler set.
- COORDINATE WITH BUG-033/BUG-036 on the approval path to avoid duplicate packet vs
  approval emails to overlapping recipients.

## Residual open questions

- none (office include/exclude per event, the To party, and the To+CC model are all locked
  2026-06-03; the only build-time detail is the resolver-output-to-dispatch-recipient
  mapping, covered in the outline).
