---
status: draft
issue: forgot-password-feature
owner: AdrianG
created: 2026-05-14
approach: code (no tests; live verification deferred to the slow-loop test session)
---

# Forgot / reset password feature -- complete implementation

## Goal

Make the forgot-password flow work end-to-end on the AuthServer Razor
UI (`http://*.localhost:44368`). Today the Razor pages render but
silently fail to send the reset email because the stock ABP Pro
`IAccountAppService.SendPasswordResetCodeAsync` path is broken by a
`Scriban.Parsing.ParserOptions` `TypeLoadException` (NuGet version
mismatch). The Razor PageModel catches the exception, redirects to
`/Account/PasswordResetLinkSent`, and shows the user a fake-success
message -- no email actually arrives.

The custom `IExternalAccountAppService.SendPasswordResetCodeAsync` +
`ResetPasswordAsync` endpoints work correctly today (verified via
direct curl). Adrian's testing fallback yesterday was to call the
custom reset endpoint directly via API, bypassing the broken UI path.

## Why

Adrian's design choice: **AuthServer handles all authentication
UI**. The SPA's `/account/*` routes are dead -- the OAuth-challenge
guard kicks in before the route resolves, redirecting anonymous
users back to AuthServer login. The forgot-password journey must
live on the AuthServer Razor pages.

This PR wires the existing custom service into the existing Razor
pages so the flow works end-to-end.

## Locked decisions (2026-05-14)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Subclass ABP's PageModels** -- override `OnPostAsync` (Forgot) and `OnGetAsync`/`OnPostAsync` (Reset) to call `IExternalAccountAppService` instead of stock | Less HTML to maintain; inherits ABP's layout / antiforgery / localization wiring. Pattern matches existing `Pages/Account/ResendVerification.cshtml.cs` (which is fully custom, not a subclass, but same plumbing). |
| 2 | **Redirect to `/Account/Login` with success TempData** after reset | Matches ABP Pro stock UX. Login page shows green success banner. No new page needed. |
| 3 | **Post-reset confirmation email**: no code change needed | `ExternalAccountAppService.ResetPasswordAsync` already routes the post-reset confirmation through `INotificationDispatcher.DispatchAsync` with template code `PasswordChange` (line 178-188). The stock `_accountEmailer` path is NOT used. The template HTML file `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/PasswordChange.html` exists and is seeded. |
| 4 | **Invalid-link UX**: `ResetPassword` GET catches `EntityNotFoundException`, redirects to `/Account/ForgotPassword` with TempData error | User can immediately request a fresh link. No new page. |

## What's actually broken today (test evidence captured 2026-05-14)

### Routes / endpoints tested

| Surface | Result | Notes |
|---|---|---|
| `GET /Account/ForgotPassword` | 200 | Form renders correctly with email field + antiforgery |
| `POST /Account/ForgotPassword` (Razor) | 302 -> `/Account/PasswordResetLinkSent` | Calls broken stock service; exception swallowed; user sees fake success |
| `GET /Account/ResetPassword?userId=X&resetToken=Y` (valid IDs) | 200 | Form renders |
| `GET /Account/ResetPassword?userId=00000...&resetToken=test` (invalid) | 500 | Raw `EntityNotFoundException` leaks to browser |
| `GET /Account/PasswordResetLinkSent` | 200 | Generic "check your inbox" page |
| `POST /api/account/send-password-reset-code` (ABP stock) | 500 | `TypeLoadException: Scriban.Parsing.ParserOptions` (see BUG-018 below) |
| `POST /api/public/external-account/send-password-reset-code` (custom) | 204 | Always success (OWASP-correct, no enumeration). Composes email via `INotificationDispatcher` + template `ResetPassword`. Queues `SendAppointmentEmailJob` Hangfire async. |
| `POST /api/account/reset-password` (ABP stock) | 404 with leak | `EntityNotFoundException` body leaks user-existence (BUG-001 pattern) |
| `POST /api/public/external-account/reset-password` (custom) | 403 `CaseEvaluation:Account.ResetPasswordTokenInvalid` | Proper error code, no info leak |
| `GET /account/forgot-password` (SPA) | OAuth-redirected to login | Unreachable for anonymous users |
| `GET /account/reset-password` (SPA) | Same OAuth redirect | Unreachable |

