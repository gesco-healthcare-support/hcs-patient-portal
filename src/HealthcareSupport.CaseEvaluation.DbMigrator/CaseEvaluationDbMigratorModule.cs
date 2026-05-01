using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace HealthcareSupport.CaseEvaluation.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(CaseEvaluationEntityFrameworkCoreModule),
    typeof(CaseEvaluationApplicationContractsModule)
)]
public class CaseEvaluationDbMigratorModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        if (Program.DisableRedis)
        {
            var configuration = context.Services.GetConfiguration();
            configuration["Redis:IsEnabled"] = "false";
        }

        base.PreConfigureServices(context);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "CaseEvaluation:"; });

        // DbMigrator is a one-shot console host -- never drain the background-jobs queue.
        // Mirrors the AuthServer pattern (CaseEvaluationAuthServerModule.cs:198-201).
        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = false;
        });
    }
}
