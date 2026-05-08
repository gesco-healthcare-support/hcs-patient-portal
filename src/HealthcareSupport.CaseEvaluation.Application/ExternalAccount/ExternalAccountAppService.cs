using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Phase 10 (2026-05-03) -- OLD-parity password-reset surface. See
/// <see cref="IExternalAccountAppService"/> for the rationale behind
/// shipping a NEW AppService rather than overriding ABP Pro's
/// <c>AccountAppService</c> (member obfuscation makes service-replacement
/// fragile across patch versions).
///
/// <para>Two endpoints; both anonymous so unauthenticated users can
/// reset; rate-limited at the HTTP layer
/// (<c>CaseEvaluationHttpApiHostModule</c>) to 5 requests / hour / email
/// key per audit Q3 resolution.</para>
///
/// <para>Email send is synchronous via <see cref="IEmailSender"/>. The
/// existing <c>SendAppointmentEmailJob</c> Hangfire pipeline could
/// background it, but for password-reset the user expects to see the
/// "check your email" message and then check immediately -- a queued
/// delivery introduces a perceived "did it send" gap that has worse UX
/// than the synchronous SMTP latency. ACS placeholder credentials in
/// dev throw at the transport layer; we catch + log so the user-visible
/// "if registered, check your email" message remains generic
/// (avoids account-enumeration leak).</para>
/// </summary>
[RemoteService(IsEnabled = false)]
public class ExternalAccountAppService : CaseEvaluationAppService, IExternalAccountAppService
{
    // 2026-05-07 (Wave 3 #17.1): default flipped to the Phase 1A Falkinstein
    // tenant subdomain on plain HTTP (the Docker-exposed AuthServer port). Used
    // only when ABP setting subsystem returns null for AuthServerBaseUrl --
    // defensive fallback. Override per-tenant in /setting-management.
    private const string DefaultAuthServerBaseUrl = "http://falkinstein.localhost:44368";

    private readonly IdentityUserManager _userManager;
    private readonly ISettingProvider _settingProvider;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ExternalAccountAppService> _logger;

    public ExternalAccountAppService(
        IdentityUserManager userManager,
        ISettingProvider settingProvider,
        IEmailSender emailSender,
        ILogger<ExternalAccountAppService> logger)
    {
        _userManager = userManager;
        _settingProvider = settingProvider;
        _emailSender = emailSender;
        _logger = logger;
    }