### Out-of-scope bugs surfaced (not fixed in this PR; separate followups)

- **BUG-018 Scriban TypeLoadException**: `Volo.Abp.TextTemplating.Scriban 10.0.2` fails to load `Scriban.Parsing.ParserOptions`. Affects every stock ABP path that uses `IEmailTemplateRenderer` (the stock register-confirmation email, the stock password-reset email, etc.). This PR sidesteps the issue by routing through our custom dispatcher instead of stock. **Fix separately**: NuGet version pin / upgrade.
- **BUG-014**: hardcoded `authServerBaseUrl` + `portalBaseUrl` in `CaseEvaluationSettingDefinitionProvider.cs:43,53`. Email link always emits `:44368`. Blocks alt-port testing + Phase-2 multi-tenant. **Fix separately**: read from `IConfiguration` + env-var-drive in `docker-compose.yml`.
- **BUG-001 family**: stock ABP `/api/account/*` endpoints leak user-existence in error messages. The custom endpoints already follow OWASP-correct generic-success pattern; this PR makes the Razor UI funnel through custom endpoints, so the leak is sidestepped for the Razor journey. **Fix separately for any remaining direct API callers**.
- **SMTP-1**: `Abp.Mailing.Smtp.Password` is corrupted in DB; decryption fails; deliveries currently rejected by Exchange Online with `4.5.127 Excessive message rate`. Independent of forgot-password design. **Fix separately**: reseed SMTP setting via DbMigrator.

## Implementation steps

### Step 1 -- Custom `Pages/Account/ForgotPassword.cshtml.cs`

Standalone `AbpPageModel` (NOT subclassing stock `ForgotPasswordModel`).
Mirrors `ResendVerification.cshtml.cs` shape:

```csharp
namespace HealthcareSupport.CaseEvaluation.Pages.Account;

public class ForgotPasswordModel : AbpPageModel
{
    [BindProperty]
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool RequestSubmitted { get; set; }

    private readonly IExternalAccountAppService _externalAccountAppService;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        IExternalAccountAppService externalAccountAppService,
        ILogger<ForgotPasswordModel> logger)
    {
        _externalAccountAppService = externalAccountAppService;
        _logger = logger;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Email))
        {
            return Page();
        }

        try
        {
            await _externalAccountAppService.SendPasswordResetCodeAsync(
                new SendPasswordResetCodeInput
                {
                    Email = Email!,
                    AppName = "Angular",
                    ReturnUrl = ReturnUrl,
                });
        }
        catch (Exception ex)
        {
            // Log + swallow. Generic-success UX regardless of outcome
            // so the page doesn't leak which emails are registered.
            _logger.LogWarning(ex,
                "ForgotPasswordModel.OnPostAsync: SendPasswordResetCodeAsync threw for email-key {EmailKey}; surfacing generic success.",
                Email);
        }

        RequestSubmitted = true;
        return Page();
    }
}
```

File overrides ABP Pro's RCL stock `ForgotPasswordModel` because
ASP.NET Core Razor Pages resolves filesystem before RCL. Same
precedence trick `ResendVerification` uses.

### Step 2 -- Custom `Pages/Account/ForgotPassword.cshtml`

Razor view that renders the form + the post-submit success block.
Layout mirrors `ResendVerification.cshtml` (same `account-card` /
`account-content` wrapper, same accessibility attrs, same submit
button styling). On `Model.RequestSubmitted == true` shows a
`role="status" aria-live="polite"` success banner reading
"If the email you entered matches a registered account, a reset
link is on its way."

### Step 3 -- Custom `Pages/Account/ResetPassword.cshtml.cs`

