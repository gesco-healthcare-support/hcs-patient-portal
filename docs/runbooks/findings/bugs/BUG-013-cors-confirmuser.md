---
id: BUG-013
title: /Account/ConfirmUser Verify button blocked by CORS + missing antiforgery token
severity: high
status: open
found: 2026-05-14
flow: external-user-registration -> verify-email
component: AuthServer module CORS config + /Account/ConfirmUser.cshtml.js
---

# BUG-013 — AuthServer Verify button blocked by 2 layers

## Severity
high

## Status
**Open** — for fix session. Workaround documented below.

## Affected role
Every external user that lands on `/Account/ConfirmUser` after attempting login while unverified (Patient / AA / DA / CE).

## Steps to reproduce
1. Register an external user (don't navigate the verification email link).
2. Attempt sign in with the unverified credentials.
3. Land on `/Account/ConfirmUser` with the user's email + a "Verify" button.
4. Click "Verify".
5. Observe generic error dialog *"An error has occurred! Error detail not sent by the server."*

## Layer 1 — CORS rejects same-origin request

Server log:
```
[INF] Request starting HTTP/1.1 POST /api/account/send-email-confirmation-token
[INF] CORS policy execution failed.
[INF] Request origin http://falkinstein.localhost:44368 does not have permission to access the resource.
[INF] Executing StatusCodeResult, setting HTTP status code 400
```

**Root cause:** the AuthServer's `App__CorsOrigins` env var in `docker-compose.yml:152` is set to:
```
http://localhost:${NG_PORT:-4200},http://localhost:${API_PORT:-44327},
http://*.localhost:${NG_PORT:-4200},http://*.localhost:${API_PORT:-44327}
```
It includes the Angular + API origins (with wildcard subdomain support) but NOT the AuthServer's own port. When the AuthServer's own `/Account/ConfirmUser` Razor page POSTs an XHR to its own `/api/account/send-email-confirmation-token`, browsers send `Origin: http://falkinstein.localhost:44368` and the .NET CORS middleware rejects.

**Recommended fix:** append AuthServer self-port origins to `App__CorsOrigins`:
```yaml
App__CorsOrigins: "http://localhost:${NG_PORT:-4200},http://localhost:${API_PORT:-44327},http://localhost:${AUTH_PORT:-44368},http://*.localhost:${NG_PORT:-4200},http://*.localhost:${API_PORT:-44327},http://*.localhost:${AUTH_PORT:-44368}"
```

## Layer 2 — Endpoint rejects unauthenticated XHR without antiforgery token

After fixing Layer 1, the same POST still returned 400 empty body. Server log:
```
[INF] CORS policy execution successful.
[INF] Identity.Application was not authenticated. Failure message: Unprotect ticket failed
[INF] Executing StatusCodeResult, setting HTTP status code 400
```

The endpoint requires an authenticated session OR a valid antiforgery token. The XHR from the ConfirmUser page sends neither (only `content-type: application/json` + `x-requested-with`). The unverified-user is a half-authenticated state; the Razor page's client-side script needs to read the antiforgery cookie and emit a `RequestVerificationToken` header.

**Recommended fix:** in the Razor page model / script, attach the antiforgery token to the XHR. ABP's standard approach: include `@Html.AntiForgeryToken()` in the page + add `RequestVerificationToken: <token>` header to the XHR.

## Workaround
Navigate `/Account/ResendVerification?context=register&email=<user>&autosend=1` directly. This URL — the "Verify Email" CTA on the registration success page — works end to end (returns *"Request received."* + sends the email). Two UI paths, only one functional.

## Follow-up consideration
ABP's CORS middleware probably should not check same-origin requests at all. The Origin header gets sent for cross-port XHR even when the page and target are on the same host:port; the AuthServer's own pages should be a privileged caller. Configuring the origin explicitly is the standard workaround, but a deeper fix would be a CORS bypass for same-origin.

## Related
- [[BUG-014]] (hardcoded URLs in email templates) — same family of multi-environment URL handling issues.
- [[BUG-015]] (dynamic-env.json never read).
- [[BUG-016]] (OpenIddict subdomain wildcards).
