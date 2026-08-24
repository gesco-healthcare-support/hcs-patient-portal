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

    /// <summary>
    /// Item C (2026-08-22) -- whether a user with NO tenant may be served an account email.
    ///
    /// <para><b>Why the old guard has to go.</b> Both account-email flows returned early for any
    /// null-tenant user, with the comment "External user without a tenant is a code bug". That was
    /// true when written; Phase D made internal operators HOST logins and invalidated the premise, so
    /// internal staff silently could not self-reset at all. The AccountEmailer was updated the same
    /// day and this path was missed.</para>
    ///
    /// <para><b>Why not simply allow every null-tenant user.</b> The risk is not disclosure, it is
    /// PRIVILEGE. A host row carrying an EXTERNAL role is unreachable through the product -- both the
    /// registration and invite paths throw without a tenant -- so it can only be hand-made via SQL or
    /// a dev seeder. Enabling reset on such a row would make it usable on the staff portal, which
    /// today's broken guard prevents by accident. This gate does it on purpose.</para>
    ///
    /// <para>So the rule is a positive allow-list on ROLE, which fails CLOSED, plus the
    /// <c>IsExternalUser</c> flag as defence in depth on a security path. Roles do not live on
    /// <see cref="IdentityUser"/>, so the caller fetches them and passes them in -- which also keeps
    /// this pure and unit-testable without ABP DI.</para>
    /// </summary>
    public static bool IsHostAccountEligible(
        System.Collections.Generic.IEnumerable<string?>? roles,
        bool isExternalFlag)
    {
        if (isExternalFlag)
        {
            return false;
        }

        // Reuses the product's single definition of "internal role" instead of restating the list, so
        // the two cannot drift apart. It already trims, compares case-insensitively, and returns false
        // for a null or empty set -- exactly the fail-closed behaviour this gate needs.
        return Appointments.BookingFlowRoles.IsInternalUserCaller(roles);
    }
}