```csharp
public class ResetPasswordModel : AbpPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ResetToken { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    [Required, StringLength(128, MinimumLength = 6)]
    public string? Password { get; set; }

    [BindProperty]
    [Required, Compare(nameof(Password))]
    public string? ConfirmPassword { get; set; }

    public bool ResetSucceeded { get; set; }
    public string? ErrorMessage { get; set; }

    private readonly IExternalAccountAppService _externalAccountAppService;
    private readonly ILogger<ResetPasswordModel> _logger;

    public ResetPasswordModel(
        IExternalAccountAppService externalAccountAppService,
        ILogger<ResetPasswordModel> logger)
    {
        _externalAccountAppService = externalAccountAppService;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        if (UserId == Guid.Empty || string.IsNullOrWhiteSpace(ResetToken))
        {
            return RedirectToForgotWithError(
                "Your reset link is missing required information. " +
                "Please request a new password reset email.");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _externalAccountAppService.ResetPasswordAsync(
                new ResetPasswordInput
                {
                    UserId = UserId,
                    ResetToken = ResetToken,
                    Password = Password!,
                    ConfirmPassword = ConfirmPassword!,
                });
        }
        catch (BusinessException ex)
            when (ex.Code == CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid)
        {
            return RedirectToForgotWithError(
                "Your reset link is invalid or has expired. Please request a new one.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ResetPasswordModel.OnPostAsync: ResetPasswordAsync threw for user {UserId}.",
                UserId);
            ErrorMessage =
                "We could not reset your password. Please try again or request a new reset link.";
            return Page();
        }

        // Success: redirect to Login with a TempData success banner
        // that the Login page reads + renders. Matches ABP Pro stock UX.
        TempData["SuccessMessage"] =
            "Your password has been reset. Please sign in with your new password.";
        return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
    }

    private IActionResult RedirectToForgotWithError(string message)
    {
        TempData["ErrorMessage"] = message;
        return RedirectToPage("./ForgotPassword");
    }
}
```

### Step 4 -- Custom `Pages/Account/ResetPassword.cshtml`

Razor view with two password fields (Password + Confirm) + the
hidden userId / resetToken / returnUrl. Validation-summary div shows
`ErrorMessage` when set. Same `account-card` / `account-content`
wrapper.

### Step 5 -- Login page TempData success banner

`Pages/Account/Login.cshtml` is currently NOT overridden -- it's
served by ABP Pro's stock RCL. Stock pages already read
`TempData["SuccessMessage"]` in some ABP versions; if not, the
TempData entry survives the redirect but is invisible.

**Sub-decision**: if the stock Login page does NOT render
`TempData["SuccessMessage"]`, add a `Pages/Account/Login.cshtml` +
`Login.cshtml.cs` override that subclasses ABP's stock model and
adds the banner rendering. Investigate during implementation. If
override is needed, follow the same pattern as the
ForgotPassword / ResetPassword files.

### Step 6 -- DI / module wiring

No new services to register. `IExternalAccountAppService` is
already exposed via the Application module which is already in
the AuthServer `DependsOn` list (line 83 of
`CaseEvaluationAuthServerModule.cs`). The two PageModels
constructor-inject it.

## Files affected

| File | Action | Lines (approx) |
|---|---|---|
| `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/ForgotPassword.cshtml` | NEW | ~70 |
| `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/ForgotPassword.cshtml.cs` | NEW | ~70 |
| `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/ResetPassword.cshtml` | NEW | ~90 |
| `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/ResetPassword.cshtml.cs` | NEW | ~100 |
| `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Login.cshtml` (conditional) | NEW IF NEEDED | ~50 |
| `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Login.cshtml.cs` (conditional) | NEW IF NEEDED | ~30 |
| `docs/parity/wave-1-parity/external-user-password-reset.md` (if exists) | UPDATE | small |

**No code change required** in:
- `ExternalAccountAppService.cs` (already wired to `INotificationDispatcher`)
- `IExternalAccountAppService.cs` (contract is fine)
- Email template HTML files (already seeded)
- Backend reset / send endpoints (already work)
- `app.routes.ts` or any SPA file

## Slow-loop test plan (deferred to test session)

Run as `it.admin@hcs.test` (host) AND as `admin@falkinstein.test`
(tenant) to verify host-vs-tenant context handling.

