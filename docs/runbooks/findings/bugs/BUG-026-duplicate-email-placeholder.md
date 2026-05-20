---
id: BUG-026
title: Registration "DuplicateEmail" message renders literal "{0}" instead of substituting the email
severity: low
status: open
found: 2026-05-19
flow: external-user-registration
component: src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:501-503 + Localization/CaseEvaluation/en.json:458
---

# BUG-026 — `{0}` placeholder unfilled in DuplicateEmail message

## Severity

low (cosmetic / poor UX — but also a leftover hint of the BUG-001 email-echo pattern, so it deserves explicit treatment so future edits don't accidentally restore enumeration)

## Status

open — fix is one line.

## Affected role

Any external user (Patient, Applicant Attorney, Defense Attorney, Claim Examiner) hitting `POST /api/public/external-signup/register` with an already-registered email.

## Steps to reproduce

1. Pick an email that already exists, e.g. `patient1@gesco.com`.
2. POST to `http://falkinstein.localhost:44368/api/public/external-signup/register` with that email and any valid name + password (or use the Register UI at `/Account/Register`).
3. Observe the error toast / response body.

## Expected

Either:

- a fully-rendered message with the email interpolated where `{0}` lives (matches the en.json template literally), or
- a generic message with no `{0}` token at all.

## Actual

The user sees the literal text:

> "We sent a verification link to {0}. Already have an account? Sign in or reset your password."

The `{0}` is shown verbatim because the throw site passes the localized string as a plain message argument with no format args:

```csharp
// src/.../Application/ExternalSignups/ExternalSignupAppService.cs:501-503
throw new UserFriendlyException(
    message: L["Registration:DuplicateEmail"],
    code: CaseEvaluationDomainErrorCodes.RegistrationDuplicateEmail);
```

`L["Registration:DuplicateEmail"]` returns the raw template (with `{0}` still present); ABP's `IStringLocalizer` formatter only substitutes when arguments are supplied via `L["key", arg1, arg2]`. There is no email arg here, so the placeholder leaks.

## Why this exists

The placeholder dates back to a BUG-001 (user-enumeration) earlier intermediate state where the email was echoed back. The fix that landed (PR #197) replaced the email-echoing message with a generic one but left the `{0}` token in the template by mistake.

## Recommended fix

Two acceptable shapes. Pick one:

**A. Remove the placeholder (preferred — matches the BUG-001 fix intent).**

```json
// Localization/CaseEvaluation/en.json:458
"Registration:DuplicateEmail": "We sent a verification link to your email. Already have an account? Sign in or reset your password.",
```

No throw-site change needed.

**B. Fill the placeholder (only acceptable if the user already knows their own email).**

```csharp
throw new UserFriendlyException(
    message: L["Registration:DuplicateEmail", input.Email],
    code: CaseEvaluationDomainErrorCodes.RegistrationDuplicateEmail);
```

`input.Email` is the value the user just submitted, so echoing it back is **not** a new enumeration leak (the caller already knew it). However A is safer because it removes the temptation to "improve" the message later with a server-side resolved email and accidentally re-introduce BUG-001.

## Test plan

- Manual: register with an existing email, confirm the rendered text matches the chosen fix.
- Optional unit test: snapshot the localized string against a fixture so future en.json edits don't quietly reintroduce a `{N}` token without a matching format arg.

## Links

- BUG-001 (user-enumeration leak) — the parent constraint this template's wording must honor.
- PR #197 — fix for BUG-001 / BUG-002 / BUG-003 that introduced the current generic-but-broken template.
