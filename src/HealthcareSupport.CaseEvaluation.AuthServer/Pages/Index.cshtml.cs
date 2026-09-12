using HealthcareSupport.CaseEvaluation.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages;

/// <summary>
/// 2026-05-06 -- the AuthServer's root page is never rendered.
///
/// <para>Two real entry paths land here:</para>
/// <list type="number">
///   <item>An anonymous visitor types the AuthServer URL directly, OR
///         ABP's stock /Account/Logout redirects them after clearing
///         their session. We send them to <c>/Account/Login</c> so they
///         can sign in. This avoids the default ABP "applications"
///         landing page that surfaces internal Swagger client URLs to
///         anonymous users.</item>
///   <item>A user who just signed in via <c>/Account/Login</c> with no
///         <c>ReturnUrl</c> query param. ABP's LoginModel redirects to
///         <c>~/</c> by default in that case. Routing them back to
///         <c>/Account/Login</c> would loop. We send them to the
///         Angular SPA on the same subdomain instead. The SPA's OIDC
///         client kicks off <c>/connect/authorize</c>, the AuthServer
///         sees the existing auth cookie and immediately issues a
///         code, the SPA exchanges it for a token, and the user lands
///         on the dashboard or home view per their role
///         (post-login-redirect-guard in <c>app.routes.ts</c>).</item>
/// </list>
///
/// <para>We do NOT render an HTML landing page in either case --
/// that page exposed OpenIddict client metadata (Swagger URLs etc.)
/// to anyone hitting the root, which is information disclosure no
/// production tenant wants.</para>
/// </summary>
public class IndexModel : AbpPageModel
{
    private readonly IConfiguration _configuration;

    public IndexModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult OnGet()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(ResolveAngularUrl());
        }
        return LocalRedirect("~/Account/Login");
    }

    /// <summary>
    /// Builds the Angular SPA root URL for the current office subdomain. The office slug is the
    /// leftmost label of the request host ("falkinstein.auth.&lt;base&gt;" -> "falkinstein",
    /// "admin.auth.&lt;base&gt;" -> "admin"); <see cref="TenantUrlComposer.ComposeForRequestHost"/>
    /// prepends it to the office-less <c>App:AngularUrl</c>, matching the email URL builder and
    /// angular/src/tenant-bootstrap.ts. This must NOT reuse the request host as the SPA host: on the
    /// production subdomain layout the SPA ({office}.&lt;base&gt;) and AuthServer
    /// ({office}.auth.&lt;base&gt;) are different hosts, so reusing the request host redirected the
    /// AuthServer root to itself -> ERR_TOO_MANY_REDIRECTS.
    /// </summary>
    private string ResolveAngularUrl()
    {
        var configured = _configuration["App:AngularUrl"];
        var spaBase = TenantUrlComposer.ComposeForRequestHost(configured, Request.Host.Host);
        return string.IsNullOrWhiteSpace(spaBase) ? "/" : spaBase!.TrimEnd('/') + "/";
    }
}
