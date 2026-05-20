using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Standalone custom Razor page hosted on the AuthServer at
/// <c>/Account/LockedOut</c>. Filesystem precedence overrides the stock
/// <c>AbpAccountPublicWeb</c> RCL page. No logic beyond rendering the
/// page; the page itself is informational. Threshold + duration are
/// configured via <c>Abp.Identity.Lockout.*</c> settings in
/// CaseEvaluationSettingDefinitionProvider.
/// </summary>
public class LockedOutModel : AbpPageModel
{
    public IActionResult OnGet() => Page();
}
