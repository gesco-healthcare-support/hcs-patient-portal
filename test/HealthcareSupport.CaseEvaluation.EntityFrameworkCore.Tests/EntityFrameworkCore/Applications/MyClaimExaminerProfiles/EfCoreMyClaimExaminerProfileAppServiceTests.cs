using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MyClaimExaminerProfiles;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreMyClaimExaminerProfileAppServiceTests
    : MyClaimExaminerProfileAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
