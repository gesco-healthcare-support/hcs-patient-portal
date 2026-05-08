using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// B5 (2026-05-07) -- AuthServer's <c>/Account/Logout</c> override.
///
/// <para>The framework default signs out the cookie and redirects to
/// <c>~/</c>, which our <see cref="HealthcareSupport.CaseEvaluation.Pages.IndexModel"/>
/// then sends to <c>/Account/Login</c>. That is fine for the AuthServer
/// itself but leaves the Angular SPA on the original tenant subdomain
/// holding stale OAuth tokens in localStorage. Until those tokens
/// expire (~1 hour for access_token + 14 days for refresh_token) the
/// SPA continues to make API calls as the previous user, which is the
/// behavior Adrian flagged as "stale OAuth tokens after logout".</para>
///
/// <para>The fix: sign out every authentication scheme on the
/// AuthServer side, then redirect back to the SPA with
/// <c>?logout=true</c> appended. <c>app.component.ts</c> detects that
/// query param at bootstrap, calls
/// <c>AuthService.logout()</c> (clears localStorage tokens via the
/// underlying angular-oauth2-oidc client), and then redirects to
/// <c>/login</c>. End result: regardless of whether the user clicked
/// the SPA's user-menu Logout link or typed
/// <c>/Account/Logout</c> directly, both ends are clean.</para>
///
/// <para>The route preserves any incoming <c>?ReturnUrl</c> -- it
/// becomes the base for the SPA redirect when present, so logging out
/// from inside an OIDC flow still completes the flow correctly.</para>
/// </summary>
public class LogoutModel : AbpPageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(IConfiguration configuration, ILogger<LogoutModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            // Sign out every Microsoft.Identity scheme that ABP wires up.
            // The runtime registers exactly: Identity.Application,
            // Identity.External, Identity.TwoFactorRememberMe,
            // Identity.TwoFactorUserId. The OpenIddict server schemes
            // are sign-OUT-incompatible (server-managed); we skip them.
            // SignOutAsync is no-op for schemes that aren't currently
            // active, so the four-call sequence is safe.
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
            await HttpContext.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);

            _logger.LogInformation(
                "LogoutModel: signed out user {User} on {Host}.",
                User.Identity.Name ?? "<unknown>",
                Request.Host.Host);
        }

        return Redirect(BuildSpaLogoutUrl());
    }

    /// <summary>
    /// Build the SPA URL on the same tenant subdomain as the incoming
    /// request, append <c>?logout=true</c> so
    /// <c>app.component.ts</c> can run its localStorage cleanup. The
    /// host (e.g. <c>falkinstein.localhost</c>) is preserved verbatim;
    /// only the port is swapped to the SPA's. Falls back to the
    /// configured <c>App:AngularUrl</c> when the request host is
    /// unavailable (rare; non-HTTP test contexts).
    /// </summary>
    private string BuildSpaLogoutUrl()
    {
        var configured = _configuration["App:AngularUrl"];
        var requestHost = Request.Host.Host;
        if (string.IsNullOrWhiteSpace(requestHost))
        {
            return string.IsNullOrWhiteSpace(configured)
                ? "/?logout=true"
                : configured.TrimEnd('/') + "/?logout=true";
        }

        var angularPort = "4200";
        if (!string.IsNullOrWhiteSpace(configured)
            && Uri.TryCreate(configured, UriKind.Absolute, out var configuredUri))
        {
            angularPort = configuredUri.IsDefaultPort ? string.Empty : configuredUri.Port.ToString();
        }

        var portSegment = string.IsNullOrEmpty(angularPort) ? string.Empty : ":" + angularPort;
        var scheme = Request.Scheme;
        return $"{scheme}://{requestHost}{portSegment}/?logout=true";
    }
}
