using Volo.Abp;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Phase 10 (2026-05-03) -- pure helper enforcing OLD's
/// <c>ForgotPasswordValidation</c> verified-only + active-only gate
/// (<c>P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs</c>:166-173).
///
/// <para>Behavior matrix (OLD-verbatim parity):</para>
/// <list type="table">
///   <item><term>user is null</term><description>silent return -- caller
///     should still report generic success to avoid leaking which emails
///     are registered. OLD audit gap: OLD reported <c>UserNotExist</c>
///     here (line 177) which DID leak; the audit's <c>L3</c> finding
///     described this as an OLD-bug-fix opportunity. NEW returns silently
///     so the AppService caller can synthesize a generic "if registered,
///     check your email" response.</description></item>
///   <item><term>!user.EmailConfirmed</term><description>throw
///     <c>BusinessException(EmailNotConfirmedForPasswordReset)</c>. OLD
///     line 168 returned the misleading "we have sent a verification
///     link" string here even though no email was actually sent; NEW
///     returns the corrected gate message
///     <c>"Please verify your email address before resetting your password."</c>.
///   </description></item>
///   <item><term>!user.IsActive</term><description>throw
///     <c>BusinessException(UserInactiveForPasswordReset)</c>. Mirrors OLD
///     line 172.</description></item>
/// </list>
///
/// <para>Internal so unit tests can verify without standing up ABP DI.</para>
/// </summary>
internal static class PasswordResetGate
{
    /// <summary>
    /// Enforces the verified-only + active-only gate. Caller-supplied user
    /// may be null when the email did not resolve to any account; the gate
    /// silently returns in that case so the AppService can report the same
    /// generic success message regardless of whether the email is
    /// registered (avoids account-enumeration leak).
    /// </summary>
    /// <exception cref="BusinessException">
    /// <see cref="CaseEvaluationDomainErrorCodes.EmailNotConfirmedForPasswordReset"/>
    /// when the user has not yet confirmed their email.
    /// <see cref="CaseEvaluationDomainErrorCodes.UserInactiveForPasswordReset"/>
    /// when the user is inactive.
    /// </exception>
    public static void EnsureUserCanRequestReset(IdentityUser? user)
    {
        if (user == null)
        {
            return;
        }
        if (!user.EmailConfirmed)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.EmailNotConfirmedForPasswordReset);
        }
        if (!user.IsActive)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.UserInactiveForPasswordReset);
        }
    }
}
