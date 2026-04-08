using Volo.Abp.Modularity;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(CaseEvaluationDomainModule),
    typeof(CaseEvaluationTestBaseModule)
)]
public class CaseEvaluationDomainTestModule : AbpModule
{

}
