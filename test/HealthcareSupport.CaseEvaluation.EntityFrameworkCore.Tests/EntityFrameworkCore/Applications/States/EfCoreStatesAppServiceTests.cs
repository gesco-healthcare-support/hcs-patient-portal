using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.States;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreStatesAppServiceTests : StatesAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
