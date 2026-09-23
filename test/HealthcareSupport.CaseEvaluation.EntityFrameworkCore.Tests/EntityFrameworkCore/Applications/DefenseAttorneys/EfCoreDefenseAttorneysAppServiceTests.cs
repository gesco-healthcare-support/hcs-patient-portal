using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDefenseAttorneysAppServiceTests
    : DefenseAttorneysAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
