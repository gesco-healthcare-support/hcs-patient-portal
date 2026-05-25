---
id: BUG-017
title: Stock Login.cshtml does not render TempData[SuccessMessage]; post-reset banner missing
severity: low
status: open
found: 2026-05-15 during forgot-password end-to-end testing
flow: forgot-password / reset-password
component: stock Volo.Abp.Account.Public.Web Login.cshtml (compiled into the RCL; no local override)
---

# BUG-017 — Stock Login Razor page swallows `TempData["SuccessMessage"]`

> 2026-05-23: renamed from `BUG-021-login-tempdata-success-banner.md` to free `BUG-021` for the earlier-filed datepicker observation. See OBS-33.

> **Verification 2026-05-22: FIXED (confidence 95%).**
>
> Closed by PR #206 + commit `cb8edb4` (`feat(auth-server): ship full Razor account pages...`), merged via PR #206 (2026-05-18) and PR #207 (2026-05-20) into `main`. A local override at `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Login.cshtml` (151 lines, NEW file) was added, defeating this doc's root cause ("local `Pages/Account/Login.cshtml` does not exist").
>
> Key file:line evidence:
> - `Login.cshtml:34-41` -- renders `<div class="alert ..." role="status" aria-live="polite">` when `FlashBannerLevel` / `FlashBannerText` are set.
> - `Login.cshtml.cs:92-116` (`LoginModel.OnGetAsync`) -- reads `Request.Query["flash"]`, maps `password-updated` -> success banner `"Password updated. Sign in with your new password."`, then calls `base.OnGetAsync()`.
> - `ResetPassword.cshtml.cs:130-138` -- redirects to `~/Account/Login?flash=password-updated` (with optional `&ReturnUrl=`). The `TempData["SuccessMessage"]` write described in this doc is **gone**; doc-comment lines 36-39 of `ResetPassword.cshtml.cs` cite "decision 7, locked 2026-05-18" that "TempData would not survive the OpenIddict authorize redirect chain". The query-string `?flash=` approach is functionally superior.
>
> **Residual concern (live-verify, optional):** Playwright snapshot of an actual reset flow to confirm the `?flash=password-updated` banner renders end-to-end through the OpenIddict authorize chain.
>
> **ID-collision note:** there are two BUG-021 files in this folder -- this one (`login-tempdata-success-banner`, found 2026-05-15) and `BUG-021-ce-cannot-book.md` (datepicker UX, found later). This one was filed earlier and is the "real" BUG-021. The `ce-cannot-book` file should be renumbered to the next free ID (likely BUG-036 -- sanity-check via `ls bugs/ | sort` for the highest existing ID) so cross-references in PR notes / vault don't shift.
>
> **Action: doc-close.** Update frontmatter to `status: resolved`, add a "Resolution" section citing PR #206 / commit `cb8edb4`, `Login.cshtml.cs:92-116`, and `ResetPassword.cshtml.cs:130-138`. The TempData approach was deliberately abandoned in favor of `?flash=` query banners.

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
