using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentAccessorManagerTests
    : AppointmentAccessorManagerTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
