using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(CaseEvaluationDomainModule),
    typeof(CaseEvaluationTestBaseModule)
)]
public class CaseEvaluationDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The shared rig keeps every office in ONE database and stores no office rows, so the
        // production resolver (Data.OfficeConnectionStringResolver) would refuse every office request
        // here -- correctly, since in production such an office has no database. Put ABP's stock
        // resolver back for this rig only; the Application and EF test modules inherit it.
        //
        // The production resolver is still exercised: by OfficeConnectionStringResolverTests (unit),
        // by the MultiOffice and RealAuthorization harnesses (one database per office, real resolver),
        // and by the real-SQL container test. CaseEvaluationMultiOfficeTestBase asserts that the
        // real resolver is what an application WITHOUT this module gets.
        context.Services.Replace(
            ServiceDescriptor.Transient<IConnectionStringResolver, MultiTenantConnectionStringResolver>());
    }
}
