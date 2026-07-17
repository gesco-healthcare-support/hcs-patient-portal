using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// AuthServer's <c>/Account/Logout</c> override, for a direct hit on this URL
/// (a bookmark or a typed address). The SPA's user-menu Logout uses the
/// standard OIDC end-session flow (<c>OAuthService.revokeTokenAndLogout()</c>
/// -> <c>/connect/endsession</c>), which clears the SSO cookie and returns to
/// <see cref="HealthcareSupport.CaseEvaluation.Pages.Account.LoggedOutModel"/>
/// -> <c>/Account/Login</c>, so this page is no longer part of the SPA logout
/// path.
///
/// <para>2026-07-17 -- removed the old <c>?logout=true</c> handshake and the
/// hand-built SPA redirect (<c>BuildSpaLogoutUrl</c>). That URL was computed by
/// reusing the AuthServer's own request host + a port swap, which is only valid
/// in dev; on the production subdomain layout ({office}.&lt;base&gt; SPA vs
/// {office}.auth.&lt;base&gt; AuthServer) it resolved to the AuthServer host
/// itself and produced ERR_TOO_MANY_REDIRECTS. We now sign out every scheme,
/// expire the non-auth cookies, and redirect to <c>/Account/Login</c> with a
/// relative link (no host guessing).</para>
/// </summary>
public class LogoutModel : AbpPageModel
{
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(ILogger<LogoutModel> logger)
    {
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

        // Issue #106a (2026-05-13) -- SignOutAsync above expires the
        // HttpOnly auth cookies (.AspNetCore.Identity.Application etc.)
        // but leaves the non-auth ABP cookies in place. A stale __tenant
        // cookie can leak the prior user's tenant into a brand-new
        // registration in the same browser, so explicitly expire it.
        // XSRF-TOKEN is anti-forgery; rotating it on logout prevents
        // an attacker who scraped the prior token from replaying it
        // against the next session in the same browser. Same Path="/"
        // ABP uses when setting them so the Expires/Max-Age=0 cookie
        // matches and the browser drops it.
        Response.Cookies.Delete("__tenant", new Microsoft.AspNetCore.Http.CookieOptions { Path = "/" });
        Response.Cookies.Delete("XSRF-TOKEN", new Microsoft.AspNetCore.Http.CookieOptions { Path = "/" });

        return RedirectToPage("./Login");
    }
}
