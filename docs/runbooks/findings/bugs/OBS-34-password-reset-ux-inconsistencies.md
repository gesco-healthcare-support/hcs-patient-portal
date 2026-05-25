---
id: OBS-34
title: Password reset flow UX inconsistencies - false sign-in promise + consumed-token form re-render
severity: observation
status: open
found: 2026-05-23 hardening HRD-P9.2
flow: password-reset
component: src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/ResetPassword.cshtml{,.cs}
---

# OBS-34 - Password reset flow has two UX inconsistencies

## Symptom

Phase 9.2 hardening confirmed the password-reset round-trip WORKS:

- Real email -> POST `/api/public/external-account/send-password-reset-code`
  returns HTTP 204 (success).
- Hangfire enqueues a `SendAppointmentEmailJob` with the reset URL
  embedded.
- Clicking the URL renders `/Account/ResetPassword?userId=&resetToken=`
  with password + confirm-password fields.
- Submitting valid passwords redirects to
  `/Account/Login?flash=password-updated`.
- Old password is rejected (HTTP 200 = login page reload, no auth cookie).
- New password succeeds (HTTP 302 + `.AspNetCore.Identity.Application` cookie set).

So functionally the flow is correct. Two cosmetic / UX issues surfaced:

### Issue 1: Reset page promises auto-sign-in but doesn't deliver

The `/Account/ResetPassword` page renders this subtitle:

> "Choose a new password. **We'll sign you in after you save.**"

But the actual behavior after a successful reset is:

- 302 redirect to `/Account/Login?flash=password-updated&ReturnUrl=%2F`
- Flash message: "Password updated. Sign in with your new password."

So the user is told "we'll sign you in" but is then asked to sign in
manually. The mismatch is confusing.

### Issue 2: Consumed reset token still renders the form

After successfully using a reset token, navigating BACK to the same
URL (consumed-token GET) renders the ResetPassword form again with
no warning that the link has been used:

- Page heading still says "Reset your password".
- No banner/alert saying "This link has been used" or "Request a new
  link".
- The user could fill in a new password and submit, hitting the
  consumed-token failure at POST time (which DOES redirect them back
  to `/Account/ForgotPassword` correctly - so the failure handling
  works, just not the proactive UI message).

This is similar to OBS-30 (EmailConfirmation tampered-token on
already-verified user) - both are idempotency-first designs that
short-circuit information surfacing.

## Recommended fix

### Fix 1: align promise with behavior

Either:
- Actually sign the user in after reset (set the auth cookie at the
  end of the OnPostAsync handler, then redirect to root with a
  welcome flash). This is what the page text promises.
- Or change the subtitle to: "Choose a new password. We'll redirect
  you to sign in after you save."

### Fix 2: detect consumed tokens at GET time

The `ResetPassword.cshtml.cs` OnGetAsync could call
`UserManager.VerifyUserTokenAsync(...)` to check token validity. If
expired/consumed, render an alternate view with:

> "This reset link has been used or has expired. Need a new one?"
> [Request one]

This matches the "consumed link" UX pattern documented in OWASP ASVS.

## Verified positive findings

During the same Phase 9.2 sweep:

- **P9.2.e anti-enumeration**: PASS. Both real and non-existent emails
  return the same `/Account/ForgotPassword` success card ("If the email
  matches an account, we sent a reset link.").
- **P9.2.f throttle**: PASS. After 1 request from this IP, a second
  request returned HTTP 429. (The 5/hr limit per PR #197 fires
  aggressively from this IP because earlier hardening probes also
  counted; throttle is per-IP.)
- **Old password rejected after reset**: PASS.
- **New password works**: PASS.
- **Email body greeting**: works correctly (says "Hello, Daniel" - the
  patient's first name is in scope at reset time, unlike invite emails
  per OBS-27).

## Repro

```bash
# 1. POST send-password-reset-code (or use the /Account/ForgotPassword form)
curl -X POST http://falkinstein.localhost:44327/api/public/external-account/send-password-reset-code \
  -H "Content-Type: application/json" \
  -d '{"email":"patient1@gesco.com","appName":"MVC","returnUrl":"/"}'

# 2. Grab the URL from HangFire.Job (latest row), extract userId + resetToken.

# 3. Navigate to the URL, submit new password.
#    Observe: page says "We'll sign you in" but redirects to /Account/Login.

# 4. Navigate to the SAME URL again (consumed-token GET).
#    Observe: same form renders, no consumed warning.

# 5. Try OLD password at /Account/Login -> rejected (200).
# 6. Try NEW password at /Account/Login -> 302 + cookie set.
```

## Functional impact

Cosmetic. The flow works; the two issues are usability gaps:
- Issue 1 confuses users who expect to be auto-logged in.
- Issue 2 risks the user thinking they need to set the password again,
  potentially attempting a second reset.

## Related

- OBS-30 (EmailConfirmation idempotency short-circuit on
  already-verified user - same UX pattern family).
- OBS-25 (invite-accept does not auto-confirm - same "second-step
  required" friction family).
- BUG-011 (reset-password SPA fall-through - now superseded by the
  AuthServer Razor flow used here, which works).
- HRD-P9.2.a / .b / .e / .f scenarios.
