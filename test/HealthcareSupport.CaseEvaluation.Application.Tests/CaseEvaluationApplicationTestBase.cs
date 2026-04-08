using Volo.Abp.Modularity;

namespace HealthcareSupport.CaseEvaluation;

public abstract class CaseEvaluationApplicationTestBase<TStartupModule> : CaseEvaluationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
