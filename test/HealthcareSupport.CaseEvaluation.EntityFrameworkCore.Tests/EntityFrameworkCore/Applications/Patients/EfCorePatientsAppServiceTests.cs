using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCorePatientsAppServiceTests : PatientsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
