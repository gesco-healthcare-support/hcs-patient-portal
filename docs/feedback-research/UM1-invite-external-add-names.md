---
id: UM1
title: Invite External User form should collect First/Last Name, persist them, and address invite emails by name
type: enhancement
components: [angular/src/app/external-users/components/invite-external-user.component.ts, angular/src/app/external-users/components/invite-external-user.component.html, src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/InviteExternalUserDto.cs, src/HealthcareSupport.CaseEvaluation.Domain/Invitations/Invitation.cs, src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs, src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html, src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/]
related_known_bugs: [OBS-27, OBS-25, OBS-28, OBS-29]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

The Invite External User form collects only Email + Role. The resulting invite email renders
an empty greeting ("Hi ,") because no name is captured at invite time. Add First Name and
Last Name fields to the form, persist them on the Invitation aggregate, and thread them into
the invite email greeting. This directly fixes OBS-27.

## Current behavior (from investigation)

- Form collects only email + userType. The reactive form group has no name controls
  (invite-external-user.component.ts:51-54; invite-external-user.component.html:16-54 -- Email
  + Role select only).
- Input DTO carries only Email + UserType (InviteExternalUserDto.cs:24-38; verified: properties
  are exactly `Email` [Required/EmailAddress/StringLength(256)] and `UserType` [Required]).
- The Invitation aggregate persists Email, UserType, TokenHash, ExpiresAt, AcceptedAt,
  AcceptedByUserId, InvitedByUserId -- no name columns (Invitation.cs:30-108; verified: the
  internal ctor at :92-108 takes id/tenantId/email/userType/tokenHash/expiresAt/invitedByUserId,
  no names).
- Names are first captured later, at registration ACCEPTANCE, into
  ExternalUserSignUpDto.FirstName/LastName (ExternalUserSignUpDto.cs:45-49) and mapped to
  IdentityUser.Name/Surname (ExternalSignupAppService.cs:524-525).
- The invite email greeting token is deliberately blank: BuildInvitationVariables sets
  PatientFullName / PatientFirstName / PatientLastName to string.Empty
  (ExternalSignupAppService.cs:997-1002, verified) with the comment that no name is collected at
  invite time. The InviteExternalUser.html body references `##PatientFullName##`, producing the
  empty "Hi ," greeting (OBS-27).
- AppInvitations is mapped on BOTH DbContexts (CaseEvaluationDbContext.cs and
  CaseEvaluationTenantDbContext.cs -- confirmed both reference the entity), so any column change
  needs a single migration that the host and tenant migration runners both apply.

## Relevant code locations

Frontend:
- angular/src/app/external-users/components/invite-external-user.component.ts:51-54 (form group)
- angular/src/app/external-users/components/invite-external-user.component.html:16-54 (template)

Backend:
- src/.../Application.Contracts/ExternalSignups/InviteExternalUserDto.cs:24-38 (input DTO)
- src/.../Domain/Invitations/Invitation.cs:30-108 (aggregate: add columns + ctor params)
- src/.../Domain/Invitations/InvitationManager.cs (IssueAsync signature threads names)
- src/.../Application/ExternalSignups/ExternalSignupAppService.cs:868-944
  (InviteExternalUserAsync), :983-1004 (BuildInvitationVariables), :460-676 (RegisterAsync)
- src/.../Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html (greeting tokens)
- src/.../HttpApi/Controllers/ExternalUsers/ExternalUserController.cs:41-48 (POST /invite passthrough -- no change)
- src/.../EntityFrameworkCore/EntityFrameworkCore/{CaseEvaluationDbContext.cs,CaseEvaluationTenantDbContext.cs} (entity config) + new migration

## Phase 3 cross-reference

- OBS-27 (invite-email-empty-greeting): exact root cause. This item is the fix; close OBS-27 with it.
- OBS-28 (send-invite-no-success-toast): same form, no success toast. Bundle: while editing the
  component, add a toast on invite success (the component already renders an inline result card).
- OBS-29 (cookie-consent-overlays-invite-form): GDPR overlay obstructs the same form. Bundle
  only if the overlay blocks the new name fields during manual/Playwright verification; otherwise
  leave as its own item.
- OBS-25 (invite-acceptance-no-auto-confirm): adjacent accept-flow behavior; NOT in scope here,
  cross-listed only because it touches the sibling register/accept path.

## Research findings

- Internal patterns / prior art:
  - Name collection already exists at the accept step (ExternalUserSignUpDto.FirstName/LastName,
    ExternalSignupAppService.cs:524-525) -- reuse the same StringLength conventions for the new
    invite-time fields so both ends agree.
  - The Invitation aggregate uses protected setters + an internal ctor and is created via
    InvitationManager.IssueAsync (Invitation.cs:88-108; Invitations CLAUDE.md "Lifecycle").
    New name fields must be ctor params, not public setters, to keep the aggregate's encapsulation.
  - BuildInvitationVariables already emits PatientFirstName/PatientLastName/PatientFullName tokens
    as defensive zero-fills (ExternalSignupAppService.cs:997-1002); we replace the empty strings
    with the persisted invite names. The HTML already references `##PatientFullName##`, so the
    template likely needs no token change -- only a non-empty value.
  - Email casing: callers must lowercase email before IssueAsync (Invitations CLAUDE.md gotcha).
    Names need no normalization beyond trim; do not lowercase names.
