---
id: BUG-021
title: Stock Login.cshtml does not render TempData[SuccessMessage]; post-reset banner missing
severity: low
status: open
found: 2026-05-15 during forgot-password end-to-end testing
flow: forgot-password / reset-password
component: stock Volo.Abp.Account.Public.Web Login.cshtml (compiled into the RCL; no local override)
---

# BUG-021 — Stock Login Razor page swallows `TempData["SuccessMessage"]`

## Severity
low — does not block the flow. After a successful password reset, the user is redirected to `/Account/Login` and signs in with the new password. The success message intended to confirm the reset is set in `TempData` by `ResetPasswordModel.OnPostAsync` but never rendered because stock Login does not look for it.

## Status
**Open** — for fix session.

## Symptom

1. User completes `/Account/ResetPassword` with valid token + new password.
2. `ResetPasswordModel.OnPostAsync` succeeds and runs:
   ```csharp
   TempData["SuccessMessage"] =
       "Your password has been reset. Please sign in with your new password.";
   return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
   ```
3. Browser lands on `/Account/Login`. The page renders the stock ABP Login form — username, password, remember-me, "Forgot password?", "Sign In" — but no banner. The TempData entry survives the redirect (one-redirect lifetime, per ASP.NET Core default) but the Razor view never references it.

Verified 2026-05-15 in the Playwright run of PR #201 end-to-end: snapshot of `/Account/Login` after a successful reset has zero `status` / `alert` / `success` nodes.

## Root cause

The local `Pages/Account/Login.cshtml` does not exist; ASP.NET Core resolves the page out of the compiled `Volo.Abp.Account.Public.Web` RCL. The stock view template renders the Login form but does not include a `@if (TempData["SuccessMessage"] is string msg) { ... }` block, so the entry is read by nobody and discarded after the request completes.

## Recommended fix

Mirror the pattern used by `ForgotPassword.cshtml` (PR #201): add a local Razor view override under `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Login.cshtml` (with matching `Login.cshtml.cs` if needed) that subclasses or mirrors the stock model and renders a `role="status"` banner when `TempData["SuccessMessage"]` is set. The same pattern can also render `TempData["ErrorMessage"]` for general-purpose login redirects from other flows.

Filesystem precedence overrides the RCL page automatically — no DI or registration change required. Match the `account-card` / `account-content` layout used by `ResendVerification.cshtml` / `ForgotPassword.cshtml` so visual rhythm stays consistent.

## Related

- PR #201 introduced the redirect that surfaces this gap.
- [[BUG-011]] — earlier reset-password SPA fallthrough; superseded by PR #201 design (AuthServer Razor owns the reset surface).
