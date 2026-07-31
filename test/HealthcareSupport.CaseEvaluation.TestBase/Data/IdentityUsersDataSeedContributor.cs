using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Testing;

/// <summary>
/// Seeds the seven test users across the seven intended user-facing roles
/// (Phase 0.1, 2026-05-01: Doctor is non-user reference entity, not seeded here):
///
///   Host scope: HostAdmin (role "admin").
///   TenantA:    TenantAdmin1, ApplicantAttorney1, DefenseAttorney1, ClaimExaminer1, Patient1.
///   TenantB:    Patient2.
///
/// Mirrors production's tenant-provisioning pattern by wrapping each tenant's user
/// + role creation in _currentTenant.Change(tenantId). Each tenant has its own row
/// per role because role definitions in ABP are tenant-scoped when created inside
/// a tenant context.
///
/// Must run AFTER the orchestrator's SeedTenantsAsync step -- this contributor
/// reads TenantsTestData.TenantARef / TenantBRef which are populated there.
/// </summary>
public class IdentityUsersDataSeedContributor : ISingletonDependency
{
    private bool _isSeeded;
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;

    public IdentityUsersDataSeedContributor(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        ICurrentTenant currentTenant)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (_isSeeded)
        {
            return;
        }

        RequireTenantsSeeded();

        await SeedHostAdminAsync();
        await SeedTenantAUsersAsync();
        await SeedTenantBUsersAsync();

        _isSeeded = true;
    }

    private static void RequireTenantsSeeded()
    {
        if (TenantsTestData.TenantARef == Guid.Empty || TenantsTestData.TenantBRef == Guid.Empty)
        {
            throw new InvalidOperationException(
                "IdentityUsersDataSeedContributor requires TenantsTestData.TenantARef/TenantBRef to be populated. " +
                "The orchestrator must call its SeedTenantsAsync step before invoking this contributor.");
        }
    }

    private async Task SeedHostAdminAsync()
    {
        using (_currentTenant.Change(null))
        {
            await EnsureRoleAsync(IdentityUsersTestData.HostAdminRoleName);
            await SeedUserAsync(
                IdentityUsersTestData.HostAdminId,
                IdentityUsersTestData.HostAdminUserName,
                IdentityUsersTestData.HostAdminEmail,
                IdentityUsersTestData.HostAdminRoleName);
        }
    }

    private async Task SeedTenantAUsersAsync()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await EnsureRoleAsync(IdentityUsersTestData.TenantAdminRoleName);
            await EnsureRoleAsync(IdentityUsersTestData.ApplicantAttorneyRoleName);
            await EnsureRoleAsync(IdentityUsersTestData.DefenseAttorneyRoleName);
            await EnsureRoleAsync(IdentityUsersTestData.ClaimExaminerRoleName);
            await EnsureRoleAsync(IdentityUsersTestData.PatientRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.TenantAdmin1UserId,
                IdentityUsersTestData.TenantAdmin1UserName,
                IdentityUsersTestData.TenantAdmin1Email,
                IdentityUsersTestData.TenantAdminRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.ApplicantAttorney1UserId,
                IdentityUsersTestData.ApplicantAttorney1UserName,
                IdentityUsersTestData.ApplicantAttorney1Email,
                IdentityUsersTestData.ApplicantAttorneyRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.DefenseAttorney1UserId,
                IdentityUsersTestData.DefenseAttorney1UserName,
                IdentityUsersTestData.DefenseAttorney1Email,
                IdentityUsersTestData.DefenseAttorneyRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.ClaimExaminer1UserId,
                IdentityUsersTestData.ClaimExaminer1UserName,
                IdentityUsersTestData.ClaimExaminer1Email,
                IdentityUsersTestData.ClaimExaminerRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.Patient1UserId,
                IdentityUsersTestData.Patient1UserName,
                IdentityUsersTestData.Patient1Email,
                IdentityUsersTestData.PatientRoleName);
        }
    }

    private async Task SeedTenantBUsersAsync()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            await EnsureRoleAsync(IdentityUsersTestData.PatientRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.Patient2UserId,
                IdentityUsersTestData.Patient2UserName,
                IdentityUsersTestData.Patient2Email,
                IdentityUsersTestData.PatientRoleName);
        }
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        var existing = await _roleManager.FindByNameAsync(roleName);
        if (existing != null)
        {
            return;
        }

        var role = new IdentityRole(Guid.NewGuid(), roleName, _currentTenant.Id);
        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed role '{roleName}' in tenant {_currentTenant.Id?.ToString() ?? "(host)"}: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedUserAsync(Guid id, string userName, string email, string roleName)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            return;
        }

        var user = new IdentityUser(id, userName, email, _currentTenant.Id);

        var createResult = await _userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed user '{userName}' in tenant {_currentTenant.Id?.ToString() ?? "(host)"}: " +
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign role '{roleName}' to user '{userName}': " +
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }
    }
}
