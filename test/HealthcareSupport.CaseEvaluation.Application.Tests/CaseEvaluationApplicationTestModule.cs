using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Volo.Abp.Modularity;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(CaseEvaluationApplicationModule),
    typeof(CaseEvaluationDomainTestModule)
)]
public class CaseEvaluationApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // BUG-012 Sub-bug 1 (2026-05-22): ExternalSignupAppService's
        // constructor takes IHostEnvironment (used to gate dev-only
        // MarkEmailConfirmed / DeleteTestUsers helpers). The default
        // ABP test harness does not register it, so resolving the
        // AppService via DI fails with "Cannot resolve parameter
        // IHostEnvironment". Register a stub so the AppService can be
        // instantiated under test. Production composition is unchanged.
        context.Services.AddSingleton<IHostEnvironment>(
            new HostingEnvironment
            {
                EnvironmentName = Environments.Development,
                ApplicationName = "CaseEvaluationTests",
                ContentRootPath = System.IO.Directory.GetCurrentDirectory(),
            });
    }
}