| Step | Expected |
|---|---|
| 1. Navigate to `http://falkinstein.localhost:44368/Account/Login` | Login form renders, "Forgot password?" link present |
| 2. Click "Forgot password?" link | Lands on `/Account/ForgotPassword` (custom page now, NOT stock) -- verify by page source comment / form action |
| 3. Enter `admin@falkinstein.test`, submit | Page shows "If the email matches a registered account..." success banner. **Check authserver logs** for `_dispatcher.DispatchAsync` call (templateCode = `ResetPassword`). Check Hangfire dashboard for the queued `SendAppointmentEmailJob`. |
| 4. Check Gmail inbox for the reset email | Subject contains "Password Reset"; body contains link `http://falkinstein.localhost:44368/Account/ResetPassword?userId=X&resetToken=Y` |
| 5. Click the reset link | Lands on `/Account/ResetPassword`, form renders with two password fields |
| 6. Enter a new password + confirm, submit | Redirected to `/Account/Login`. If Step 5 override added: green banner "Your password has been reset. Please sign in." |
| 7. Sign in with the new password | Login succeeds, lands on the user's home page |
| 8. Check Gmail inbox for the post-reset confirmation | Subject "Your password has been successfully changed - <tenant>"; body matches `PasswordChange.html` template |
| 9. Edge case: hit `/Account/ResetPassword?userId=00000...&resetToken=test` | Redirected to `/Account/ForgotPassword` with a red error banner "Your reset link is invalid or has expired" -- NO 500 |
| 10. Edge case: submit reset form with `password != confirmPassword` | Stays on page, shows validation error from `Compare` attribute |
| 11. Edge case: enter unregistered email on ForgotPassword | Same generic success banner as Step 3 -- no enumeration leak |
| 12. Edge case: rate-limit -- submit ForgotPassword 6 times rapidly for the same email | First 5 succeed silently; 6th still shows generic success but logs `Rate limited (5/hour/email)`. No UX hint to attacker. |

## Risks

- **`Pages/Account/Login.cshtml` override may be needed for the success banner** (Step 5 sub-decision). If the stock Login page doesn't read `TempData["SuccessMessage"]`, the user's UX after a successful reset is "redirected to Login with no feedback that the reset worked." Acceptable as a degraded experience; the user can just try logging in with their new password. Investigate during implementation, add the override only if missing.

- **Custom service exception classes**: `IExternalAccountAppService.SendPasswordResetCodeAsync` is documented as always returning success generic-style. Confirm by reading the method body that it really doesn't throw on missing user, throttling, etc. If it can throw, the try/catch in `ForgotPasswordModel.OnPostAsync` handles it -- verify the catch is correctly broad.

- **TempData session persistence**: TempData survives one redirect by default. Confirm that the AuthServer cookie auth + TempData are configured so that a redirect from `/Account/ResetPassword` to `/Account/Login` carries the success message through. Most ABP defaults do this; verify during testing.

- **Concurrent password reset requests for the same user**: ABP Identity tokens are not single-use across the token TTL. If a user requests two reset emails and uses the older link, both work until expiry. Document but no fix needed -- matches ABP default behavior.

## Out of scope

- BUG-018 Scriban version conflict (separate ticket)
- BUG-014 hardcoded URL settings (separate ticket -- needed for alt-port testing but not for canonical-port forgot-password)
- SMTP-1 SMTP password decryption (separate ticket, infra-level)
- SPA password-reset UI (SPA `/account/*` routes intentionally dead per Adrian's design)
- Stock `/api/account/*` endpoint hardening to remove user-enumeration leaks (separate ticket; this PR sidesteps by funneling Razor UI through custom endpoints)
- Removing the now-unused stock-account-public dependency (keeps stock Login + Register + ConfirmUser + PasswordResetLinkSent pages working)

## Acceptance (when slow-loop test session signs off)

- `/Account/ForgotPassword` POST sends a real email through `INotificationDispatcher` (verified via Hangfire dashboard + Gmail inbox)
- Reset email link lands on `/Account/ResetPassword`; form submission resets the password successfully
- Invalid / expired `userId` on `/Account/ResetPassword` GET redirects to `/Account/ForgotPassword` with a friendly error message (NO 500)
- Post-reset confirmation email arrives in Gmail inbox via the `PasswordChange` template
- No user-enumeration leak in any error path

## Rollback

Revert the PR. The custom pages are filesystem-resolved; deleting
them returns control to ABP Pro's RCL stock pages (which silently
fail today but at least don't 500). API endpoints are untouched.
