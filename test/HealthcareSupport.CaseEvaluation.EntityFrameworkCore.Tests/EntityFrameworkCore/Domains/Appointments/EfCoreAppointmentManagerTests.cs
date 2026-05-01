using HealthcareSupport.CaseEvaluation.Appointments;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.Appointments;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentManagerTests : AppointmentManagerTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
