using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace HealthcareSupport.CaseEvaluation.Pages.Account;

/// <summary>
/// Phase 1.D follow-up (Category 1, 2026-05-08) -- standalone "resend
/// verification email" Razor page hosted on the AuthServer at
/// <c>/Account/ResendVerification</c>. Two contexts feed users here:
///
/// <list type="bullet">
///   <item><b>Post-register:</b> after ABP's stock register page creates
///         the account and auto-fires the first verification email, the
///         user lands here with <c>?context=register&amp;email=...</c>
///         so they can re-fire the email if it didn't arrive.</item>
///   <item><b>Login-blocked:</b> when login fails because the email is
///         not yet confirmed, the user is bounced here with
///         <c>?context=login&amp;email=...</c>.</item>
/// </list>
///
/// <para>POSTing the form invokes
/// <see cref="IExternalAccountAppService.ResendEmailVerificationAsync"/>
/// directly via DI (the AuthServer references the Application module).
/// The AppService enforces the rate-limit gate
/// (3 / hour / email + 60-second cooldown, Adrian Decision 3,
/// 2026-05-08) silently -- a rate-limited submission produces the same
/// "request received" UX as a successful submission so the page does
/// not leak rate-limit state to attackers.</para>
///
/// <para>Bypasses the manual HTTP controller deliberately: cross-process
/// HTTP calls from the AuthServer to the API would require a separate
/// HTTP client + cert handling and would also hit the controller's
/// rate-limit partition (5 / hour / email shared with password-reset),
/// which is looser than this page's intended 3 / hour / email cap.
/// Direct DI keeps a single rate-limit policy.</para>
/// </summary>
public class ResendVerificationModel : AbpPageModel
{
    /// <summary>
    /// Email address the verification link should go to. Populated from
    /// the query string on GET, posted back as a hidden form field on POST.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; set; }

    /// <summary>
    /// Context flag controlling page copy: <c>register</c>, <c>login</c>,
    /// or null/unknown (renders generic body text). Round-tripped on POST.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Context { get; set; }

    /// <summary>
    /// Issue 1.4 (2026-05-12): when set to <c>1</c> on the GET,
    /// auto-fire the resend-verification flow on landing so the user
    /// doesn't need to click Send again. Used by the post-register
    /// success page's "Verify Email" primary button: it links here with
    /// <c>?context=register&amp;email=...&amp;autosend=1</c>, and the
    /// page renders the success state immediately (subject to the same
    /// rate-limit gate that an explicit POST would hit).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Autosend { get; set; }

    /// <summary>
    /// True after a POST roundtrip. The view shows the "request received"
    /// message + disables the submit button when this is true.
    /// </summary>
    public bool RequestSubmitted { get; set; }

    private readonly IExternalAccountAppService _externalAccountAppService;
    private readonly ILogger<ResendVerificationModel> _logger;

    public ResendVerificationModel(
        IExternalAccountAppService externalAccountAppService,
        ILogger<ResendVerificationModel> logger)
    {
        _externalAccountAppService = externalAccountAppService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Issue 1.4 (2026-05-12): auto-fire the resend on landing if
        // the autosend handshake flag is set AND we have a non-empty
        // email. Otherwise render the form normally so the user can
        // submit manually. Swallows exceptions the same way OnPostAsync
        // does so the UX never leaks rate-limit state.
        if (string.Equals(Autosend, "1", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(Email))
        {
            try
            {
                await _externalAccountAppService.ResendEmailVerificationAsync(
                    new ResendEmailVerificationInput { Email = Email! });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "ResendVerificationModel.OnGetAsync (autosend): ResendEmailVerificationAsync threw for email-key {EmailKey}; surfacing generic success.",
                    Email);
            }
            RequestSubmitted = true;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Email))
        {
            return Page();
        }

        try
        {
            await _externalAccountAppService.ResendEmailVerificationAsync(
                new ResendEmailVerificationInput { Email = Email! });
        }
        catch (Exception ex)
        {
            // Log + swallow. Surface a generic success message regardless of
            // outcome so the page does not leak which emails are registered
            // / confirmed / rate-limited. The AppService internally logs
            // dispatch failures with more context.
            _logger.LogWarning(
                ex,
                "ResendVerificationModel.OnPostAsync: ResendEmailVerificationAsync threw for email-key {EmailKey}; surfacing generic success.",
                Email);
        }

        RequestSubmitted = true;
        return Page();
    }

    /// <summary>
    /// View helper: heading text per <see cref="Context"/>. Internal so
    /// unit tests can verify the per-context copy contract.
    /// </summary>
    public string GetHeading()
    {
        return Context switch
        {
            "register" => "Welcome! Please verify your email",
            "login" => "Verify your email to sign in",
            _ => "Verify your email",
        };
    }

    /// <summary>
    /// View helper: body intro text per <see cref="Context"/>.
    /// </summary>
    public string GetIntro()
    {
        return Context switch
        {
            "register" =>
                "Your account was created successfully. We sent a verification link to your email. " +
                "Click the link to confirm your address and finish setting up your account. " +
                "If you did not receive the email, request a new one below.",
            "login" =>
                "Your email is not yet verified. Click the link we sent to confirm your address, " +
                "or request a new verification email below if the original is lost or expired.",
            _ =>
                "Request a new email-verification link. The link will be sent to the address below " +
                "if it matches an unverified account.",
        };
    }
}
