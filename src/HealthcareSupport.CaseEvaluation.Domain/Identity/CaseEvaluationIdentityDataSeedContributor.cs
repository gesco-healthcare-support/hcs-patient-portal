using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Saas;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// ABP's identity seed, except that the synthetic TEST office (<see cref="OfficeSeedData"/>) gets
/// NO admin user outside Development.
///
/// <para>WHY: ABP's contributor falls back to a publicly known default password whenever the seed
/// context carries none, and the TEST office is seeded in every environment, production included.
/// Its admin would therefore be a known credential on a live system. The host admin invites the TEST
/// office's staff through the UI instead, like any office. In Development the admin is kept so a
/// fresh local stack can still sign in to the office.</para>
///
/// <para>Replaces the framework contributor in DI. The data seeder resolves each contributor by
/// type, so the framework's own <see cref="IdentityDataSeedContributor"/> entry now resolves to this
/// class, and CaseEvaluationDomainModule removes this type's own entry so it runs once.</para>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IdentityDataSeedContributor), typeof(CaseEvaluationIdentityDataSeedContributor))]
public class CaseEvaluationIdentityDataSeedContributor : IdentityDataSeedContributor
{
    private readonly ITenantStore _tenantStore;
    private readonly IAbpHostEnvironment _environment;
    private readonly ILogger<CaseEvaluationIdentityDataSeedContributor> _logger;

    public CaseEvaluationIdentityDataSeedContributor(
        IIdentityDataSeeder identityDataSeeder,
        ITenantStore tenantStore,
        IAbpHostEnvironment environment,
        ILogger<CaseEvaluationIdentityDataSeedContributor> logger)
        : base(identityDataSeeder)
    {
        _tenantStore = tenantStore;
        _environment = environment;
        _logger = logger;
    }

    public override async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId != null && !_environment.IsDevelopment())
        {
            var tenant = await _tenantStore.FindAsync(context.TenantId.Value);
            if (OfficeSeedData.FindByTenantName(tenant?.Name) != null)
            {
                _logger.LogInformation(
                    "Identity seed: no admin user for the synthetic office {OfficeId} outside Development.",
                    context.TenantId);
                return;
            }
        }

        await base.SeedAsync(context!);
    }
}
