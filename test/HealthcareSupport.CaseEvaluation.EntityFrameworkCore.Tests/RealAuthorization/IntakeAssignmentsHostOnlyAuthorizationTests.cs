using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// Intake-assignment management is host-only. The six methods read or change which host operator
/// may enter which office, and list every office, so a caller inside an office is refused while the
/// host operators who run the screen are admitted.
///
/// <para>THE OFFICE CALLER HOLDS THE GRANT AN OFFICE ADMIN HAD BEFORE THIS CHANGE. ABP grants every
/// permission an office can hold to that office's static <c>admin</c> role, so a deployed office
/// carries a role row for the manage permission. It is written straight into the permission-grant
/// table, because the permission manager refuses to grant a Host-only permission inside an office;
/// these tests therefore also show that the existing row opens nothing.</para>
///
/// <para>THE HOST CALLERS HOLD THE GRANT SETS PRODUCTION SHIPS. The host pass of
/// <see cref="InternalUserRoleDataSeedContributor"/> runs once for the process, so an admitted call
/// proves the real IT Admin and Staff Supervisor grants reach the screen.</para>
///
/// <para>EVERY TEST SEEDS ITS OWN OPERATORS AND ASSIGNMENT. A refusal is re-read against rows it
/// must not have touched; shared rows would let one test's outcome leak into another's.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class IntakeAssignmentsHostOnlyAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string OfficeAdminRoleName = "admin";
    private const string RoleProviderName = "R";

    private static readonly SemaphoreSlim HostRolesLock = new(1, 1);
    private static bool _hostRolesSeeded;

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public IntakeAssignmentsHostOnlyAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ---- An office caller is refused, and nothing it named changes ----

    [Fact]
    public async Task OfficeOptions_AreRefused_ForAnOfficeCaller()
    {
        await SeedAsync();

        await AssertRefusedInOfficeAsync(sp => sp.GetRequiredService<IIntakeAssignmentsAppService>().GetOfficeOptionsAsync());
    }

    [Fact]
    public async Task AssignableOperators_AreRefused_ForAnOfficeCaller()
    {
        await SeedAsync();

        await AssertRefusedInOfficeAsync(sp => sp.GetRequiredService<IIntakeAssignmentsAppService>().GetAssignableOperatorsAsync());
    }

    [Fact]
    public async Task Assign_IsRefused_ForAnOfficeCaller_AndNothingIsAssignedOrProvisioned()
    {
        var seeded = await SeedAsync();

        await AssertRefusedInOfficeAsync(sp => sp.GetRequiredService<IIntakeAssignmentsAppService>()
            .AssignAsync(new AssignIntakeOfficeDto { OperatorUserId = seeded.OperatorB.Id, OfficeId = seeded.OfficeId }));

        (await AssignmentCountAsync(seeded.OperatorB.Id)).ShouldBe(0);
        (await ShadowExistsAsync(seeded.OfficeId, seeded.OperatorB.Email!)).ShouldBeFalse();
    }

    [Fact]
    public async Task Unassign_IsRefused_ForAnOfficeCaller_AndTheAssignmentRemains()
    {
        var seeded = await SeedAsync();

        await AssertRefusedInOfficeAsync(sp => sp.GetRequiredService<IIntakeAssignmentsAppService>()
            .UnassignAsync(seeded.OperatorA.Id, seeded.OfficeId));

        (await AssignmentCountAsync(seeded.OperatorA.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task AssignmentList_IsRefused_ForAnOfficeCaller()
    {
        await SeedAsync();

        await AssertRefusedInOfficeAsync(sp => sp.GetRequiredService<IIntakeAssignmentsAppService>().GetListAsync());
    }

    [Fact]
    public async Task AssignmentPagedList_IsRefused_ForAnOfficeCaller()
    {
        var seeded = await SeedAsync();

        await AssertRefusedInOfficeAsync(sp => sp.GetRequiredService<IIntakeAssignmentsAppService>()
            .GetPagedListAsync(new GetIntakeAssignmentsInput { Filter = seeded.Label, MaxResultCount = 10 }));
    }

    // ---- Positive controls: the host operators who run the screen are admitted. Without these, a
    // harness that refused everyone would make every refusal above pass. ----

    [Theory]
    [InlineData(InternalUserRoleDataSeedContributor.ItAdminRoleName)]
    [InlineData(InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)]
    public async Task OfficeOptions_AreAdmitted_ForAHostOperator_AndListTheOffice(string role)
    {
        var seeded = await SeedAsync();

        var offices = await RunAsHostAsync(role, sp => sp.GetRequiredService<IIntakeAssignmentsAppService>().GetOfficeOptionsAsync());

        offices.Items.Where(o => o.Id == seeded.OfficeId).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(InternalUserRoleDataSeedContributor.ItAdminRoleName)]
    [InlineData(InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)]
    public async Task AssignableOperators_AreAdmitted_ForAHostOperator_AndListTheOperator(string role)
    {
        var seeded = await SeedAsync();

        var operators = await RunAsHostAsync(role, sp => sp.GetRequiredService<IIntakeAssignmentsAppService>().GetAssignableOperatorsAsync());

        operators.Items.Where(o => o.Id == seeded.OperatorA.Id).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(InternalUserRoleDataSeedContributor.ItAdminRoleName)]
    [InlineData(InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)]
    public async Task AssignmentLists_AreAdmitted_ForAHostOperator_AndShowTheAssignment(string role)
    {
        var seeded = await SeedAsync();

        var list = await RunAsHostAsync(role, sp => sp.GetRequiredService<IIntakeAssignmentsAppService>().GetListAsync());
        var paged = await RunAsHostAsync(role, sp => sp.GetRequiredService<IIntakeAssignmentsAppService>()
            .GetPagedListAsync(new GetIntakeAssignmentsInput { Filter = seeded.Label, MaxResultCount = 10 }));

        list.Items.Where(r => r.OperatorUserId == seeded.OperatorA.Id && r.OfficeId == seeded.OfficeId).ShouldHaveSingleItem();
        paged.Items.Where(r => r.OperatorUserId == seeded.OperatorA.Id && r.OfficeId == seeded.OfficeId).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(InternalUserRoleDataSeedContributor.ItAdminRoleName)]
    [InlineData(InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)]
    public async Task Assign_IsAdmitted_ForAHostOperator(string role)
    {
        var seeded = await SeedAsync();

        // An unknown operator: an admitted call reaches the body and fails on the lookup; a refused
        // one would throw AbpAuthorizationException instead. Nothing is assigned either way.
        await Should.ThrowAsync<BusinessException>(() => RunAsHostAsync(role, async sp =>
        {
            await sp.GetRequiredService<IIntakeAssignmentsAppService>()
                .AssignAsync(new AssignIntakeOfficeDto { OperatorUserId = Guid.NewGuid(), OfficeId = seeded.OfficeId });
            return true;
        }));
    }

    [Theory]
    [InlineData(InternalUserRoleDataSeedContributor.ItAdminRoleName)]
    [InlineData(InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)]
    public async Task Unassign_IsAdmitted_ForAHostOperator(string role)
    {
        var seeded = await SeedAsync();

        // An unknown operator: the body finds no row, then fails resolving the operator for the shadow
        // user; a refused call would throw AbpAuthorizationException instead.
        await Should.ThrowAsync<BusinessException>(() => RunAsHostAsync(role, async sp =>
        {
            await sp.GetRequiredService<IIntakeAssignmentsAppService>().UnassignAsync(Guid.NewGuid(), seeded.OfficeId);
            return true;
        }));
    }

    // ------------------------------------------------------------------------

    private async Task AssertRefusedInOfficeAsync(Func<IServiceProvider, Task> call)
    {
        var fixture = await GetFixtureAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), OfficeAdminRoleName))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(() => call(ServiceProvider));
            }
        }, requiresNew: true);
    }

    private Task<T> RunAsHostAsync<T>(string role, Func<IServiceProvider, Task<T>> call) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), role))
            {
                return await call(ServiceProvider);
            }
        }, requiresNew: true);

    private Task<int> AssignmentCountAsync(Guid operatorId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return await GetRequiredService<IRepository<IntakeOfficeAssignment, Guid>>()
                    .CountAsync(a => a.OperatorUserId == operatorId);
            }
        }, requiresNew: true);

    private Task<bool> ShadowExistsAsync(Guid officeId, string email) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                return await GetRequiredService<IdentityUserManager>().FindByEmailAsync(email) != null;
            }
        }, requiresNew: true);

    /// <summary>
    /// Per test: two host Intake operators and one assignment of operator A to the shared office.
    /// Once per process: the production host role pass, and the office admin's manage-permission row.
    /// </summary>
    private async Task<Seeded> SeedAsync()
    {
        var fixture = await GetFixtureAsync();
        var officeId = fixture.Office.OfficeId;
        await EnsureHostRolesAndOfficeAdminRowAsync(officeId);

        var label = Guid.NewGuid().ToString("N")[..8];
        var operatorA = await HostIntakeOperatorAsync("TEST-intake-a", label);
        var operatorB = await HostIntakeOperatorAsync("TEST-intake-b", label);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                await GetRequiredService<IRepository<IntakeOfficeAssignment, Guid>>().InsertAsync(
                    new IntakeOfficeAssignment(Guid.NewGuid(), operatorA.Id, officeId), autoSave: true);
            }
        }, requiresNew: true);

        return new Seeded(officeId, label, operatorA, operatorB);
    }

    private async Task EnsureHostRolesAndOfficeAdminRowAsync(Guid officeId)
    {
        if (_hostRolesSeeded)
        {
            return;
        }

        await HostRolesLock.WaitAsync();
        try
        {
            if (_hostRolesSeeded)
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
                    await GetRequiredService<IPermissionGrantRepository>().InsertAsync(
                        new PermissionGrant(Guid.NewGuid(), CaseEvaluationPermissions.IntakeAssignments.Manage,
                            RoleProviderName, OfficeAdminRoleName, officeId),
                        autoSave: true);
                }
            }, requiresNew: true);

            _hostRolesSeeded = true;
        }
        finally
        {
            HostRolesLock.Release();
        }
    }

    private Task<IdentityUser> HostIntakeOperatorAsync(string name, string label) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var email = $"{name.ToLowerInvariant()}.{label}@example.test";
                var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId: null) { Name = name, Surname = label };
                (await users.CreateAsync(user, "Synthetic-Passw0rd!")).Succeeded.ShouldBeTrue();
                (await users.AddToRoleAsync(user, InternalUserRoleDataSeedContributor.IntakeStaffRoleName)).Succeeded.ShouldBeTrue();
                return user;
            }
        }, requiresNew: true);

    private sealed record Seeded(Guid OfficeId, string Label, IdentityUser OperatorA, IdentityUser OperatorB);
}