    [AllowAnonymous]
    public virtual async Task SendPasswordResetCodeAsync(SendPasswordResetCodeInput input)
    {
        Check.NotNull(input, nameof(input));
        var normalizedEmail = NormalizeEmail(input.Email);
        if (normalizedEmail.Length == 0)
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        PasswordResetGate.EnsureUserCanRequestReset(user);
        if (user == null)
        {
            // Silent return -- caller treats this as generic success.
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var authServerBaseUrl = await ResolveAuthServerBaseUrlAsync();
        var resetUrl = BuildResetUrl(authServerBaseUrl, user.Id, token, input.ReturnUrl);

        var subject = L["Account:PasswordResetEmailSubject"].Value;
        var body = string.Format(L["Account:PasswordResetEmailBody"].Value, resetUrl);
        try
        {
            await _emailSender.SendAsync(user.Email!, subject, body, isBodyHtml: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService.SendPasswordResetCodeAsync: SMTP delivery failed for user {UserId}. Configure SMTP / ACS credentials to deliver. Returning generic success to caller.",
                user.Id);
        }
    }

    [AllowAnonymous]
    public virtual async Task ResetPasswordAsync(ResetPasswordInput input)
    {
        Check.NotNull(input, nameof(input));
        EnsurePasswordsMatch(input.Password, input.ConfirmPassword);

        var user = await _userManager.FindByIdAsync(input.UserId.ToString());
        if (user == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid);
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, input.ResetToken, input.Password);
        if (!resetResult.Succeeded)
        {
            // ABP Identity surfaces both "token invalid" and "new password
            // failed policy" through the same IdentityResult.Errors list.
            // Token-related codes ("InvalidToken") are silently mapped to
            // ResetPasswordTokenInvalid to avoid info leak; policy-violation
            // codes (PasswordRequiresDigit etc.) re-throw verbatim so the
            // user can see what to fix. The classifier matches the codes
            // ABP returns for token vs policy failures.
            if (IsTokenFailure(resetResult))
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid);
            }
            throw new UserFriendlyException(
                string.Join(", ", resetResult.Errors.Select(e => e.Description)));
        }

        // OLD parity: the post-reset confirmation email is sent inline.
        // ABP 10.0.2 has no UserPasswordChangedEto distributed event we can
        // subscribe to (verified by reflection 2026-05-03), so the master
        // plan's "subscribe to UserPasswordChangedEto" instruction is
        // adapted here: the confirmation goes out immediately after a
        // successful ResetPasswordAsync. SMTP failure is logged but not
        // bubbled -- the user has already had their password changed.
        var subject = L["Account:PasswordChangedEmailSubject"].Value;
        var body = L["Account:PasswordChangedEmailBody"].Value;
        try
        {
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService.ResetPasswordAsync: post-reset confirmation SMTP delivery failed for user {UserId}.",
                user.Id);
        }
    }

    private async Task<string> ResolveAuthServerBaseUrlAsync()
    {
        var configured = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultAuthServerBaseUrl;
        }
        return configured.TrimEnd('/');
    }

    /// <summary>
    /// Trims + lowercases the inbound email so reverse lookups match the
    /// normalized form ABP Identity stores in <c>NormalizedEmail</c>. Returns
    /// an empty string when the input is null / whitespace -- callers treat
    /// this as a no-op (silent success path).
    /// Internal so unit tests can verify normalization edge cases.
    /// </summary>
    internal static string NormalizeEmail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }
        return raw.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Mirrors OLD <c>PutCredentialValidation</c>'s
    /// <c>Password.Equals(ConfirmPassword)</c> check
    /// (<c>UserAuthenticationDomain.cs:222</c>). Ordinal compare so trailing
    /// whitespace + case differences are caught.
    /// Internal for unit-test coverage.
    /// </summary>
    internal static void EnsurePasswordsMatch(string password, string confirmPassword)
    {
        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            throw new UserFriendlyException(
                "Password and confirm password do not match");
        }
    }

    /// <summary>
    /// Builds the AuthServer reset URL the user clicks from the email.
    /// Format: <c>{base}/Account/ResetPassword?userId={guid}&amp;resetToken={url-encoded-token}</c>
    /// (matches ABP Pro's built-in ResetPassword Razor page query-string
    /// contract). When <paramref name="returnUrl"/> is supplied it is
    /// appended verbatim so the AuthServer can chain the user back to
    /// the page they came from after a successful reset.
    /// Internal for unit-test coverage.
    /// </summary>
    internal static string BuildResetUrl(
        string authServerBaseUrl,
        Guid userId,
        string resetToken,
        string? returnUrl)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(authServerBaseUrl);
        builder.Append("/Account/ResetPassword");
        builder.Append("?userId=").Append(userId.ToString());
        builder.Append("&resetToken=").Append(WebUtility.UrlEncode(resetToken));
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            builder.Append("&returnUrl=").Append(WebUtility.UrlEncode(returnUrl));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Heuristic: ABP Identity's ResetPasswordAsync surfaces token failures
    /// with code <c>"InvalidToken"</c> (verified against ASP.NET Core
    /// Identity 10.0.x source -- IdentityErrorDescriber.InvalidToken).
    /// Anything else -- digit / length / non-alphanumeric policy errors --
    /// is a password-policy failure the user can fix without a new email.
    /// Internal for unit-test coverage.
    /// </summary>
    internal static bool IsTokenFailure(Microsoft.AspNetCore.Identity.IdentityResult result)
    {
        if (result == null)
        {
            return false;
        }
        foreach (var err in result.Errors)
        {
            if (string.Equals(err.Code, "InvalidToken", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
