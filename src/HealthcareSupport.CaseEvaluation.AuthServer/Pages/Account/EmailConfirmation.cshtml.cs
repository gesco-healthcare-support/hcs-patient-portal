using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Custom Razor PageModel hosted on the AuthServer at
/// <c>/Account/EmailConfirmation</c>. Filesystem precedence overrides the
/// stock <c>Volo.Abp.Account.Pro.Public.Web</c> RCL page.
///
/// <para>Landing page for the link in the verification email. Parses
/// <c>userId</c> + <c>confirmationToken</c> from the query string, calls
/// <see cref="IdentityUserManager.ConfirmEmailAsync"/>, and redirects to
/// <c>/Account/Login?flash=email-verified</c> on success (or
/// <c>?flash=verification-invalid</c> on failure). The Login page reads
/// the flash and renders a banner (Phase 9 override -- separate plan).</para>
///
/// <para>Defensive against two ABP-framework bugs documented in 2026-05
/// support tickets:
/// <list type="bullet">
///   <item><b>HEAD-request bug</b> -- email-scanner clients (Outlook Safe
///         Links, corporate proxies) hitting HEAD before the user's GET
///         prematurely fired ConfirmEmailAsync in framework stock. We
///         expose OnGetAsync only; other HTTP methods fall through to
///         the (no-op) view and do nothing.</item>
///   <item><b>Multi-tenant return-URL bug</b> (ABP support #7077) --
///         framework's post-confirm Login button dropped returnUrl. We
///         build the redirect URL ourselves so the original ReturnUrl +
///         ReturnUrlHash round-trip through the flash.</item>
/// </list></para>
///
/// <para>Tenant context flows from the URL subdomain via
/// <c>HostAwareDomainTenantResolveContributor</c>. Confirmation tokens
/// remain validatable across processes because DataProtection keys are
/// shared via Redis (CaseEvaluationAuthServerModule:267-283).</para>
///
/// <para>Plan: docs/plans/2026-05-18-fix-verification-email-url.md</para>
/// </summary>
[AllowAnonymous]
public class EmailConfirmationModel : AbpPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ConfirmationToken { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrlHash { get; set; }

    private readonly IdentityUserManager _userManager;
    private readonly ILogger<EmailConfirmationModel> _logger;

    public EmailConfirmationModel(
        IdentityUserManager userManager,
        ILogger<EmailConfirmationModel> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (UserId == Guid.Empty || string.IsNullOrWhiteSpace(ConfirmationToken))
        {
            _logger.LogInformation(
                "EmailConfirmationModel: missing userId or token; redirecting to login.");
            return RedirectToLogin("verification-invalid");
        }

        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
        {
            // Generic redirect -- do not leak whether the userId was real.
            _logger.LogInformation(
                "EmailConfirmationModel: user {UserId} not found; redirecting with generic flash.",
                UserId);
            return RedirectToLogin("verification-invalid");
        }

        if (user.EmailConfirmed)
        {
            // Idempotent: a link clicked twice (or scanned by an email
            // safety service that already hit GET earlier) lands here on
            // the second attempt. Treat as success.
            _logger.LogInformation(
                "EmailConfirmationModel: user {UserId} already confirmed; redirecting as success.",
                UserId);
            return RedirectToLogin("email-verified");
        }

        var result = await _userManager.ConfirmEmailAsync(user, ConfirmationToken);
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "EmailConfirmationModel: ConfirmEmailAsync failed for user {UserId} with errors: {Errors}.",
                UserId,
                string.Join(", ", result.Errors));
            return RedirectToLogin("verification-invalid");
        }

        _logger.LogInformation(
            "EmailConfirmationModel: user {UserId} email confirmed; redirecting to login.",
            UserId);
        return RedirectToLogin("email-verified");
    }

    private IActionResult RedirectToLogin(string flash)
    {
        var url = $"~/Account/Login?flash={flash}";
        if (!string.IsNullOrWhiteSpace(ReturnUrl))
        {
            url += $"&ReturnUrl={Uri.EscapeDataString(ReturnUrl)}";
        }
        if (!string.IsNullOrWhiteSpace(ReturnUrlHash))
        {
            url += $"&ReturnUrlHash={Uri.EscapeDataString(ReturnUrlHash)}";
        }
        return LocalRedirect(url);
    }
}
