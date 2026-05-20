using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Standalone custom Razor page hosted on the AuthServer at
/// <c>/Account/LoggedOut</c>. Filesystem precedence overrides the stock
/// <c>AbpAccountPublicWeb</c> RCL page. The stock model performs
/// OpenIddict post-logout redirect-URI lookup so the user can be sent
/// back to the originating client; we deliberately do NOT replicate
/// that because AuthServer owns the authentication UI surface end-to-end
/// (see memory: project_authserver-ui-not-spa) and the user should
/// always land on <c>/Account/Login</c> after sign-out for OLD parity.
/// </summary>
public class LoggedOutModel : AbpPageModel
{
    public IActionResult OnGet() => Page();
}
