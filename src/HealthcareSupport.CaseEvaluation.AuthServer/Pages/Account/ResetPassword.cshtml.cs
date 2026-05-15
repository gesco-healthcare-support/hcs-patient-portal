using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Standalone custom Razor page hosted on the AuthServer at
/// <c>/Account/ResetPassword</c>. Filesystem precedence overrides the
/// stock <c>AbpAccountPublicWeb</c> RCL page. The stock page looks up
/// the user on GET to validate the reset token; missing / invalid user
/// surfaces an unhandled <c>EntityNotFoundException</c> that leaks as
/// a raw 500 to the browser. The custom GET skips that lookup -- empty
/// <c>UserId</c> or empty <c>ResetToken</c> redirects to ForgotPassword
/// with a friendly error; everything else renders the form. Token
/// validity is checked at POST time when the AppService is invoked.
///
/// <para>POSTing the form calls
/// <see cref="IExternalAccountAppService.ResetPasswordAsync"/> directly
/// via DI. The custom service throws a
/// <c>BusinessException(ResetPasswordTokenInvalid)</c> for invalid /
/// expired tokens (we catch + redirect to ForgotPassword) and
/// re-throws password-policy violations as <c>UserFriendlyException</c>
/// (we surface in the validation summary).</para>
///
/// <para>On success, the AppService also fires the post-reset
/// confirmation email through the <c>PasswordChange</c> notification
/// template -- the user gets two emails in sequence: the reset-link
/// email and the confirmation email after their password is changed.
/// We set <c>TempData["SuccessMessage"]</c> and redirect to
/// <c>/Account/Login</c> so the next page can render a banner.</para>
/// </summary>
public class ResetPasswordModel : AbpPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ResetToken { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string? Password { get; set; }

    [BindProperty]
    [Required]
    [StringLength(128, MinimumLength = 6)]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string? ConfirmPassword { get; set; }

    public string? ErrorMessage { get; set; }

    private readonly IExternalAccountAppService _externalAccountAppService;
    private readonly ILogger<ResetPasswordModel> _logger;

    public ResetPasswordModel(
        IExternalAccountAppService externalAccountAppService,
        ILogger<ResetPasswordModel> logger)
    {
        _externalAccountAppService = externalAccountAppService;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        if (UserId == Guid.Empty || string.IsNullOrWhiteSpace(ResetToken))
        {
            return RedirectToForgotWithError(
                "Your reset link is missing required information. " +
                "Please request a new password reset email.");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _externalAccountAppService.ResetPasswordAsync(
                new ResetPasswordInput
                {
                    UserId = UserId,
                    ResetToken = ResetToken,
                    Password = Password!,
                    ConfirmPassword = ConfirmPassword!,
                });
        }
        catch (BusinessException ex)
            when (ex.Code == CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid)
        {
            return RedirectToForgotWithError(
                "Your reset link is invalid or has expired. Please request a new one.");
        }
        catch (UserFriendlyException ex)
        {
            // Password-policy violations (digit / length / non-alphanumeric)
            // surface as UserFriendlyException with the joined error
            // descriptions in Message. Show them in the validation summary
            // so the user can fix the password without requesting a new link.
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ResetPasswordModel.OnPostAsync: ResetPasswordAsync threw for user {UserId}.",
                UserId);
            ErrorMessage =
                "We could not reset your password. Please try again or request a new reset link.";
            return Page();
        }

        TempData["SuccessMessage"] =
            "Your password has been reset. Please sign in with your new password.";
        return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
    }

    private IActionResult RedirectToForgotWithError(string message)
    {
        TempData["ErrorMessage"] = message;
        return RedirectToPage("./ForgotPassword");
    }
}
