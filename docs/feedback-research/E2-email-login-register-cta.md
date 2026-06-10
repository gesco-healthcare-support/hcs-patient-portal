---
id: E2
title: Appointment Request email body must say "Register or Login to view" with a login link
type: enhancement
components: [src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/, src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/BookingSubmissionEmailHandler.cs, src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailSubjects.cs, src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/NotificationTemplateDataSeedContributor.cs]
related_known_bugs: [BUG-014, BUG-029, OBS-27, OBS-36]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
The Appointment Request notification body must explicitly tell the recipient to "log in or
register to view the appointment" and surface a login link, in ONE shared, non-role-aware
body. Today the wording is split across three role-aware templates (Registered = Login CTA,
Unregistered = Register CTA, Office = neither). When E1 collapses the per-recipient fan-out
into a single To+CC message, that single body must offer both affordances to every recipient.

## Current behavior (from investigation)
Three separate AppointmentRequested templates exist, each role-aware:

- `AppointmentRequestedRegistered.html:13-17` -- says "Log in to the appointment portal to
  view the request, see updates, and receive scheduling notifications." with a "Log in to
  view appointment" button to `##LoginUrl##`. No register wording.
- `AppointmentRequestedUnregistered.html:13-19` -- says "Register below to view this and
  future appointments..." with a "Register as ##RoleDisplayName##" button to `##RegisterUrl##`,
  plus a trailing greyed note (line 19): "After registering, log in to view this appointment
  on the appointment portal."
- `AppointmentRequestedOffice.html:13-18` -- says "Open the appointments queue in the
  appointment portal to review the request and respond." with an "Open appointment portal"
  button to `##PortalUrl##`. No Register/Login wording (targets the office mailbox).

The handler chooses which template + variables per recipient and builds the URLs:
`BookingSubmissionEmailHandler.cs:329-459` (`BuildAppointmentRequestedVariables`,
`BuildRegisterUrl`, `BuildLoginUrl`; sets `PortalUrl`/`RegisterUrl`/`LoginUrl`). `LoginUrl`
and `RegisterUrl` already carry `__tenant` + email (+ role for register) pre-fill (per BUG-029).

So E2's wording requirement is ALREADY satisfied for the Registered and Unregistered
templates; the literal gaps are (a) the Office template has neither CTA, and (b) E1's single
shared body cannot branch on `IsRegistered`, so the one body must contain BOTH affordances.

## Relevant code locations
- `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/AppointmentRequestedRegistered.html`
- `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/AppointmentRequestedUnregistered.html`
- `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/AppointmentRequestedOffice.html`
- `BookingSubmissionEmailHandler.cs:329-459` -- variable wiring; collapses to one variable
  set when E1 lands (LoginUrl + RegisterUrl both present on the single body).
- `EmailSubjects.cs:147-156` -- subjects for the three AppointmentRequested codes (subject
  consolidation if codes collapse).
- `NotificationTemplateDataSeedContributor.cs` -- seeder OVERWRITES the DB body + subject
  from `EmailBodies/*.html` on every run when the file exists (per NotificationTemplates
  CLAUDE.md "Seeder update behavior"). Editing the `.html` is the supported propagation path.

## Phase 3 cross-reference
- OBS-27 (invite email empty greeting) -- sibling template-wording fix; bundle if touching
  EmailBodies wording in the same pass so reviewers review one wording PR.
- OBS-36 (23 stub templates pending parity) -- template-wording governance; the consolidated
  body must follow the same Codes.All / EmbeddedResource discipline if a new code is minted.
- BUG-014 / BUG-029 -- already-fixed URL composition (config-driven LoginUrl/PortalUrl;
  `__tenant` on email URLs); no new work, just do not regress when editing the body. Not
  bundled.

## Research findings
- Internal patterns / prior art:
  - Variable substitution is `##Var##` -> dictionary via `TemplateVariableSubstitutor.Substitute`;
    unknown placeholders are left in place and null renders as empty (NotificationTemplates
    CLAUDE.md "Variable substitution"). A single body referencing both `##LoginUrl##` and
    `##RegisterUrl##` is safe: if E1 supplies both, both render; if one is null it renders empty.
  - GOTCHA (Domain CLAUDE.md + NotificationTemplates CLAUDE.md): a missing `.html` under
    `EmailBodies/` is SILENT -- the email sends with an empty body. Any new/renamed code must
    ship its `.html` as `<EmbeddedResource>` in the `.csproj` and be appended to `Codes.All`,
    or the row is never seeded and handlers throw NotFound.
  - Seeder overwrites DB body+subject from disk only when the `.html` exists; stubs are
    written once and preserved. So editing the existing files re-propagates to all tenants.
