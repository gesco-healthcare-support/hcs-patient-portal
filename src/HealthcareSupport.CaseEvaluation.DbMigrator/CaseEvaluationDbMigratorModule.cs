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
        var configuration = context.Services.GetConfiguration();

        // GetHostingEnvironment() is the ASP.NET Core helper and returns IWebHostEnvironment; this
        // is a console host, so it is not available here. GetAbpHostEnvironment() is ABP's own and
        // works in any host, and reports the same environment name the seeders read.
        var isDevelopment = string.Equals(
            context.Services.GetAbpHostEnvironment().EnvironmentName,
            Microsoft.Extensions.Hosting.Environments.Development,
            System.StringComparison.OrdinalIgnoreCase);

        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "CaseEvaluation:"; });

        // B12: the admin-password store, and the startup gate that refuses a host configuring
        // neither or both. Registered in both this process and the other one that can create a
        // database, so neither can seed a published default.
        EntityFrameworkCore.AdminPasswords.AdminPasswordStoreRegistrar.Register(
            context.Services, configuration, isDevelopment);

        // DbMigrator is a one-shot console host -- never drain the background-jobs queue.
        // Mirrors the AuthServer pattern (CaseEvaluationAuthServerModule.cs:198-201).
        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = false;
        });
    }
}
