using HealthcareSupport.CaseEvaluation.ExternalAccount;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Standalone custom Razor page hosted on the AuthServer at <c>/Account/LockedOut</c>. Filesystem
/// precedence overrides the stock <c>AbpAccountPublicWeb</c> RCL page.
///
/// <para>Item D (2026-08-22): the page no longer states a fixed duration. Lockout is now progressive
/// (1 -> 5 -> 15 minutes -> the configured maximum), so the previous hard-coded "1 hour" was wrong for
/// most lockouts and overstated the wait by up to 59 minutes.</para>
///
/// <para>The framework's redirect carries no user id, so this page still cannot look the user up
/// itself. <c>LoginModel</c> computes the real remainder -- it still has the submitted identifier --
/// and passes it in TempData. When that is absent (a direct navigation, or an identifier that did not
/// resolve) the wording degrades to the generic phrase rather than inventing a number.</para>
/// </summary>
public class LockedOutModel : AbpPageModel
{
    /// <summary>
    /// Human-readable remaining lockout time, e.g. "about 5 minutes", or
    /// <see cref="LockoutRemainingText.Unknown"/> when it could not be determined.
    /// </summary>
    public string RemainingText { get; private set; } = LockoutRemainingText.Unknown;

    public IActionResult OnGet()
    {
        if (TempData.TryGetValue(LoginModel.LockoutRemainingTempDataKey, out var value)
            && value is string text
            && !string.IsNullOrWhiteSpace(text))
        {
            RemainingText = text;
        }

        return Page();
    }
}
