using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreCaseTrackerPushSettingsFeedTests
    : CaseTrackerPushSettingsFeedTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
