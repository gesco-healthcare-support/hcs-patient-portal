using Volo.Abp.Modularity;

namespace HealthcareSupport.CaseEvaluation;

/* Inherit from this class for your domain layer tests. */
public abstract class CaseEvaluationDomainTestBase<TStartupModule> : CaseEvaluationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
