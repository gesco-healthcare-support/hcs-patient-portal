using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreTenantWorkRunnerTests : TenantWorkRunnerTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
