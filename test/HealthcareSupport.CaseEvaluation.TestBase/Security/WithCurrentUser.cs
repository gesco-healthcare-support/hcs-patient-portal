using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Security.Claims;

namespace HealthcareSupport.CaseEvaluation.Security;

/// <summary>
/// IDisposable scope that swaps the test fixture's current principal to a
/// supplied user id (and optional roles) for the duration of a `using` block.
///
/// Wraps ABP's <see cref="ICurrentPrincipalAccessor.Change(ClaimsPrincipal)"/>
/// which returns its own IDisposable; the swap is async-local so the prior
/// principal is restored on dispose without affecting other concurrent tests.
///
/// Usage:
/// <code>
/// using (WithCurrentUser.Run(currentPrincipalAccessor, IdentityUsersTestData.Patient1UserId, "Patient"))
/// {
///     var profile = await _patientsAppService.GetMyProfileAsync();
///     profile.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
/// }
/// </code>
///
/// Phase B-6 Wave-2 PR-W2C: built to unblock the 2 skipped Patient profile
/// Facts (GetMyProfileAsync, UpdateMyProfileAsync) and to provide the
/// authenticated-caller setup for ExternalSignup tests in PR-W2D.
/// Built against the existing FakeCurrentPrincipalAccessor (which extends
/// ABP's ThreadCurrentPrincipalAccessor); no production-code changes.
/// </summary>
public static class WithCurrentUser
{
    /// <summary>
    /// Pushes a new principal carrying the supplied UserId + optional Role
    /// claims onto the accessor's async-local stack. Disposing the returned
    /// IDisposable restores the prior principal.
    /// </summary>
    public static IDisposable Run(
        ICurrentPrincipalAccessor accessor,
        Guid userId,
        params string[] roles)
    {
        var claims = new List<Claim>
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
            new Claim(AbpClaimTypes.UserName, $"test-user-{userId:N}"),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(AbpClaimTypes.Role, role));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return accessor.Change(principal);
    }

    /// <summary>
    /// As <see cref="Run"/>, but also emits an Email claim so
    /// <c>ICurrentUser.Email</c> is populated.
    ///
    /// <para>DO NOT fold this into <see cref="Run"/> as an optional parameter or an overload.
    /// Two independent reasons:</para>
    ///
    /// <para>1. C# forbids an optional parameter ahead of a <c>params</c> array, and an overload
    /// taking the email third would silently rebind existing two-argument-plus-role calls such as
    /// <c>Run(accessor, id, "Patient")</c> to <c>email: "Patient"</c> with no roles. There are 36
    /// <see cref="Run"/> call sites; none is affected by adding this alongside.</para>
    ///
    /// <para>2. <see cref="Run"/>'s LACK of an email claim is pinned behaviour, not an oversight.
    /// <c>AbpClaimTypes.Email</c> appears exactly once elsewhere in the repo -- the default principal
    /// in <c>FakeCurrentPrincipalAccessor</c> -- and <see cref="Run"/> pushes over that default, so
    /// the email is actively removed rather than merely unset.
    /// <c>NotificationTemplatesAppServiceTests.SendTestAsync_WhenCurrentUserHasNoEmail_ThrowsUserFriendly</c>
    /// DEPENDS on that: it asserts a UserFriendlyException precisely because the email is null.
    /// Teaching <see cref="Run"/> to emit an email would break that test outright.</para>
    ///
    /// <para>Needed by any rule that reads the caller's email -- notably
    /// <c>AppointmentAccessRules.IsAppointmentEmailRoleVisible</c>, which returns false
    /// immediately on a null email, so a test using <see cref="Run"/> alone would pass
    /// with the email+role rule deleted.</para>
    /// </summary>
    public static IDisposable RunWithEmail(
        ICurrentPrincipalAccessor accessor,
        Guid userId,
        string? email,
        params string[] roles)
    {
        var claims = new List<Claim>
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
            new Claim(AbpClaimTypes.UserName, $"test-user-{userId:N}"),
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(AbpClaimTypes.Email, email));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(AbpClaimTypes.Role, role));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return accessor.Change(principal);
    }
}
