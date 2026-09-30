using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class EfCoreDoctorTenantAppServiceTests
    : DoctorTenantAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
