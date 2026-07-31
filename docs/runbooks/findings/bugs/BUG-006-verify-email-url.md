---
id: BUG-006
title: Email-verification SPA hits wrong endpoint (405 Method Not Allowed)
severity: blocker
status: fixed
fixed-in: PR #197
found: 2026-05-13
flow: external-user-registration -> email confirmation -> first login
component: angular/src/app/shared/auth/custom-email-confirmation.component.ts
---

# BUG-006 — Email-verification URL wrong

## Severity
blocker

## Status
**FIXED in PR #197** — SPA now POSTs to `/api/account/confirm-email` (correct ABP 10.0.2 endpoint).

## Affected role
Every external user that must verify email before first login (Patient / AA / DA / CE)

## Steps to reproduce
1. Register a new external user.
2. Open the verification email; navigate the link.
3. SPA shows *"We could not verify your email. The link may have expired."* (Token is fresh.)

## Expected
- API POST `/api/account/confirm-email` with `{userId, token}` → 204 No Content.
- SPA shows *"Email verified — proceed to sign in"* + Sign In CTA.

## Actual (pre-fix)
- POST `/api/account/verify-email` → **405 Method Not Allowed** (route doesn't exist with that name; Swagger probe confirmed both `main-api-1` and `replicate-old-app-api-1` 405 the wrong path and 404-auth-required the right path).
- SPA error UI generically blamed token expiry regardless of HTTP status.

## Evidence
```
POST http://falkinstein.localhost:44328/api/account/verify-email
request body: {"userId":"97e54a2b-...","token":"CfDJ8LV2..."}
response status: 405
```

## Root cause
`angular/src/app/shared/auth/custom-email-confirmation.component.ts:112` hardcoded `/api/account/verify-email`. The actual ABP 10.0.2 endpoint is `/api/account/confirm-email` (originally `/api/account/verify-email-confirmation-token` per first investigation; final fix landed at `/api/account/confirm-email`).

## Recommended fix (applied in PR #197)
Single-character path change:
```typescript
this.http.post(`${environment.apis.default.url}/api/account/confirm-email`, {
    userId: this.userId,
    token: this.confirmationToken,
}),
```
Same JSON body shape — no proxy regen needed.

## Related follow-up
SPA's error UI should branch on HTTP status (4xx-other-than-410 should say *"We could not verify your email — please try again or request a new link"*, not always blame token expiry). Polish; not blocker.

## OLD source
`P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\UserAuthenticationController.cs` (PutEmailVerification PUT endpoint — different verb)

## Parity doc
`docs/parity/wave-1-parity/external-user-registration.md`
