using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Notifications;

public class EfCoreAppNotificationAppServiceTests
    : AppNotificationAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
