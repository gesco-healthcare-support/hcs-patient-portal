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
