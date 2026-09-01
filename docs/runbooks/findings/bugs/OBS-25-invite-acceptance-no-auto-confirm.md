---
id: OBS-25
title: Invite acceptance does not auto-confirm email; second verification email + click required
severity: observation
status: open
found: 2026-05-23 hardening HRD-P1.B (reconfirmed; first observed in 2026-05-21 run)
flow: invite-registration
component: src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs + InvitationManager.AcceptAsync
---

# OBS-25 - Invite-accept does not auto-confirm email

## Symptom

Confirmed twice during the 2026-05-23 hardening run (defatty1 + claimE1
invite-flow registrations). The flow:

1. `stafsuper1` issues an invite via POST `/api/app/external-signup/invite`
   for `defatty1@gesco.com` with role=DefenseAttorney.
2. The invitee receives an email with a registration link:
   `http://falkinstein.localhost:44368/Account/Register?inviteToken=<32-byte-base64>`.
3. The invitee opens the link. Email + role are correctly locked /
   prefilled in the form. They set first/last name + firm name + password,
   accept T&C, submit.
4. `POST /api/public/external-signup/register` returns 200; `AppInvitations`
   row gets `AcceptedAt = now`.
5. The user record is created in `AbpUsers` with `EmailConfirmed = 0`.
6. A SECOND email is sent ("You have registered successfully ... Click
   here to verify"), containing another `/Account/EmailConfirmation?userId=&confirmationToken=` URL.
7. The user must click THIS second link to flip `EmailConfirmed = 1`.

## Why this is observation-worthy

The invitee already proved control of the email address by clicking the
invite-token URL (which was sent by the AuthServer to that email). Asking
them to click a SECOND verification link doesn't gain any security; it
just adds a step the user might fail to complete.

Two paths the post-acceptance handler could take:

- **Auto-confirm**: trust the invite-token round-trip as proof of email
  ownership, mark `EmailConfirmed = 1` synchronously in
  `AcceptAsync`. Don't send the post-register verification email.
- **Skip the second email but keep `EmailConfirmed = 0`**: arguably the
  worst of both worlds (user stays unverified silently).

The current behavior (send + require second click) is the third option,
which is what the prior 2026-05-21 run and this run both observed.

## Recommended fix

In `InvitationManager.AcceptAsync` (or wherever `RegisterAsync` is
called for invite-acceptance), set `EmailConfirmed = true` and skip the
post-register verification email queue when the registration is being
completed via a valid invite-token. The verification email + URL flow
should only kick in for the manual `/Account/Register` self-signup path.

## Functional impact

Cosmetic + drop-off risk. Some invitees will abandon at the "click the
verify link" step because they assume the registration succeeded.

## Related

- HRD-P1.B (this run's invite-flow scenarios)
- BUG-029 (Hangfire URL host - now fixed)
- 2026-05-21 archived state file at `.hardening-run/2026-05-21.archived.json`
- OBS-21 (claime1-verification-not-recorded - now closed; the
  second-verify flow does flip EmailConfirmed correctly).
