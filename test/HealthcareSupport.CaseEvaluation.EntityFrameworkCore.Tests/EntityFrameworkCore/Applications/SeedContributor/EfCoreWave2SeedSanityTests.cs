using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Testing;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreWave2SeedSanityTests : Wave2SeedSanityTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
