using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Timing;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreClockKindTests : ClockKindTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
