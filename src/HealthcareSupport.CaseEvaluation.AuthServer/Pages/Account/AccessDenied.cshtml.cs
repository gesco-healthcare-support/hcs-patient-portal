using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Standalone custom Razor page hosted on the AuthServer at
/// <c>/Account/AccessDenied</c>. Filesystem precedence overrides the
/// stock <c>AbpAccountPublicWeb</c> RCL page. No logic beyond rendering
/// the page; the page itself is informational.
/// </summary>
public class AccessDeniedModel : AbpPageModel
{
    public IActionResult OnGet() => Page();
}
