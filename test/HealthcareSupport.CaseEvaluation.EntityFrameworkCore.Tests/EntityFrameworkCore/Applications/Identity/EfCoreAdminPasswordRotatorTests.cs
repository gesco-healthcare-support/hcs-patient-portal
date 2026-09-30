using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Identity;

public class EfCoreAdminPasswordRotatorTests
    : AdminPasswordRotatorTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
