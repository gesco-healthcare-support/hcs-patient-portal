using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentsAppServiceTests : AppointmentsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
