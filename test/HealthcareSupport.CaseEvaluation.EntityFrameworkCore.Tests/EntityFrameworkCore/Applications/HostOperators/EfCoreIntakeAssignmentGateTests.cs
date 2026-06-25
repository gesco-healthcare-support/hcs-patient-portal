using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreIntakeAssignmentGateTests
    : IntakeAssignmentGateTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
