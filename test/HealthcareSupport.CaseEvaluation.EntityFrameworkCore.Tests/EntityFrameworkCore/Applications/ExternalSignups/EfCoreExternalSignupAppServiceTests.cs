using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreExternalSignupAppServiceTests
    : ExternalSignupAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
