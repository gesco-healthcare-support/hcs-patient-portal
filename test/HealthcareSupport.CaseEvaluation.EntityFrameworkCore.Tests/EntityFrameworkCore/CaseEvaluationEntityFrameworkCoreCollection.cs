using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

[CollectionDefinition(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class CaseEvaluationEntityFrameworkCoreCollection : ICollectionFixture<CaseEvaluationEntityFrameworkCoreFixture>
{

}
