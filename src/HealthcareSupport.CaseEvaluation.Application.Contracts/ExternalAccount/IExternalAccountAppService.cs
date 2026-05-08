using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// OLD-parity password-reset surface. Mirrors OLD
/// <c>UserAuthenticationDomain.PostForgotPassword</c> +
/// <c>UserAuthenticationDomain.PutCredential</c>
/// (<c>P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs</c>:182-256).
///
/// <para>NEW intentionally builds this as a NEW AppService rather than
/// overriding ABP's <c>IAccountAppService</c>: ABP Pro 10.0.2 obfuscates
/// the Account module heavily (member names like
/// <c>L8nSSMWkSiiYJHjaCG9.lRHeAfgqo7WklTsGVuU</c>), so subclassing or
/// service-replacement is fragile across patch versions. This AppService
/// goes through stable lower-level primitives:
/// <see cref="Volo.Abp.Identity.IdentityUserManager"/> (open-source, ABP
/// Identity Domain) for password-reset token generation +
/// <c>Volo.Abp.Emailing.IEmailSender</c> for delivery.</para>
///
/// <para>Both endpoints are anonymous and rate-limited
/// (5 requests / hour / email key) per audit Q3 resolution.</para>
/// </summary>
public interface IExternalAccountAppService : IApplicationService
{
    /// <summary>
    /// OLD-parity gates (mirrors <c>ForgotPasswordValidation</c>):
    ///   - User not found OR soft-deleted -> generic success (no info leak).
    ///   - User !EmailConfirmed -> <c>BusinessException(EmailNotConfirmedForPasswordReset)</c>.
    ///   - User !IsActive -> <c>BusinessException(UserInactiveForPasswordReset)</c>.
    ///
    /// On success: generates a cryptographic reset token via
    /// <c>UserManager.GeneratePasswordResetTokenAsync</c>, builds the URL
    /// <c>{authServerBaseUrl}/Account/ResetPassword?userId={guid}&amp;resetToken={token}</c>,
    /// and sends an email via the seeded <c>ResetPassword</c>
    /// notification template (Phase 4).
    /// </summary>
    Task SendPasswordResetCodeAsync(SendPasswordResetCodeInput input);

    /// <summary>
    /// OLD-parity reset (mirrors <c>PutCredential</c>):
    ///   - Validates Password == ConfirmPassword (ordinal compare).
    ///   - Resolves user by id; null user -> <c>ResetPasswordTokenInvalid</c>.
    ///   - Calls <c>UserManager.ResetPasswordAsync(user, ResetToken,
    ///     Password)</c> -- ABP Identity enforces the password policy
    ///     (Phase 2.1) and validates the token. Failure ->
    ///     <c>ResetPasswordTokenInvalid</c> (generic to avoid info leak).
    ///   - On success, sends the OLD-verbatim post-reset confirmation
    ///     email via the seeded <c>PasswordChange</c> notification
    ///     template.
    /// </summary>
    Task ResetPasswordAsync(ResetPasswordInput input);
}
