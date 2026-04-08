using Volo.Abp.Modularity;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(CaseEvaluationApplicationModule),
    typeof(CaseEvaluationDomainTestModule)
)]
public class CaseEvaluationApplicationTestModule : AbpModule
{

}
