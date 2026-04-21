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
/// Seeds the three IdentityUsers + roles that every Tier-1 entity FKs into.
/// Closes the PR-0 gap identified while preparing PR-1C (Patients): without these
/// users, no Patient / ApplicantAttorney / AppointmentAccessor seed can satisfy
/// the required IdentityUserId FK, which is why earlier PRs were forced to
/// validation-only "Wave 1" coverage.
///
/// Invoked by <see cref="CaseEvaluationIntegrationTestSeedContributor"/> as its
/// first seed call (FK ordering). Not registered as an IDataSeedContributor itself
/// so ABP's non-deterministic contributor ordering cannot interleave it with the
/// orchestrator. Mirrors the production pattern in
/// HealthcareSupport.CaseEvaluation.Doctors.DoctorTenantAppService.CreateDoctorUserAsync.
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

        using (_currentTenant.Change(null))
        {
            await EnsureRoleAsync(IdentityUsersTestData.AdminRoleName);
            await EnsureRoleAsync(IdentityUsersTestData.AttorneyRoleName);
            await EnsureRoleAsync(IdentityUsersTestData.PatientRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.StaffAdminId,
                IdentityUsersTestData.StaffAdminUserName,
                IdentityUsersTestData.StaffAdminEmail,
                IdentityUsersTestData.AdminRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.AttorneyUserId,
                IdentityUsersTestData.AttorneyUserName,
                IdentityUsersTestData.AttorneyEmail,
                IdentityUsersTestData.AttorneyRoleName);

            await SeedUserAsync(
                IdentityUsersTestData.PatientUserId,
                IdentityUsersTestData.PatientUserName,
                IdentityUsersTestData.PatientEmail,
                IdentityUsersTestData.PatientRoleName);
        }

        _isSeeded = true;
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
                $"Failed to seed role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
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
                $"Failed to seed user '{userName}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var roleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign role '{roleName}' to user '{userName}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
        }
    }
}
