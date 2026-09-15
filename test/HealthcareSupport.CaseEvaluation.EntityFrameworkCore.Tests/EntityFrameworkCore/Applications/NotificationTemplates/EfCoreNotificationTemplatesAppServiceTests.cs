using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreNotificationTemplatesAppServiceTests
    : NotificationTemplatesAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
