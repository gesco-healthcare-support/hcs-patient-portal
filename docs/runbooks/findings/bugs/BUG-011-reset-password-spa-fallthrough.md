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
