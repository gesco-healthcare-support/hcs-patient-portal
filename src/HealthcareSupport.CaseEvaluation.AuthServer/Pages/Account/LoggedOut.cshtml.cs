using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Standalone custom Razor page hosted on the AuthServer at
/// <c>/Account/LoggedOut</c>. Filesystem precedence overrides the stock
/// <c>AbpAccountPublicWeb</c> RCL page. The stock model performs an
/// OpenIddict post-logout redirect-URI lookup so the user can be sent
/// back to the originating client; we deliberately do NOT replicate
/// that because the AuthServer owns the authentication UI surface
/// end-to-end (see memory: project_authserver-ui-not-spa) and the user
/// must always land on <c>/Account/Login</c> after sign-out for OLD
/// parity. The standard OIDC end-session flow (SPA -> /connect/endsession)
/// clears the SSO cookie and, with no post_logout_redirect_uri, returns
/// here -- so we redirect straight to the login page.
/// </summary>
public class LoggedOutModel : AbpPageModel
{
    public IActionResult OnGet() => RedirectToPage("./Login");
}
