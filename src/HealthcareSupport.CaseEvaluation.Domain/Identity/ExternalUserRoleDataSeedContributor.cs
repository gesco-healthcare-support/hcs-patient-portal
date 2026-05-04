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
            // Role-naming reconciliation 2026-05-04 -- OLD has 4 external
            // roles total (verified at
            // P:\PatientPortalOld\PatientAppointment.Models\Enums\Roles.cs):
            //   OLD Patient         = 4  -> NEW Patient
            //   OLD Adjuster        = 5  -> NEW Claim Examiner
            //   OLD PatientAttorney = 6  -> NEW Applicant Attorney
            //   OLD DefenseAttorney = 7  -> NEW Defense Attorney
            // "Adjuster" and "Claim Examiner" are the SAME role (NEW
            // renamed for clarity to align with the
            // AppointmentClaimExaminer entity name). Earlier audit
            // mistakenly listed "Adjuster" as a fifth role; reconciled.
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
