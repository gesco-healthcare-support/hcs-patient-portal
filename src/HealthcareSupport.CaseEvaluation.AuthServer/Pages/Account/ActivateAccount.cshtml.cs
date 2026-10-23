using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// <c>/Account/ActivateAccount</c> -- the page an activate-your-account email links to
/// (<c>?userId=&amp;token=</c>). The person chooses a password here, and submitting it is what
/// confirms their email, so they can sign in straight away.
///
/// <para><b>GET and HEAD only look.</b> Mail scanners (Microsoft 365 Safe Links among them) open a
/// link seconds before the person does, and Razor Pages answers HEAD with the GET handler. So the GET
/// handler asks <see cref="IAccountActivationLinkChecker"/>, which never consumes the token, and
/// renders either the form or the dead-link message. Only the antiforgery-protected POST, carrying a
/// password the person typed, changes anything.</para>
///
/// <para>The logic lives in <see cref="IAccountActivationService"/> in the Application layer, where
/// it is tested; this model only maps its outcomes to the page. A dead link (used, expired, tampered,
/// unknown user) gets one message for every case, so the page says nothing about which accounts
/// exist, and points at the resend page, which sends a fresh activation link.</para>
/// </summary>
[AllowAnonymous]
public class ActivateAccountModel : AbpPageModel
{
    public const string DeadLinkMessage =
        "That activation link doesn't work anymore. It may have been used already or have expired.";

    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(128, MinimumLength = 6)]
    public string? Password { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter the password again.")]
    [StringLength(128, MinimumLength = 6)]
    [Compare(nameof(Password), ErrorMessage = "Passwords don't match.")]
    public string? ConfirmPassword { get; set; }

    /// <summary>True when the link cannot be used; the view shows the message and no form.</summary>
    public bool LinkIsDead { get; private set; }

    public string? ErrorMessage { get; set; }

    private readonly IAccountActivationLinkChecker _linkChecker;
    private readonly IAccountActivationService _activationService;
    private readonly ILogger<ActivateAccountModel> _logger;

    public ActivateAccountModel(
        IAccountActivationLinkChecker linkChecker,
        IAccountActivationService activationService,
        ILogger<ActivateAccountModel> logger)
    {
        _linkChecker = linkChecker;
        _activationService = activationService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        LinkIsDead = !await _linkChecker.IsUsableAsync(UserId, Token);
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
            await _activationService.ActivateAsync(new ActivateAccountInput
            {
                UserId = UserId,
                Token = Token,
                Password = Password!,
                ConfirmPassword = ConfirmPassword!,
            });
        }
        catch (BusinessException ex)
            when (ex.Code == CaseEvaluationDomainErrorCodes.ActivationLinkInvalid)
        {
            LinkIsDead = true;
            return Page();
        }
        catch (UserFriendlyException ex)
        {
            // Password policy (or the two entries differing). Nothing was changed, so the same link
            // still works: show the reason and keep the form.
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ActivateAccountModel.OnPostAsync: activation threw for user {UserId}.", UserId);
            ErrorMessage = "We could not activate your account just now. Please try again.";
            return Page();
        }

        // The Login page shows "Password updated. Sign in with your new password." and, with no
        // return URL, sends the signed-in person on to the portal itself.
        return LocalRedirect("~/Account/Login?flash=password-updated");
    }
}