- External docs: none required; this is internal HTML template wording + existing substitution.

## Approaches considered (with tradeoffs)
1. (CHOSEN) One shared, non-role-aware body offering BOTH "Log in" and "Register" with a
   login link; applied when E1's single To+CC message lands; add the same wording to the
   Office path. Wins because E1 mandates one body addressed To one party with the rest CC'd
   -- a CC'd recipient may be registered OR not, and the sender cannot branch per CC. Both
   affordances in one body covers every recipient; the user can navigate to register from the
   login page, so a login link plus a short "or register" line is sufficient.
2. (REJECTED) Keep the three role-aware templates and only add Register/Login wording to the
   Office template. Rejected because it leaves the per-recipient fan-out in place, directly
   contradicting E1's locked single-message model; a single message cannot carry three
   per-recipient bodies.
3. (REJECTED) Keep branching but compute "registered vs not" for the single To party only.
   Rejected because CC'd parties (the majority of recipients on a multi-party appointment)
   would then get a body tailored to someone else -- e.g. unregistered CC recipients see a
   "log in" body with no register affordance, defeating the feedback's intent.

## Decision (locked 2026-06-03)
ONE shared, non-role-aware Appointment Request body. It explicitly says to log in OR register
to view the appointment and includes a login link (register is reachable from the login page).
Consolidate the Registered/Unregistered/Office wording into this single body when E1's single
To+CC message lands, and ensure the Office path carries the same wording. The patient packet
remains a separate email (out of E2 scope). The wording change rides E1; E2 alone is wording.

## Implementation outline (no code)
1. Sequence after E1: E2's single-body consolidation depends on E1 having one To+CC message
   and one variable set. Until E1 lands, the three templates stay.
2. Author the consolidated body in `EmailBodies/` (reuse the existing AppointmentRequested
   code that E1 settles on, or a new code if E1 mints one): generic greeting (no
   `##RoleDisplayName##` branching), the confirmation/date/booker/worker block, one line
   "Log in to view this appointment -- or register if you do not yet have a portal login for
   this practice," a primary "Log in to view appointment" button to `##LoginUrl##`, and a
   secondary register link/line to `##RegisterUrl##`.
3. If a NEW consolidated code is introduced: add the const to `NotificationTemplateConsts.Codes`,
   append to `Codes.All`, add the subject to `EmailSubjects.cs`, ship the `.html` as
   `<EmbeddedResource>` (avoids the silent-empty-body gotcha). If the existing code is reused,
   editing its `.html` auto-propagates via the seeder overwrite.
4. Handler: in `BookingSubmissionEmailHandler.cs:329-459`, ensure the single variable set
   supplies both `##LoginUrl##` and `##RegisterUrl##` (and drop the per-recipient template
   selection that E1 removes). Keep BUG-014/BUG-029 URL builders intact.
5. Migration: NONE (template rows are data-seeded, not schema). No proxy regen (no DTO/contract
   change). Enforcement is content-only -- no server-side validation; this is a UI/affordance
   wording change, not a security/integrity rule, so it stays template-only.
6. Verify on re-seed that the consolidated body renders with both URLs populated and that the
   Office recipient now sees the same wording.

## Dependencies
- DEPENDS ON E1 (single To+CC message + single variable set). E2 cannot deliver one shared
  body until the fan-out is collapsed; without E1, the wording stays in three templates.
- Loosely related to E3 (document-email To/CC) -- same notification subsystem, but no shared
  code path; not a hard dependency.

## Residual open questions
- Whether to reuse the existing AppointmentRequested template code or mint a single new
  consolidated code is an E1 decision (E1 settles the code/addressing shape); E2 inherits it.
- "Mention the login link" is satisfied by the existing styled button (href=`##LoginUrl##`);
  a literal visible URL string is not required unless product asks (minor).
