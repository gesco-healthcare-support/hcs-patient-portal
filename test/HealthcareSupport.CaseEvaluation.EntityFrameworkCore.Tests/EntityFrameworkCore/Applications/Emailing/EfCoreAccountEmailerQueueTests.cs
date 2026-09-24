using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Emailing;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAccountEmailerQueueTests
    : AccountEmailerQueueTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
