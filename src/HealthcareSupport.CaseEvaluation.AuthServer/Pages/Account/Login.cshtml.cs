using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Owl.reCAPTCHA;
using Volo.Abp.Account.ExternalProviders;
using Volo.Abp.Account.Public.Web;
using Volo.Abp.Account.Security.Recaptcha;
using Volo.Abp.Account.Web.Pages.Account;
using Volo.Abp.AspNetCore.Mvc.UI.Alerts;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Custom Login PageModel for <c>/Account/Login</c>. Filesystem
/// precedence overrides the stock OpenIddict-supported Login page
/// shipped by <c>Volo.Abp.Account.Pro.Public.Web.OpenIddict</c>.
///
/// <para>Anti-enumeration UX (proposed-copy.md 2.1, locked 2026-05-18):
/// all three credential-failure modes -- wrong password, non-existent
/// email, unverified email -- collapse into one generic banner. The Pro
/// framework's "unverified email" branch redirects to
/// <c>/Account/ConfirmUser</c>; we intercept that redirect (returning
/// the Login page with the generic banner) so failure outcomes are
/// indistinguishable from the user's point of view.</para>
///
/// <para>Lockout, 2FA, and successful login flow through
/// <c>base.OnPostAsync</c> unchanged: lockout still redirects to
/// <c>/Account/LockedOut</c> (per the Pro module), 2FA still goes to
/// the security-code page, and success runs the OpenIddict authorize
/// callback via the inherited <c>RedirectSafelyAsync</c>.</para>
///
/// <para>Flash banners on GET: query string <c>?flash=password-updated</c>,
/// <c>?flash=email-verified</c>, or <c>?flash=verification-invalid</c>
/// renders a one-shot banner above the form. Source contracts:
/// <c>EmailConfirmationModel</c> emits the email-verified +
/// verification-invalid values; <c>ResetPasswordModel</c> emits
/// password-updated.</para>
/// </summary>
[ExposeServices(IncludeSelf = true)]
public class LoginModel : OpenIddictSupportedLoginModel
{
    public string? FlashBannerLevel { get; set; }
    public string? FlashBannerText { get; set; }
    public bool ShowGenericFailureBanner { get; set; }

    /// <summary>
    /// True only when the failed login was caused by an unverified email
    /// (Pro's LoginModel would have redirected to /Account/ConfirmUser
    /// for this state; we intercept and surface a generic banner with
    /// the Resend link visible). Adrian (2026-05-19) explicitly accepted
    /// the enumeration trade-off: the Resend link's presence side-channels
    /// "this email exists AND is unverified" to anyone probing the form.
    /// Wrong-password and non-existent-email cases keep
    /// <c>ShowGenericFailureBanner = true</c> but leave this flag false,
    /// so the Resend link stays hidden for those.
    /// </summary>
    public bool IsUnverifiedEmailFailure { get; set; }

    /// <summary>
    /// Pre-fill value for the conditional "Resend verification" link
    /// below the form. Sourced from the form-submitted
    /// <c>LoginInput.UserNameOrEmailAddress</c>.
    /// </summary>
    public string? UnverifiedEmailHint { get; set; }

    public LoginModel(
        IAuthenticationSchemeProvider schemeProvider,
        IOptions<AbpAccountOptions> accountOptions,
        IAbpRecaptchaValidatorFactory recaptchaValidatorFactory,
        IAccountExternalProviderAppService accountExternalProviderAppService,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IOptions<IdentityOptions> identityOptions,
        IOptionsSnapshot<reCAPTCHAOptions> reCaptchaOptions,
        AbpOpenIddictRequestHelper openIddictRequestHelper)
        : base(
            schemeProvider,
            accountOptions,
            recaptchaValidatorFactory,
            accountExternalProviderAppService,
            currentPrincipalAccessor,
            identityOptions,
            reCaptchaOptions,
            openIddictRequestHelper)
    {
    }

    public override async System.Threading.Tasks.Task<IActionResult> OnGetAsync()
    {
        // ?flash= query string contract (cross-page banners):
        //   password-updated   -- from ResetPasswordModel on a successful reset
        //   email-verified     -- from EmailConfirmationModel on a successful confirm
        //   verification-invalid -- from EmailConfirmationModel on a bad/missing token
        var flash = Request.Query["flash"].ToString();
        switch (flash)
        {
            case "password-updated":
                FlashBannerLevel = "success";
                FlashBannerText = "Password updated. Sign in with your new password.";
                break;
            case "email-verified":
                FlashBannerLevel = "success";
                FlashBannerText = "Email verified. Sign in to continue.";
                break;
            case "verification-invalid":
                FlashBannerLevel = "warning";
                FlashBannerText = "That verification link doesn't work anymore. Resend below.";
                break;
        }

        return await base.OnGetAsync();
    }

    public override async System.Threading.Tasks.Task<IActionResult> OnPostAsync(string? action)
    {
        // OpenIddict cancel-button flow -- pass through untouched.
        if (action == "Cancel")
        {
            return await base.OnPostAsync(action!);
        }

        // Pass-through value: base.OnPostAsync's parameter is non-nullable
        // in the framework. Substitute an empty string for null so the
        // base validator does not reject the submission with
        // "The action field is required" -- our submit button does send
        // name="Action" value="Login", but defense-in-depth.
        var result = await base.OnPostAsync(action ?? string.Empty);

        // Intercept the Pro module's "unverified email" redirect to
        // /Account/ConfirmUser. The actual IActionResult subtype varies
        // (Pro DLL is obfuscated; could be RedirectToPageResult,
        // RedirectResult, or LocalRedirectResult). Match by URL/page
        // substring so we catch all three forms.
        if (IsConfirmUserRedirect(result))
        {
            Alerts.Clear();
            ShowGenericFailureBanner = true;
            IsUnverifiedEmailFailure = true;
            UnverifiedEmailHint = LoginInput?.UserNameOrEmailAddress;
            return Page();
        }

        // Normalize credential-failure Page() results (wrong password /
        // non-existent email) into the same generic banner -- but do
        // NOT set IsUnverifiedEmailFailure, so the Resend link stays
        // hidden for these cases. Detection: base.OnPostAsync surfaces
        // failures by appending Alerts entries of type Danger or
        // Warning to the AlertList and returning Page(). Successful and
        // lockout/2FA paths return IActionResults other than PageResult
        // (redirects), so this branch never fires for them.
        if (result is PageResult && System.Linq.Enumerable.Any(Alerts, IsLoginFailureAlert))
        {
            Alerts.Clear();
            ShowGenericFailureBanner = true;
            UnverifiedEmailHint = LoginInput?.UserNameOrEmailAddress;
        }

        return result;
    }

    private static bool IsLoginFailureAlert(AlertMessage alert) =>
        alert.Type == AlertType.Danger || alert.Type == AlertType.Warning;

    private static bool IsConfirmUserRedirect(IActionResult result)
    {
        switch (result)
        {
            case RedirectToPageResult rtpr:
                return !string.IsNullOrEmpty(rtpr.PageName)
                    && rtpr.PageName.Contains("ConfirmUser", System.StringComparison.OrdinalIgnoreCase);

            case RedirectResult rr:
                return !string.IsNullOrEmpty(rr.Url)
                    && rr.Url.Contains("/Account/ConfirmUser", System.StringComparison.OrdinalIgnoreCase);

            case LocalRedirectResult lrr:
                return !string.IsNullOrEmpty(lrr.Url)
                    && lrr.Url.Contains("/Account/ConfirmUser", System.StringComparison.OrdinalIgnoreCase);

            default:
                return false;
        }
    }
}
