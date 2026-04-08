using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Identity;

public class ExternalUserRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;

    public ExternalUserRoleDataSeedContributor(IdentityRoleManager roleManager, ICurrentTenant currentTenant)
    {
        _roleManager = roleManager;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        using (_currentTenant.Change(context?.TenantId))
        {
            await EnsureRoleAsync("Patient");
            await EnsureRoleAsync("Claim Examiner");
            await EnsureRoleAsync("Applicant Attorney");
            await EnsureRoleAsync("Defense Attorney");
        }
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        var existingRole = await _roleManager.FindByNameAsync(roleName);
        if (existingRole != null)
        {
            return;
        }

        var role = new IdentityRole(Guid.NewGuid(), roleName, _currentTenant.Id);
        await _roleManager.CreateAsync(role);
    }
}
