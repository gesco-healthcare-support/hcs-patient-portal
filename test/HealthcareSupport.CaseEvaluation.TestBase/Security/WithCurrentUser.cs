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
}
