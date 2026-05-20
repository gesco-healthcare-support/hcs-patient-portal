using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Standalone custom Razor PageModel at <c>/Account/ConfirmUser</c>.
/// Filesystem precedence overrides the Pro framework's stock
/// ConfirmUser page. B-3 (proposed-copy.md 2.4, locked 2026-05-18)
/// retired the dedicated ConfirmUser page in favour of the Login
/// page's anti-enumeration banner + always-visible Resend verification
/// link.
///
/// <para>Behaviour: any request method (GET or POST) 302s to
/// <c>/Account/Login</c>. Stale bookmarks, old email references, and
/// any in-flight links from the legacy flow bounce here and land
/// somewhere useful instead of a 404.</para>
/// </summary>
[AllowAnonymous]
public class ConfirmUserModel : AbpPageModel
{
    public IActionResult OnGet() => RedirectToPage("./Login");
    public IActionResult OnPost() => RedirectToPage("./Login");
}
