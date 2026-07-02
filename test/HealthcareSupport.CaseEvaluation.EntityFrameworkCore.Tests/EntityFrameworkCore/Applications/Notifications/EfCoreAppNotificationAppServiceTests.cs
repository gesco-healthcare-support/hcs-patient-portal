using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppNotificationAppServiceTests
    : AppNotificationAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
