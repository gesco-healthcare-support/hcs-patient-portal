using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
/// <para>Landing page for the link in the verification email sent at self sign-up. GET checks the
/// link WITHOUT using it and shows a "Confirm my email" button; the POST behind that button calls
/// <see cref="IdentityUserManager.ConfirmEmailAsync"/> and redirects to
/// <c>/Account/Login?flash=email-verified</c> (or <c>?flash=verification-invalid</c>).</para>
///
/// <para><b>Why the button (2026-10-23).</b> This page used to confirm inside its GET handler. Mail
/// scanners (Microsoft 365 Safe Links, corporate proxies) open every link before the person does,
/// and Razor Pages answers a HEAD with the GET handler -- so the scanner, not the person, confirmed
/// the email. Measured: on 2026-10-23 a Safe Links HEAD confirmed an account 2 seconds before the
/// real click. The earlier note here, that HEAD "falls through" to a no-op, was wrong. A confirmed
/// email is what grants a party access to appointments, so it must take a person's action, and only
/// a POST carries one.</para>
///
/// <para>Multi-tenant return-URL bug (ABP support #7077): the framework's post-confirm Login button
/// dropped returnUrl. We build the redirect URL ourselves so the original ReturnUrl + ReturnUrlHash
/// round-trip through the flash.</para>
///
/// <para>Tenant context flows from the URL subdomain via
/// <c>HostAwareDomainTenantResolveContributor</c>. Confirmation tokens remain validatable across
/// processes because DataProtection keys are shared via Redis.</para>
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

    /// <summary>
    /// Looks only. A link that cannot be used goes straight to the sign-in page with the invalid
    /// flash; an already-confirmed account goes there as success; a usable link shows the button.
    /// Nothing here changes the account, so a scanner's HEAD or GET is harmless.
    /// </summary>
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
            return RedirectToLogin("verification-invalid");
        }

        if (user.EmailConfirmed)
        {
            return RedirectToLogin("email-verified");
        }

        var usable = await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.EmailConfirmationTokenProvider,
            UserManager<Volo.Abp.Identity.IdentityUser>.ConfirmEmailTokenPurpose,
            ConfirmationToken);
        if (!usable)
        {
            _logger.LogInformation(
                "EmailConfirmationModel: the link for user {UserId} is not usable; redirecting with generic flash.",
                UserId);
            return RedirectToLogin("verification-invalid");
        }

        return Page();
    }

    /// <summary>The person pressed "Confirm my email". This is the only place the email is confirmed.</summary>
    public async Task<IActionResult> OnPostAsync()
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
            // Idempotent: a double click lands here on the second attempt. Treat as success.
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
                DescribeErrors(result));
            return RedirectToLogin("verification-invalid");
        }

        _logger.LogInformation(
            "EmailConfirmationModel: user {UserId} email confirmed; redirecting to login.",
            UserId);
        return RedirectToLogin("email-verified");
    }

    /// <summary>
    /// The error CODES of a failed identity result, comma-separated. <see cref="IdentityError"/>
    /// has no ToString override, so joining the errors themselves logs the type name once per
    /// error and never the reason. Codes carry no personal data.
    /// </summary>
    public static string DescribeErrors(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(e => e.Code));

    private LocalRedirectResult RedirectToLogin(string flash)
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
