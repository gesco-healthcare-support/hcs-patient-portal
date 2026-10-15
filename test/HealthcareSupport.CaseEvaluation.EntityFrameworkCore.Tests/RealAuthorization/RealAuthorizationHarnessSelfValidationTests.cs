using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;
using Xunit;
using HealthcareSupport.CaseEvaluation.Security;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// #707 LAYER 3, PRECONDITIONS. Everything else in this namespace asserts that a caller is
/// refused. None of those assertions means anything unless this harness genuinely differs
/// from every other one in the repository, so these tests prove the difference directly
/// rather than inferring it from a refusal.
///
/// <para>Mirrors <c>MultiOfficeHarnessSelfValidationTests</c>, which does the same job for
/// the isolation harness: prove the fixture before trusting what it reports.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class RealAuthorizationHarnessSelfValidationTests : CaseEvaluationRealAuthorizationTestBase
{
    /// <summary>
    /// The three services ABP's <c>AddAlwaysAllowAuthorization()</c> registers must NOT be
    /// what this harness resolves.
    ///
    /// <para>Asserted by type NAME because the always-allow implementations are internal to
    /// Volo.Abp.Authorization and cannot be named in a typeof. A name comparison is the
    /// weaker assertion in general, and it is the right one here: the module removes
    /// nothing, so there is no removal for this test to agree with. It composes the ABP
    /// module chain directly and simply never calls AddAlwaysAllowAuthorization(). This
    /// assertion is therefore a backstop against that call being ADDED later, which is the
    /// realistic regression and the one the NOTE in
    /// CaseEvaluationRealAuthorizationTestModule.ConfigureServices warns against.</para>
    ///
    /// <para>A rename inside Volo.Abp would defeat a name comparison, and that is accepted
    /// rather than overlooked: PermissionChecker_DistinguishesGrantedFromUngranted below is
    /// behavioural, not name-based, and would fail if always-allow were in force whatever
    /// the implementation types were called. The two assertions together do not depend on
    /// that string staying stable.</para>
    /// </summary>
    [Fact]
    public void Harness_DoesNotResolveAbpsAlwaysAllowAuthorizationServices()
    {
        GetRequiredService<IAuthorizationService>().GetType().Name
            .ShouldNotBe("AlwaysAllowAuthorizationService");

        GetRequiredService<IMethodInvocationAuthorizationService>().GetType().Name
            .ShouldNotBe("AlwaysAllowMethodInvocationAuthorizationService");

        GetRequiredService<IPermissionChecker>().GetType().Name
            .ShouldNotBe("AlwaysAllowPermissionChecker");
    }

    /// <summary>
    /// The permission checker answers FALSE for a permission the caller's role was never
    /// granted, and TRUE for the one it was.
    ///
    /// <para>This is the narrowest possible proof that the grant store is wired and being
    /// read. It is deliberately asserted at the <see cref="IPermissionChecker"/> level,
    /// below the interceptor: if this pair ever disagrees, every behavioural test in this
    /// namespace is reporting on a broken fixture rather than on an attribute, and this
    /// test names that cause in one line.</para>
    /// </summary>
    [Fact]
    public async Task PermissionChecker_DistinguishesGrantedFromUngranted()
    {
        var fixture = await GetFixtureAsync();
        var currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        var permissionChecker = GetRequiredService<IPermissionChecker>();
        var currentTenant = GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>();

        await WithUnitOfWorkAsync(async () =>
        {
            using (currentTenant.Change(fixture.Office.OfficeId))
            using (WithCurrentUser.Run(
                currentPrincipalAccessor, fixture.Office.BookerUserId, MinimalRoleName))
            {
                (await permissionChecker.IsGrantedAsync(MinimalRoleGrant))
                    .ShouldBeTrue(
                        $"the {MinimalRoleName} role was seeded WITH {MinimalRoleGrant}; if this " +
                        "is false the grant store is not being read and every refusal in this " +
                        "namespace is meaningless");

                (await permissionChecker.IsGrantedAsync("CaseEvaluation.Patients.RevealSsn"))
                    .ShouldBeFalse(
                        $"the {MinimalRoleName} role was never granted RevealSsn; if this is " +
                        "true the harness is still allowing everything");
            }
        }, requiresNew: true);
    }
}
