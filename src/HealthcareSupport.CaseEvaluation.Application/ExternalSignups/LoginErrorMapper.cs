using System;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Phase 9 (2026-05-03) -- pure mapping helper from ASP.NET Core Identity
/// + ABP Account error codes to OLD-verbatim user-facing localization keys.
///
/// <para>OLD's <c>UserAuthenticationDomain.PostLogin</c>
/// (<c>P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs</c>)
/// surfaces exactly four user-facing strings:</para>
///
/// <list type="bullet">
///   <item><c>"User not exist"</c> -- line 73, 152
///         (<c>ValidationFailedCode.UserNotExist</c>): user not found by
///         email OR <c>StatusId == Status.Delete</c>.</item>
///   <item><c>"Invalid username or password"</c> -- line 120
///         (<c>ValidationFailedCode.InvalidUserNamePassword</c>): verified
///         user, password mismatch.</item>
///   <item><c>"Your account is not activated"</c> -- line 65
///         (<c>ValidationFailedCode.UserInactivated</c>):
///         <c>StatusId == InActive</c> or <c>IsActive == false</c>.</item>
///   <item><c>"We have sent a verification link to your registered email
///         id, please verify your email address to login."</c> -- line 143
///         (hardcoded literal): <c>IsVerified == false</c> after
///         auto-resend.</item>
/// </list>
///
/// <para>NEW relies on ABP's <c>RequireConfirmedEmail = true</c> setting
/// (Phase 2.2) which makes ABP return <c>IdentityResult.Failed</c> with
/// codes like <c>EmailNotConfirmed</c> on unverified login. The AuthServer
/// Razor login page consumes those codes and asks
/// <see cref="MapAbpErrorToLocalizationKey"/> for the appropriate
/// OLD-verbatim message key. The Razor page also conditionally renders a
/// "Resend confirmation email" link when
/// <see cref="ShouldShowResendLink"/> returns <c>true</c>.</para>
///
/// <para>This class is a pure function with no DI dependencies. Promoted
/// to <c>public</c> in G4 (2026-05-04) so the AuthServer assembly can
/// import it directly for the Login.cshtml override; previously
/// <c>internal static</c> with InternalsVisibleTo for the test project.</para>
/// </summary>
public static class LoginErrorMapper
{
    public const string UserNotExist = "Login:UserNotExist";
    public const string InvalidUsernameOrPassword = "Login:InvalidUsernameOrPassword";
    public const string AccountNotActivated = "Login:AccountNotActivated";
    public const string VerificationLinkSent = "Login:VerificationLinkSent";

    /// <summary>
    /// Maps an ASP.NET Core / ABP <see cref="IdentityError.Code"/> to the
    /// OLD-verbatim localization key. Unknown codes default to
    /// <see cref="InvalidUsernameOrPassword"/>: ABP's stock error response
    /// already covers most password / lockout cases, so an unmapped code is
    /// almost always a credential failure of some kind, and falling back to
    /// the same generic message OLD used keeps the surface OLD-faithful.
    /// </summary>
    /// <param name="errorCode">
    /// The Identity error code. Comparison is case-insensitive (ordinal).
    /// Null or whitespace returns <see cref="InvalidUsernameOrPassword"/>.
    /// </param>
    public static string MapAbpErrorToLocalizationKey(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return InvalidUsernameOrPassword;
        }

        // ASP.NET Core IdentityErrorDescriber + ABP Account error codes
        // mapped per OLD UserAuthenticationDomain.cs:65, 73, 120, 143, 152.
        // Comparison is case-insensitive ordinal so callers don't need to
        // normalize.
        return errorCode switch
        {
            // Email confirmation gate -- both stock ASP.NET Identity and ABP
            // Account use these codes when RequireConfirmedEmail = true and
            // the user has not yet confirmed.
            var c when Eq(c, "EmailNotConfirmed") => VerificationLinkSent,
            var c when Eq(c, "EmailNotConfirmedAndNotAllowed") => VerificationLinkSent,

            // Account inactive / locked / not allowed.
            var c when Eq(c, "LockedOut") => AccountNotActivated,
            var c when Eq(c, "NotAllowed") => AccountNotActivated,
            var c when Eq(c, "IsNotActive") => AccountNotActivated,
            var c when Eq(c, "UserLockedOut") => AccountNotActivated,

            // Account not found cases (ABP returns these for unknown email +
            // soft-deleted users).
            var c when Eq(c, "UserNotFound") => UserNotExist,
            var c when Eq(c, "InvalidUserName") => UserNotExist,
            var c when Eq(c, "UserDoesNotExist") => UserNotExist,

            // Credential mismatch.
            var c when Eq(c, "InvalidLogin") => InvalidUsernameOrPassword,
            var c when Eq(c, "PasswordMismatch") => InvalidUsernameOrPassword,
            var c when Eq(c, "InvalidPassword") => InvalidUsernameOrPassword,

            // Default -- treat unknown as a credential failure.
            _ => InvalidUsernameOrPassword,
        };
    }

    /// <summary>
    /// True when the given error code indicates the user account exists but
    /// has not yet confirmed its email address. The Razor login page should
    /// render a "Resend confirmation email" link for these cases (Phase 9
    /// audit Q2 resolution).
    /// </summary>
    public static bool ShouldShowResendLink(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return false;
        }
        return Eq(errorCode, "EmailNotConfirmed")
            || Eq(errorCode, "EmailNotConfirmedAndNotAllowed");
    }

    /// <summary>
    /// Convenience overload that walks an
    /// <see cref="Microsoft.AspNetCore.Identity.IdentityResult"/> and returns
    /// the first matching localization key. Falls back to
    /// <see cref="InvalidUsernameOrPassword"/> when no errors or none match.
    /// </summary>
    public static string MapIdentityResult(IdentityResult? result)
    {
        if (result == null || result.Errors == null)
        {
            return InvalidUsernameOrPassword;
        }
        foreach (var err in result.Errors)
        {
            if (err == null || string.IsNullOrWhiteSpace(err.Code))
            {
                continue;
            }
            return MapAbpErrorToLocalizationKey(err.Code);
        }
        return InvalidUsernameOrPassword;
    }

    private static bool Eq(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
