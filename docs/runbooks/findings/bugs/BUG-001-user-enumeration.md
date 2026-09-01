---
id: BUG-001
title: Register form leaks user enumeration via duplicate-email error
severity: high
status: fixed
fixed-in: PR #197
found: 2026-05-13
flow: external-user-registration
component: AuthServer Account/Register + Application/ExternalSignups/ExternalSignupAppService.cs
---

# BUG-001 — Register form leaks user enumeration

## Severity
high (HIPAA + OWASP A07:2021 — Identification & Authentication oracle)

## Status
**FIXED in PR #197** — generic localized message replaces email-echo response. Rate-limit added to `/api/public/external-signup/register`.

## Affected role
Patient (also affects Applicant Attorney / Defense Attorney / Claim Examiner)

## Steps to reproduce
1. Navigate to `http://falkinstein.localhost:44368/Account/Register`.
2. Type any existing email — e.g. `SoftwareThree@gesco.com` (seeded with EmailConfirmed=0).
3. Fill First Name "Software", Last Name "Three", Password `1q2w3E*r`, Confirm `1q2w3E*r`.
4. Click "Sign Up".

## Expected (OWASP A07:2021 + healthcare-domain caution)
Registration response must NOT confirm or deny whether an email is already registered. Industry practice: return a generic acknowledgement like *"If this email is new, you will receive a verification message shortly. If it is already registered, sign in instead."* The registration endpoint must not be an enumeration oracle. In a healthcare context (HCS Patient Portal handles PII associated with workers' comp evaluations), confirming an email maps to an existing Patient is doubly sensitive — it tells an attacker that the person is a patient at this practice.

## Actual (pre-fix)
- Server returned HTTP 403 with body `{"error":{"code":null,"message":"Email address is already used: SoftwareThree@gesco.com",...}}`.
- Browser rendered a native `window.alert()` with the literal email echoed back.
- No rate limiting observed.

## Evidence
- Network: POST `/api/public/external-signup/register` → 403
- Correlation ID: `68469a17cc0a4032aa9c84cd456cd769`
- Repro via the BUG-002 alert dialog could not be screenshotted (native alert).

## Root cause
`ExternalSignupAppService.cs:443` threw `UserFriendlyException` with `L["Email address is already used: {0}", input.Email]`. The literal email is echoed back; the response is also a `UserFriendlyException` which ABP 10.0.2 maps to HTTP 403 by default — see BUG-003 for the status-code half.

## Recommended fix (applied in PR #197)
- Replace duplicate-email throw with `BusinessException` carrying `CaseEvaluationDomainErrorCodes.ExternalSignupDuplicateEmail` + a generic localized message that does NOT echo the email.
- Map the error code to HTTP 400 via `AbpExceptionHttpStatusCodeOptions` (BUG-003).
- Add rate-limit on `/api/public/external-signup/register` (extends existing `ConfigurePasswordResetRateLimiter` pattern).

## OLD source
`P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs` (AddValidation) — also returns a duplicate-email message; long-standing leak inherited verbatim. Per project CLAUDE.md bug-and-deviation policy this is a clear security bug — fix in NEW silently rather than replicate.

## Parity doc
`docs/parity/wave-1-parity/external-user-registration.md`

## Related
- [[BUG-002]] (native alert)
- [[BUG-003]] (403 vs 400 status code)
