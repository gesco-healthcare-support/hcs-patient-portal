using System;
using System.IO;
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
    /// Builds the Angular SPA URL for the current tenant subdomain.
    /// The host (e.g. <c>falkinstein.localhost</c>) is preserved from
    /// the incoming request; only the port is swapped for the SPA's
    /// (4200 by default, or whatever port <c>App:AngularUrl</c>
    /// configures). Falls back to the configured value when the
    /// request host is unavailable (rare; non-HTTP test contexts).
    /// </summary>
    private string ResolveAngularUrl()
    {
        var configured = _configuration["App:AngularUrl"];
        var requestHost = Request.Host.Host;
        if (string.IsNullOrWhiteSpace(requestHost))
        {
            return string.IsNullOrWhiteSpace(configured) ? "/" : configured;
        }

        var angularPort = "4200";
        if (!string.IsNullOrWhiteSpace(configured)
            && Uri.TryCreate(configured, UriKind.Absolute, out var configuredUri))
        {
            angularPort = configuredUri.IsDefaultPort ? string.Empty : configuredUri.Port.ToString();
        }

        var portSegment = string.IsNullOrEmpty(angularPort) ? string.Empty : ":" + angularPort;
        var scheme = Request.Scheme;
        return $"{scheme}://{requestHost}{portSegment}/";
    }
}
