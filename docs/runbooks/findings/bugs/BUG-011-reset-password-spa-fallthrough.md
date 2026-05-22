---
id: BUG-011
title: Password-reset email link lands on SPA /error (no UI handler)
severity: high
status: needs-rehydration
found: 2026-05-13
flow: password-reset
component: angular/src/app/account/reset-password (suspected — needs verification)
---

# BUG-011 — Password-reset link falls through to OAuth challenge

> **Verification 2026-05-22: FIXED-BY-REDESIGN (confidence 90%).**
>
> Closed by PR #201 (`feat(auth): wire AuthServer forgot-password and drop SPA auth UI`, merged 2026-05-15, commit `1c79858`). The SPA `/account/*` routes -- where the broken fallthrough lived -- were **deleted entirely**. Confirm at `angular/src/app/app.routes.ts:38-47`: comment dated 2026-05-15 reads "SPA `/account/*` routes are gone... All authentication UI ... is hosted by the AuthServer Razor pages." No `account` child folder remains under `angular/src/app/`.
>
> The new flow lives at `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/ResetPassword.cshtml.cs:42-145` -- a standalone `ResetPasswordModel` that binds `UserId` + `ResetToken` from query, renders the form on GET, calls `IExternalAccountAppService.ResetPasswordAsync` on POST, and handles `ResetPasswordTokenInvalid` / `UserFriendlyException` gracefully. Further hardened by PR #210/#222: `AccountUrlBuilder.BuildPasswordResetUrlAsync` (in `src/HealthcareSupport.CaseEvaluation.Application/Notifications/AccountUrlBuilder.cs:48-54`) composes `{AuthServerBaseUrl}/Account/ResetPassword?userId=...&resetToken=...` with the tenant subdomain.
>
> **Residual concern (live-verify, non-blocking):** the new throw on missing `App__SelfUrl` env var means a misconfigured stack fails loud (email never sent) instead of producing the SPA `/error` symptom -- different failure mode, still a hard fail. Worth one real reset click on the Falkinstein demo tenant to close the loop.
>
> **Action: doc-close.** Update frontmatter to `status: fixed-by-redesign`, `closed-by: PR #201` (with `+ PR #210, #222` hardening). The original broken pattern (SPA `/account/reset-password` route) no longer exists in the codebase.

## Severity
high

## Status
**Needs rehydration.** Documented in earlier session compact summary; full repro to be added when re-encountered.

## What's known from earlier session
- Click the reset-password link in the email body.
- SPA navigates to `/account/reset-password?userId=...&resetToken=...`.
- Instead of rendering a reset form, the SPA falls through to `/error` or kicks off an OAuth challenge.
- Workaround used in prior session: call `POST /api/public/external-account/reset-password` via `curl` directly to bypass the broken UI.

## To do
- Verify SPA route exists for `/account/reset-password`.
- If missing, file the SPA component as the fix scope.
- If present, capture the SPA console/network logs to see why it errors.

## Suspected root cause
Either:
- The Angular route `/account/reset-password` is not registered in `app.routes.ts` or feature route provider.
- Or the route exists but the component's APP_INITIALIZER / route guard rejects unauthenticated traffic and redirects.
- Same shape as [[BUG-006]] (email-confirmation URL was hardcoded wrong) — possibly the email template builds a URL that doesn't match any route.

## Related
- [[BUG-014]] (hardcoded URLs in email templates) — if reset-password URL is hardcoded with a path that doesn't match the SPA route, BUG-014's fix should also fix this one.
