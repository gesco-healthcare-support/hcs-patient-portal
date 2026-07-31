---
id: OBS-30
title: /Account/EmailConfirmation returns success flash for tampered token when target user is already verified
severity: observation
status: open
found: 2026-05-23 hardening HRD-P1.C.2
flow: email-verification-edge-cases
component: src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/EmailConfirmation.cshtml.cs (suspected OnGetAsync)
---

# OBS-30 - Tampered token on already-verified user returns success flash

## Symptom

P1.C.2 scenario - tamper one character of the confirmation token in an
otherwise-valid `/Account/EmailConfirmation` URL, then visit:

1. Took the URL just used to verify `claime1@gesco.com`:
   `http://falkinstein.localhost:44368/Account/EmailConfirmation?userId=35dc966d-...&confirmationToken=CfDJ8PYI4ehG...`
2. Mutated the first character of the confirmation token (`CfDJ8` -> `CfDJ9`).
3. Opened the tampered URL.

**Expected**: 302 -> `/Account/Login?flash=verification-invalid` (per
runbook P1.C.2 and prior B-2 / B-3 design notes).

**Observed**: 302 -> `/Account/Login?flash=email-verified` (SUCCESS flash).
No stack trace, no `IdentityResult.Errors` leak, no userId echo - so the
output is safe. But the success flash is incorrect.

## Why this is observation-worthy (not a bug)

Three competing interpretations:

1. **Idempotency short-circuit (most likely)**: the handler checks
   `user.EmailConfirmed` BEFORE validating the token. Once
   `EmailConfirmed=true`, ANY GET with that userId returns the success
   flash regardless of token validity. This is defensible UX (don't
   confuse already-verified users with errors) but masks tamper
   detection on verified accounts.

2. **Token actually validates**: improbable - changing one base64 char
   should fail integrity check on the DataProtection-protected token.
   But not impossible if the first character happens to be padding /
   ignored by the format.

3. **Empty-error early-return**: the handler may treat tampered tokens
   as "no change required" rather than "invalid".

For unverified users, the tamper-reject path is presumably still
exercised correctly (Round 2 R2.14 would confirm). But for the
common case where the user IS already verified (e.g., they save the
verify URL and a year later someone forwards a tampered version), the
attacker can't tell the difference from a legitimate idempotent re-click.

## Why this is acceptable as-is (probably)

The attack-value of "tampered token returns success flash on already-
verified user" is essentially zero. The attacker:
- Did NOT obtain a valid login session.
- Did NOT learn whether the userId exists (R2.14 anti-enumeration
  covers that case for unverified users).
- Only got a misleading flash message.

The information leak is minimal.

## Recommended next step

1. Decide whether the idempotency-first design is intentional:
   - YES: document the design choice in
     `docs/security/email-verification.md` and update runbook P1.C.2 to
     accept either `flash=email-verified` OR `flash=verification-invalid`
     for already-verified users.
   - NO: reorder `EmailConfirmationModel.OnGetAsync` to validate the
     token FIRST, then idempotency-check, so tampered tokens always
     return `flash=verification-invalid`.

2. R2.14 (fake userId + valid-looking token, anti-enumeration) was not
   driven in this run; that's the true tamper-reject test for unverified
   users. Add to next-run scope.

## Related

- HRD-P1.C.2 (the scenario that surfaced this).
- B-2 fix (Adrian's 2026-05-18 work on EmailConfirmationModel).
- R2.13 / R2.14 / R2.15 (not driven this run).
