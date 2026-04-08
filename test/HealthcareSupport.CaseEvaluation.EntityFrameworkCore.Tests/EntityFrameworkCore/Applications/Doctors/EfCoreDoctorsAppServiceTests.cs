using HealthcareSupport.CaseEvaluation.Doctors;
using Xunit;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class EfCoreDoctorsAppServiceTests : DoctorsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}