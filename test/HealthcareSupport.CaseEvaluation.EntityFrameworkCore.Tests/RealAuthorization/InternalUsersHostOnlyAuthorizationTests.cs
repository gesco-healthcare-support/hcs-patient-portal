using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.InternalUsers;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// Internal-user management is host-only. Internal users are host operators, so creating, listing
/// and resetting them, and the office list the screen uses, are refused to every caller inside an
/// office and admitted to the host operators who run the screen.
///
/// <para>THE OFFICE CALLERS HOLD THE GRANTS A DEPLOYED OFFICE STILL HAS. Before this change the
/// permissions could be held in an office: ABP granted them to the office's static <c>admin</c> role,
/// and the internal-role seeder granted them to the office Staff Supervisor role, which is what a host
/// Supervisor holds while switched in. Those rows are written straight into the permission-grant
/// table here, because the permission manager refuses to grant a Host-only permission inside an
/// office; these tests therefore also show that the existing rows open nothing.
/// (<see cref="HostOnlyPermissionGrantCleanupContributor"/> removes them on the next deploy.)</para>
///
/// <para>THE REFUSAL IS THE INTERCEPTOR'S. The methods also refuse an office caller in their bodies
/// (<c>InternalUsersHostCallerGuardTests</c>), so the exception type alone cannot say which one
/// refused. The interceptor's carries an ABP error code; the body's carries none.</para>
///
/// <para>THE HOST CALLERS HOLD THE GRANT SETS PRODUCTION SHIPS. The host pass of
/// <see cref="InternalUserRoleDataSeedContributor"/> runs once for the process, so an admitted call
/// proves the real IT Admin and Staff Supervisor grants reach the screen.</para>
///
/// <para>Data-light: the create names a role outside the creatable set and the reset an id that does
/// not exist, so nothing is written even for an admitted caller.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class InternalUsersHostOnlyAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string RoleProviderName = "R";
    private const string InterceptorCodePrefix = "Volo.Authorization:";

    // Independent literals: the office roles that held these permissions before the change.
    private static readonly string[] OfficeRolesThatHeldThem = ["admin", "Staff Supervisor"];

    private static readonly string[] Permissions =
    [
        CaseEvaluationPermissions.InternalUsers.Default,
        CaseEvaluationPermissions.InternalUsers.Create,
        CaseEvaluationPermissions.InternalUsers.Edit,
    ];

    private static readonly IReadOnlyDictionary<string, Func<IServiceProvider, Task>> Calls =
        new Dictionary<string, Func<IServiceProvider, Task>>(StringComparer.Ordinal)
        {
            ["Create"] = sp => Service(sp).CreateAsync(new CreateInternalUserDto
            {
                Email = "TEST-probe@example.test",
                FirstName = "TEST",
                LastName = "Probe",
                RoleName = "TEST-not-a-creatable-role",
            }),
            ["GetInternalUsers"] = sp => Service(sp).GetInternalUsersAsync(new GetInternalUsersInput { MaxResultCount = 10 }),
            ["SendPasswordResetEmail"] = sp => Service(sp).SendPasswordResetEmailAsync(Guid.Parse("00000000-0000-0000-0000-0000000000fe")),
            ["GetTenantOptions"] = sp => Service(sp).GetTenantOptionsAsync(null),
        };

    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static bool _seeded;

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public InternalUsersHostOnlyAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    public static TheoryData<string, string> CallsByOfficeRole()
    {
        var data = new TheoryData<string, string>();
        foreach (var call in Calls.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            foreach (var role in OfficeRolesThatHeldThem)
            {
                data.Add(call, role);
            }
        }
        return data;
    }

    public static TheoryData<string, string> CallsByHostRole()
    {
        var data = new TheoryData<string, string>();
        foreach (var call in Calls.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            data.Add(call, InternalUserRoleDataSeedContributor.ItAdminRoleName);
            data.Add(call, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(CallsByOfficeRole))]
    public async Task Is_refused_by_the_attribute_for_an_office_caller_holding_the_old_grants(string call, string officeRole)
    {
        var fixture = await GetFixtureAsync();
        await SeedAsync(fixture.Office.OfficeId);

        var outcome = await InvokeAsync(fixture.Office.OfficeId, officeRole, Calls[call]);

        IsInterceptorRefusal(outcome).ShouldBeTrue(
            $"The office {officeRole} must be refused by the authorization interceptor on {call}. " +
            $"Got: {Describe(outcome)}.");
    }

    /// <summary>
    /// Positive control. Without it, a harness that refused everyone would make every refusal above
    /// pass.
    /// </summary>
    [Theory]
    [MemberData(nameof(CallsByHostRole))]
    public async Task Is_not_refused_by_the_attribute_for_a_host_operator(string call, string hostRole)
    {
        var fixture = await GetFixtureAsync();
        await SeedAsync(fixture.Office.OfficeId);

        var outcome = await InvokeAsync(null, hostRole, Calls[call]);

        IsInterceptorRefusal(outcome).ShouldBeFalse(
            $"The host {hostRole} runs the internal-users screen and must get past the interceptor on " +
            $"{call}. Got: {Describe(outcome)}.");
        (outcome is AbpAuthorizationException).ShouldBeFalse(
            $"The host {hostRole} must not be refused by the method body either. Got: {Describe(outcome)}.");
    }

    // ------------------------------------------------------------------------

    private static IInternalUsersAppService Service(IServiceProvider sp) =>
        sp.GetRequiredService<IInternalUsersAppService>();

    private static ClaimsPrincipal Principal(string role, Guid? officeId)
    {
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, $"test-user-{userId:N}"),
            new(AbpClaimTypes.Role, role),
        };
        if (officeId.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, officeId.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static bool IsInterceptorRefusal(Exception? outcome) =>
        outcome is AbpAuthorizationException refusal
        && refusal.Code != null
        && refusal.Code.StartsWith(InterceptorCodePrefix, StringComparison.Ordinal);

    private static string Describe(Exception? outcome) =>
        outcome == null
            ? "no exception"
            : outcome.GetType().Name + " (code " + (outcome as AbpAuthorizationException)?.Code + ")";

    /// <summary>
    /// Runs the call as <paramref name="role"/> in <paramref name="scope"/>, returning what it threw.
    ///
    /// <para>An office caller carries its office id as a tenant claim, as every office session's token
    /// does. That claim is load-bearing: ABP takes the permission side from the principal's tenant
    /// claim and falls back to the current office only when there is none, so a principal without it
    /// is judged as a HOST caller, a Host-only permission passes the side check, and the office's old
    /// grant rows then admit it. (Measured: without the claim, the interceptor admitted all eight
    /// office cases and only the method-body guard refused them.)</para>
    /// </summary>
    private async Task<Exception?> InvokeAsync(Guid? scope, string role, Func<IServiceProvider, Task> call)
    {
        Exception? caught = null;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(scope))
            using (_principalAccessor.Change(Principal(role, scope)))
            {
                try
                {
                    await call(ServiceProvider);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
        }, requiresNew: true);

        return caught;
    }

    /// <summary>
    /// Once per process: the production host role pass, and the office rows a deployed office still
    /// holds for the two office roles.
    /// </summary>
    private async Task SeedAsync(Guid officeId)
    {
        if (_seeded)
        {
            return;
        }

        await SeedLock.WaitAsync();
        try
        {
            if (_seeded)
            {
                return;
            }

            await WithUnitOfWorkAsync(
                () => GetRequiredService<InternalUserRoleDataSeedContributor>().SeedAsync(new DataSeedContext(null)),
                requiresNew: true);

            await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(officeId))
                {
                    var grants = GetRequiredService<IPermissionGrantRepository>();
                    foreach (var role in OfficeRolesThatHeldThem)
                    {
                        foreach (var permission in Permissions)
                        {
                            await grants.InsertAsync(
                                new PermissionGrant(Guid.NewGuid(), permission, RoleProviderName, role, officeId),
                                autoSave: true);
                        }
                    }
                }
            }, requiresNew: true);

            _seeded = true;
        }
        finally
        {
            SeedLock.Release();
        }
    }
}
