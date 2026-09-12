using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// task_2e8e4dc2 (2026-07-21) -- verifies the two per-office building blocks that let a host
/// operator switch into an office as their OWN shadow user (not the shared office admin):
///  1. the re-introduced per-tenant Staff Supervisor role is seeded with its operational grants
///     (and is narrower than admin -- no framework powers);
///  2. the generalized shadow provisioner creates a per-office shadow holding a caller-specified
///     role (here Staff Supervisor).
/// The grant wiring itself (AuthServer) is exercised by the live E2E -- it has no test harness.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeImpersonationRoleTests : CaseEvaluationMultiOfficeTestBase
{
    // Matches RolePermissionValueProvider.ProviderName ("R").
    private const string RoleProviderName = "R";

    private readonly InternalUserRoleDataSeedContributor _roleSeeder;
    private readonly IIntakeShadowUserProvisioner _shadowProvisioner;
    private readonly IdentityRoleManager _roleManager;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionManager _permissionManager;
    private readonly ICurrentTenant _currentTenant;

    public MultiOfficeImpersonationRoleTests()
    {
        _roleSeeder = GetRequiredService<InternalUserRoleDataSeedContributor>();
        _shadowProvisioner = GetRequiredService<IIntakeShadowUserProvisioner>();
        _roleManager = GetRequiredService<IdentityRoleManager>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _permissionManager = GetRequiredService<IPermissionManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task TenantSeed_ReintroducesStaffSupervisorRole_WithOperationalGrantsButNotFrameworkPowers()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var role = await _roleManager.FindByNameAsync(
                    InternalUserRoleDataSeedContributor.StaffSupervisorRoleName);
                role.ShouldNotBeNull();
                role!.TenantId.ShouldBe(officeA.OfficeId);

                // Holds the top-tenant operational grants (soft-delete on operational entities +
                // the tenant dashboard).
                (await IsGrantedAsync("CaseEvaluation.Dashboard.Tenant")).ShouldBeTrue();
                (await IsGrantedAsync("CaseEvaluation.Appointments.Delete")).ShouldBeTrue();
                (await IsGrantedAsync("CaseEvaluation.InternalUsers.Create")).ShouldBeTrue();

                // Narrower than the tenant admin role: NO framework powers.
                (await IsGrantedAsync("AbpIdentity.Roles")).ShouldBeFalse();
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task EnsureShadowUser_WithSupervisorRole_ProvisionsOwnShadowHoldingThatRole()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        var operatorEmail = "supervisor.shadow.test@hcs.test";
        var operatorId = await EnsureHostOperatorAsync(operatorEmail);

        // Provision the operator's own shadow in the office, holding the Staff Supervisor role.
        await WithUnitOfWorkAsync(
            () => _shadowProvisioner.EnsureShadowUserAsync(
                officeA.OfficeId, operatorId, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName),
            requiresNew: true);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var shadow = await _userManager.FindByEmailAsync(operatorEmail);
                shadow.ShouldNotBeNull();
                shadow!.TenantId.ShouldBe(officeA.OfficeId);
                shadow.IsActive.ShouldBeTrue();
                (await _userManager.IsInRoleAsync(
                    shadow, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)).ShouldBeTrue();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Regression, 2026-08-22. A Staff Supervisor pressing "Switch into practice" got a 403 from
    /// <c>/connect/token</c> carrying <c>Volo.Abp.Identity:DuplicateUserName</c>, and could not enter
    /// the office at all.
    ///
    /// <para>Cause: the office already held a shadow whose USERNAME was the operator's host address but
    /// whose EMAIL was not. The provisioner looked the shadow up by email only, missed it, and fell
    /// through to create a user whose username was already taken.</para>
    ///
    /// <para><b>Correction (2026-08-22).</b> This docstring originally blamed the pre-Phase-D per-tenant
    /// seed for that divergence. It did not cause it -- the seed writes the same value to both fields.
    /// The rows in question had been repointed by hand in a local database. The defect the test pins is
    /// real either way, and the revoke half of it (below) is the dangerous one.</para>
    ///
    /// <para>This test reproduces that divergence exactly, then asserts the provisioner ADOPTS the
    /// existing row rather than throwing. If the lookup regresses to email-only, the
    /// <c>EnsureShadowUserAsync</c> call throws and this test fails.</para>
    /// </summary>
    [Fact]
    public async Task EnsureShadowUser_WhenExistingShadowEmailDivergedFromUserName_AdoptsItInsteadOfThrowing()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        var operatorEmail = "diverged.shadow.test@hcs.test";
        var operatorId = await EnsureHostOperatorAsync(operatorEmail);

        // The legacy row: username IS the operator's host address, email is something else entirely.
        var legacyShadowId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var legacy = new IdentityUser(
                    legacyShadowId,
                    userName: operatorEmail,
                    email: "diverged.shadow.test@example.test",
                    tenantId: officeA.OfficeId);
                (await _userManager.CreateAsync(legacy, "1q2w3E*r")).Succeeded.ShouldBeTrue();
            }
        }, requiresNew: true);

        var resolvedId = Guid.Empty;
        await WithUnitOfWorkAsync(
            async () => resolvedId = await _shadowProvisioner.EnsureShadowUserAsync(
                officeA.OfficeId, operatorId, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName),
            requiresNew: true);

        // Adopted, not duplicated: the same row comes back, now carrying the requested role.
        resolvedId.ShouldBe(legacyShadowId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var shadow = await _userManager.FindByIdAsync(legacyShadowId.ToString());
                shadow.ShouldNotBeNull();
                shadow!.IsActive.ShouldBeTrue();
                (await _userManager.IsInRoleAsync(
                    shadow, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)).ShouldBeTrue();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// The same divergence, on the revoke path. This one failed SILENTLY rather than loudly:
    /// <c>DisableShadowUserAsync</c> looked up by email, found nothing, and returned without doing
    /// anything -- so unassigning an operator from an office left their shadow ACTIVE and their access
    /// intact. A revoke that reports success while granting continued access is the worse of the two
    /// bugs, which is why it gets its own test.
    /// </summary>
    [Fact]
    public async Task DisableShadowUser_WhenExistingShadowEmailDivergedFromUserName_ActuallyDeactivatesIt()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        var operatorEmail = "diverged.revoke.test@hcs.test";
        var operatorId = await EnsureHostOperatorAsync(operatorEmail);

        var legacyShadowId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var legacy = new IdentityUser(
                    legacyShadowId,
                    userName: operatorEmail,
                    email: "diverged.revoke.test@example.test",
                    tenantId: officeA.OfficeId);
                (await _userManager.CreateAsync(legacy, "1q2w3E*r")).Succeeded.ShouldBeTrue();
            }
        }, requiresNew: true);

        await WithUnitOfWorkAsync(
            () => _shadowProvisioner.DisableShadowUserAsync(officeA.OfficeId, operatorId),
            requiresNew: true);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var shadow = await _userManager.FindByIdAsync(legacyShadowId.ToString());
                shadow.ShouldNotBeNull();
                shadow!.IsActive.ShouldBeFalse();
            }
        }, requiresNew: true);
    }

    private Task SeedTenantRolesAsync(Guid officeId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                await _roleSeeder.SeedAsync(new DataSeedContext(officeId));
            }
        }, requiresNew: true);

    private async Task<Guid> EnsureHostOperatorAsync(string email)
    {
        var operatorId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var existing = await _userManager.FindByEmailAsync(email);
                if (existing != null)
                {
                    operatorId = existing.Id;
                    return;
                }
                var op = new IdentityUser(Guid.NewGuid(), email, email, tenantId: null)
                {
                    Name = "Supervisor",
                    Surname = "Shadow",
                };
                var createResult = await _userManager.CreateAsync(op, "1q2w3E*r");
                createResult.Succeeded.ShouldBeTrue();
                operatorId = op.Id;
            }
        }, requiresNew: true);
        return operatorId;
    }

    private async Task<bool> IsGrantedAsync(string permission)
    {
        var result = await _permissionManager.GetAsync(
            permission, RoleProviderName, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName);
        return result.IsGranted;
    }
}
