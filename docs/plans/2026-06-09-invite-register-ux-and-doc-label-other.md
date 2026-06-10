---
status: built-and-deployed (live UI confirmation pending user test)
date: 2026-06-09
slug: invite-register-ux-and-doc-label-other
branch: main (worktree)
---

# Invite registration UX + document-label "Other" + text-template log fix

Three demo-readiness fixes requested 2026-06-09. Research summary delivered in
chat and approved; this is the design + execution record.

## T1 - invited users must not see a "verify your email" page

**Problem.** Invite-based registration auto-confirms the email server-side
(OBS-25: the invite link already proved inbox ownership), so NO verification
email is sent. But the AuthServer register JS overlay showed the same success
banner for everyone -- "We sent a verification link... " + a "Resend
verification" button -- stranding invited users waiting for a link that never
arrives. Verified live: `defatty1@gesco.com` had `EmailConfirmed=1`, invite
`Accepted`, and no verification email was ever logged.

**Backend (1a).** No change. `ExternalSignupAppService.RegisterAsync` already
skips the verification-email block when `EmailConfirmed` (guard verified
correct against live logs + DB).

**Frontend (1b).** `src/...AuthServer/wwwroot/global-scripts.js`
`showSignupSuccess()` now branches on `readQueryParam('inviteToken')`:
- invite present -> "You're all set. Your account is ready. Please sign in to
  continue." + single **Sign in** button (no verification text, no resend).
- self-registration -> unchanged (verification banner + Resend).

## T1c - stop the AbpTextTemplateDefinitionRecords duplicate-key startup error

**Problem.** Three `Cannot insert duplicate key ... Abp.Account.EmailConfirmationCode`
SqlExceptions at startup (`19:34:49`). Root cause: api + authserver initialize
concurrently and both save static text-template definitions to the DB. The
permission/setting/feature definition savers are guarded by the Redis
distributed lock (already configured in both hosts), but the text-template
saver is NOT -- so only it races.

**Fix.** Set `TextTemplateManagementOptions.SaveStaticTemplatesToDatabase = false`
on the two runtime hosts (`CaseEvaluationHttpApiHostModule`,
`CaseEvaluationAuthServerModule`). The DbMigrator (runs to completion before the
hosts start, alone) remains the sole writer of `AbpTextTemplateDefinitionRecords`
-> no concurrent INSERT. Templates still resolve from the in-memory definition
providers at runtime, so this is safe (this app uses its own NotificationTemplate
aggregate for emails anyway). Property names verified against the ABP 10.0.2
assembly.

## T2 - "Other" free-text option on the booking document-label dropdown

**Backend.** No change. `UploadAppointmentDocumentForm.OtherDocumentTypeName`
exists and the controller passes it to `UploadStreamAsync`; the AppService
validates (max 100, mutually exclusive with `appointmentDocumentTypeId`) and
persists it.

**Frontend.**
- `sections/appointment-add-documents.component.ts/.html` -- export
  `OTHER_DOCUMENT_TYPE_VALUE` sentinel + max-length; add "Other..." option, a
  conditional free-text input (max 100, `is-invalid` when empty), and an
  `otherDocumentTypeNameChange` output.
- `appointment-add.component.ts` -- `onDocumentTypeChange` maps the sentinel to
  `isOtherType` (clears type id + strike-list flag); `onOtherDocumentTypeNameChange`
  stores the text; `uploadStagedDocuments` sends `otherDocumentTypeName` (omitting
  `appointmentDocumentTypeId`) when "Other"; reset + a submit gate
  (`otherLabelMissing`) require the text when "Other" is chosen.
- `appointment-add.component.html` -- new output binding + a missing-label alert.

## Verification
- [x] `docker compose build` authserver/api/angular one at a time (api + angular
      needed `--no-cache`: this Windows/Docker host has unreliable COPY-layer
      cache invalidation -- the cached builds silently reused pre-edit source).
- [x] No `AbpTextTemplateDefinitionRecords` duplicate-key error after a
      concurrent `restart api authserver` (reproduces the race): 0 errors, both
      hosts boot healthy, no new template/config errors.
- [x] Deployed code present in running artifacts: authserver `global-scripts.js`
      has the invite "all set" branch AND the preserved "Resend verification"
      branch; angular bundle contains `__other__` + "Enter document type".
- [ ] (user) Register via invite link -> success page shows "Sign in" only;
      account logs in directly.
- [ ] (user) Self-register (no invite) -> verification banner still shown.
- [ ] (user) Booking: pick "Other", type a label, upload -> doc stored with
      `OtherDocumentTypeName`; blank "Other" blocks submit.

## Risk / Rollback
Blast radius: AuthServer register success UX, two host module configs, the
booking documents section. Rollback: revert the listed files; no DB migration,
no schema change, no proxy regen.
