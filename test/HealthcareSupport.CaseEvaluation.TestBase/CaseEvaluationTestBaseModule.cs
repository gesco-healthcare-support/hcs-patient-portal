using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpBackgroundJobsAbstractionsModule)
)]
public class CaseEvaluationTestBaseModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = false;
        });

        context.Services.AddAlwaysAllowAuthorization();

        // The synthetic TEST office is registered by OfficeSeedDataContributor in every real
        // environment, but the rigs seed their own offices (TenantA/TenantB) and have no office-database
        // connection template, so that contributor cannot run in the start-up seed. TestOfficeSeedTests
        // exercises it directly, over a fake connection-string provider.
        Configure<AbpDataSeedOptions>(options =>
        {
            options.Contributors.RemoveAll(type => type == typeof(Saas.OfficeSeedDataContributor));
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // A database copied from the seeded template already holds every seeded row; seeding it
        // again would duplicate them (#1033).
        if (context.ServiceProvider.GetRequiredService<IOptions<CaseEvaluationTestSeedOptions>>().Value.SkipInitialSeed)
        {
            return;
        }

        SeedTestData(context);
    }

    private static void SeedTestData(ApplicationInitializationContext context)
    {
        AsyncHelper.RunSync(async () =>
        {
            using (var scope = context.ServiceProvider.CreateScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<IDataSeeder>()
                    .SeedAsync();
            }
        });
    }
}
