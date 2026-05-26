---
title: Error / edge-state page survey
date: 2026-05-26
status: ready
audience: Adrian (presenter)
---

# Error / edge-state pages

What an audience could see if they navigate to a non-demo URL or
trip a server error. All pages probed live 2026-05-26 03:45 PT.

## AuthServer Razor pages

| URL | Title | Copy |
|---|---|---|
| /Account/AccessDenied | "Access denied" | "You don't have access to this page." + Back to sign-in link |
| /Account/LockedOut | "Account locked" | "Too many failed sign-in attempts. Try again in 1 hour. If this wasn't you, contact the clinic." + Back to sign-in |
| /Account/LoggedOut | redirects to /Account/Login when accessed directly | Normal Sign-in page renders |
| /Account/ResendVerification | "Verify your email" | "Click the link we sent to verify your address. Didn't get it? Resend below." + email input + Resend button |
| /Account/EmailConfirmation (no token) | redirects to /Account/Login?flash=verification-invalid | Flash: "That verification link doesn't work anymore. Resend below." |
| /Account/ForgotPassword | "Forgot your password?" | "Enter your email and we'll send you a reset link." + form |
| Reset request success | same page reloaded | "If the email matches an account, we sent a reset link. Check your inbox and spam folder." -- generic, doesn't leak email existence |

All copy is professional, branded as "Appointment Portal", no
raw exception traces.

## SPA SPA error / 404 routes

| URL | Behavior |
|---|---|
| /error | Renders ABP default error page with heading "0" + "Go to the homepage" link. Bare. Audience landing here would just see a near-blank page. |
| /this-page-does-not-exist | Auto-redirects to / (homepage). No 404 page. |

**Quirk:** The bare "/error" page shows "0" as the heading when the
underlying status code isn't set -- e.g. if a viewer types
`/error` into the URL bar directly. Production polish item, not
demo-critical (no flow lands here unprompted).

## Demo tactics

- If audience triggers 403 (e.g. Patient navigates to /dashboard),
  the SPA renders a friendly "403 Forbidden / Go to the homepage"
  page. Verified earlier with patient1 + /dashboard.
- If audience triggers a typo URL, the SPA bounces them back to /.
  Safe.
- If audience triggers account lockout (5+ bad sign-ins), the
  /Account/LockedOut page shows a clear 1-hour cool-down message.
- If audience asks "what happens if email isn't verified yet" --
  /Account/ResendVerification handles it cleanly.

## Rate limiter (BUG-019) -- verified in prior session

From the Sun-night hand-off doc (2026-05-24):
- Same email 7 attempts: 5x 204 -> 2x 429 (per-email cap working)
- Different email same IP: 3x 204 -> no rate limit (per-email
  partitioning, no shared-IP DoS)

Live curl probe tonight returns 400 because CSRF tokens are
required on the Razor POST -- browser-flow inclusion of the
`__RequestVerificationToken` is required. Rate limit logic still
holds; just can't repro via curl without scraping the token.

## Coverage matrix

| Page | Verified |
|---|---|
| Login | yes |
| Register | yes (Flow 1) |
| Forgot Password | yes -- generic success msg |
| Reset Password (with token) | path tested -- requires email link |
| Resend Verification | yes |
| Email Confirmation (invalid token) | yes -- flash redirect |
| Account Locked | yes -- "Try again in 1 hour" |
| Access Denied | yes -- clean copy |
| Logged Out | yes -- bounces to Login |
| SPA 403 | yes (Patient on /dashboard) |
| SPA 404 (bogus URL) | yes -- redirects to / |
| SPA /error | yes -- ugly "0" heading without context |
