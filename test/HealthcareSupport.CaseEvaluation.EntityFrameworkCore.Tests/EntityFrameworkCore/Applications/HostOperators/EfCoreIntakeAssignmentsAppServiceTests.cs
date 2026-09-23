using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreIntakeAssignmentsAppServiceTests
    : IntakeAssignmentsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
