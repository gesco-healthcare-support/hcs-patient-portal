using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Identity;

public class EfCoreKnownDefaultPasswordSignInManagerTests
    : KnownDefaultPasswordSignInManagerTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
