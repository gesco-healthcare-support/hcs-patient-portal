using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.ClaimExaminers;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreClaimExaminerManagerTests : ClaimExaminerManagerTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
