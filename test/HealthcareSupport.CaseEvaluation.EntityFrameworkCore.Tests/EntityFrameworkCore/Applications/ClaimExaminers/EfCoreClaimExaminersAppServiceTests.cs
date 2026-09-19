using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreClaimExaminersAppServiceTests : ClaimExaminersAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