- External docs (ABP / EF Core) if relevant:
  - EF Core code-first: adding nullable columns to an existing table is a non-destructive
    additive migration (no default backfill needed for nullable). Generate one migration; the
    host + tenant migration runners (Domain/Data/CaseEvaluationDbMigrationService) both apply it
    because AppInvitations is mapped on both contexts.

## Approaches considered (with tradeoffs)

1. Persist names on the Invitation aggregate (CHOSEN). New nullable FirstName/LastName columns,
   threaded end to end. Pros: durable; available to the email greeting AND any later accept-time
   prefill; single source of truth; matches how Email/UserType already flow. Cons: schema change
   + migration on both contexts.
2. Pass names only as transient email variables, do not persist. Pros: no migration. Cons: names
   would be lost if the email is re-sent/resent (resend = new invite, Invitations CLAUDE.md), and
   they could not prefill registration later. Rejected: loses the durable value for marginal
   savings.
3. Capture names only at registration (status quo). Rejected: that is exactly the OBS-27 bug --
   the greeting is empty because nothing is known at invite time.

Why CHOSEN wins: the feedback explicitly says "stored, used in emails." Persisting on the
aggregate is the only option that satisfies "stored," keeps the email greeting correct, and
leaves the door open to prefill registration -- at the cost of one additive, non-destructive
migration.

## Decision (locked 2026-06-03)

Add First Name + Last Name to the invite form; persist on the Invitation aggregate (new nullable
columns + one EF migration applied to host AND tenant contexts); thread into
BuildInvitationVariables and the InviteExternalUser.html greeting (fixes the "Hi ," empty
greeting, OBS-27). Optionally pre-fill name at registration acceptance (see open question).
Enforcement: name length/shape validated server-side on the DTO and mirrored in the UI form;
names are an affordance for the greeting, so required-vs-optional is the only product gate (see
open questions) -- not a security invariant.

## Implementation outline (no code)

1. Domain.Shared: add FirstName/LastName max-length consts to InvitationConsts (mirror the
   accept-time ExternalUserSignUpDto lengths for consistency).
2. Domain: add nullable `FirstName` / `LastName` (protected setters) to Invitation.cs; extend the
   internal ctor params; update InvitationManager.IssueAsync to accept and pass names.
3. Application.Contracts: add `FirstName` / `LastName` to InviteExternalUserDto with
   StringLength (and [Required] only if the product decides names are mandatory -- server-side).
4. Application: in InviteExternalUserAsync (ExternalSignupAppService.cs:868-944) pass the DTO
   names to IssueAsync; change BuildInvitationVariables (:997-1002) to read the persisted invite
   names instead of string.Empty (it currently builds from method args -- add name params or read
   from the Invitation row). Set PatientFullName = trimmed "First Last".
5. EntityFrameworkCore: add the two columns to the AppInvitations config (it is already mapped on
   both DbContexts) and generate ONE additive migration; both host + tenant runners apply it.
   MIGRATION FLAG: schema change.
6. Email template: confirm InviteExternalUser.html `##PatientFullName##` now renders the name;
   no token rename expected.
7. Angular: add firstName/lastName controls to the form group (component.ts:51-54) and inputs to
   the template (component.html:16-54) with validators mirroring the server DTO. PROXY REGEN:
   regenerate proxies (abp generate-proxy) after the DTO changes -- do NOT hand-edit
   angular/src/app/proxy/.
8. Optional (if product says yes): at RegisterAsync (:460-676) prefill IdentityUser.Name/Surname
   from the invite's stored names when the form leaves them blank.

Server-vs-UI enforcement: validation lives on InviteExternalUserDto (server) and is mirrored in
the Angular reactive form (UI). The greeting itself is presentation-only.

## Dependencies

- Standalone; no hard dependency on other UM items. Touches the same User Management nav cluster
  as UM2/UM3/UM4 but does not block or require them.
- Bundles cleanly with OBS-27 (fix), and opportunistically OBS-28 (success toast) since both edit
  the same component.

## Residual open questions

- Required vs optional on the invite form: decision needed. Recommend OPTIONAL (the email greeting
  degrades gracefully to a generic salutation when blank; OLD had no tokenized invite so there is
  no parity to honor). If optional, the greeting must fall back to a name-less salutation rather
  than re-introducing "Hi ,".
- Registration prefill precedence: if a recipient types a different name at registration than the
  invite stored, recipient-typed wins (they are claiming their own identity). The stored invite
  name is only a default/greeting source.
