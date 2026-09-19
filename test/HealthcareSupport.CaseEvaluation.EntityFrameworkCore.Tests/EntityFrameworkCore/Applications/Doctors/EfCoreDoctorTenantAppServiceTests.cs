using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDoctorTenantAppServiceTests
    : DoctorTenantAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
