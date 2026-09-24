using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAccountSelfServiceFlowTests
    : AccountSelfServiceFlowTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
