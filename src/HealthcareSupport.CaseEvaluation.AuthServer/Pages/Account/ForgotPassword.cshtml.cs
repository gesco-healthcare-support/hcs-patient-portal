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
/// <c>/Account/ForgotPassword</c>. Filesystem precedence overrides the
/// stock <c>AbpAccountPublicWeb</c> RCL page; we substitute our own
/// PageModel because the stock model calls
/// <c>IAccountAppService.SendPasswordResetCodeAsync</c> which is broken
/// in this project by a <c>Scriban.Parsing.ParserOptions</c>
/// <c>TypeLoadException</c> (BUG-018, separate fix). The stock model
/// swallows that exception silently and surfaces a fake-success page,
/// so no email is ever sent.
///
/// <para>POSTing the form invokes
/// <see cref="IExternalAccountAppService.SendPasswordResetCodeAsync"/>
/// directly via DI -- same in-process pattern
/// <c>ResendVerification.cshtml.cs</c> uses. The custom service
/// dispatches the reset email through <c>INotificationDispatcher</c>
/// (per-tenant <c>ResetPassword</c> template, queued via Hangfire on
/// the API host), so it sidesteps the Scriban path entirely.</para>
///
/// <para>OWASP-correct generic-success UX: the page always shows
/// "if the email matches a registered account, a link is on its way"
/// regardless of whether the user exists, has been deleted, has an
/// unconfirmed email, or the dispatch itself failed. Exceptions are
/// logged + swallowed so neither the response nor the timing leaks
/// account-existence information.</para>
/// </summary>
public class ForgotPasswordModel : AbpPageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Enter your email.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    public string? Email { get; set; }

    /// <summary>
    /// Item D (2026-08-22) -- true when the reset request was refused by the per-address throttle, so
    /// the page shows a wait message instead of the usual generic success. Wording is a literal here
    /// to match this page's existing style (its validation messages are literals too); the localized
    /// resource entry exists for the API surface, which returns 429.
    /// </summary>
    public bool IsThrottled { get; private set; }

    /// <summary>What to tell a throttled requester. Deliberately says nothing about account existence.</summary>
    public const string ThrottleMessage =
        "A reset link was already requested for this email address very recently. "
        + "Please wait a minute and try again, or check your inbox and spam folder for the message "
        + "already on its way.";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool RequestSubmitted { get; set; }

    private readonly IExternalAccountAppService _externalAccountAppService;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        IExternalAccountAppService externalAccountAppService,
        ILogger<ForgotPasswordModel> logger)
    {
        _externalAccountAppService = externalAccountAppService;
        _logger = logger;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Email))
        {
            return Page();
        }

        try
        {
            await _externalAccountAppService.SendPasswordResetCodeAsync(
                new SendPasswordResetCodeInput
                {
                    Email = Email!,
                    ReturnUrl = ReturnUrl,
                });
        }
        catch (BusinessException ex)
            when (ex.Code == CaseEvaluationDomainErrorCodes.PasswordResetThrottled)
        {
            // Item D (2026-08-22) -- the ONE deliberate exception to this page's generic-success rule.
            //
            // Safe to surface: the throttle partitions on the SUBMITTED address, so a requester learns
            // only their own request rate and nothing about whether an account exists. The response is
            // identical for a registered and an unregistered address, because the AppService checks and
            // stamps the throttle BEFORE it looks the user up.
            //
            // And necessary to surface: reset is now the way back in from a lockout, so a silent
            // refusal would leave a locked-out user waiting for mail that was never sent -- the worst
            // possible moment to be reassuring and wrong.
            //
            // Logged WITH the exception even though the code is the whole story: a throttle rejection
            // that turns out to be something else wearing the same error code is exactly the case that
            // needs a stack trace, and a bare message would hide it.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    ex,
                    "ForgotPasswordModel.OnPostAsync: reset throttled for email-key {EmailKey}.",
                    Email);
            }

            IsThrottled = true;
            return Page();
        }
        catch (Exception ex)
        {
            // Generic-success UX: log + swallow so neither the response
            // body nor the response timing leaks whether the email is
            // registered, unconfirmed, inactive, or simply failed to
            // dispatch. The AppService itself already logs dispatch
            // failures with more context.
            _logger.LogWarning(
                ex,
                "ForgotPasswordModel.OnPostAsync: SendPasswordResetCodeAsync threw for email-key {EmailKey}; surfacing generic success.",
                Email);
        }

        RequestSubmitted = true;
        return Page();
    }
}
