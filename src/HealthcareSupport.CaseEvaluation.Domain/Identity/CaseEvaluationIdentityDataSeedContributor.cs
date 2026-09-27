using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Saas;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// ABP's identity seed, except that the synthetic TEST office (<see cref="OfficeSeedData"/>) gets
/// its admin ROLE but NO admin USER outside Development.
///
/// <para>WHY no user: ABP's contributor falls back to a publicly known default password whenever the seed
/// context carries none, and the TEST office is seeded in every environment, production included.
/// Its admin would therefore be a known credential on a live system. The host admin invites the TEST
/// office's staff through the UI instead, like any office. In Development the admin is kept so a
/// fresh local stack can still sign in to the office.</para>
///
/// <para>WHY the role anyway: an IT Admin who switches into an office lands as their own shadow user
/// holding that office's <c>admin</c> role (<c>HostIntakeImpersonationExtensionGrant</c>), which fails
/// when the role does not exist. ABP creates the role only together with the admin user, so it is
/// created here the same way. Its permission grants need nothing extra: ABP's
/// <c>PermissionDataSeedContributor</c> grants them to the role NAME <c>admin</c> in every office.</para>
///
/// <para>Replaces the framework contributor in DI. The data seeder resolves each contributor by
/// type, so the framework's own <see cref="IdentityDataSeedContributor"/> entry now resolves to this
/// class, and CaseEvaluationDomainModule removes this type's own entry so it runs once.</para>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IdentityDataSeedContributor), typeof(CaseEvaluationIdentityDataSeedContributor))]
public class CaseEvaluationIdentityDataSeedContributor : IdentityDataSeedContributor
{
    /// <summary>
    /// ABP's per-office administrator role. ABP names it only in local constants
    /// (<c>IdentityDataSeeder</c>, <c>PermissionDataSeedContributor</c>), so it is named once here.
    /// </summary>
    public const string AdminRoleName = "admin";

    private readonly ITenantStore _tenantStore;
    private readonly IAbpHostEnvironment _environment;
    private readonly IdentityRoleManager _roleManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<CaseEvaluationIdentityDataSeedContributor> _logger;

    public CaseEvaluationIdentityDataSeedContributor(
        IIdentityDataSeeder identityDataSeeder,
        ITenantStore tenantStore,
        IAbpHostEnvironment environment,
        IdentityRoleManager roleManager,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        ILogger<CaseEvaluationIdentityDataSeedContributor> logger)
        : base(identityDataSeeder)
    {
        _tenantStore = tenantStore;
        _environment = environment;
        _roleManager = roleManager;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public override async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId != null && !_environment.IsDevelopment())
        {
            var tenant = await _tenantStore.FindAsync(context.TenantId.Value);
            if (OfficeSeedData.FindByTenantName(tenant?.Name) != null)
            {
                await SeedAdminRoleOnlyAsync(context.TenantId.Value);
                return;
            }
        }

        await base.SeedAsync(context!);
    }

    /// <summary>
    /// Creates the office's admin role the way ABP 10.0.2's <c>IdentityDataSeeder</c> does
    /// (IdentityDataSeeder.cs:85-103): only when missing, static and public, owned by the office.
    /// ABP's call to load the dynamic identity options is not mirrored; those options govern users
    /// (passwords, lockout, sign-in), and no user is created here.
    /// </summary>
    private async Task SeedAdminRoleOnlyAsync(Guid officeId)
    {
        using (_currentTenant.Change(officeId))
        {
            if (await _roleManager.FindByNameAsync(AdminRoleName) == null)
            {
                var adminRole = new IdentityRole(_guidGenerator.Create(), AdminRoleName, officeId)
                {
                    IsStatic = true,
                    IsPublic = true
                };
                (await _roleManager.CreateAsync(adminRole)).CheckErrors();
            }
        }

        _logger.LogInformation(
            "Identity seed: the synthetic office {OfficeId} gets its admin role but no admin user outside Development.",
            officeId);
    }
}
