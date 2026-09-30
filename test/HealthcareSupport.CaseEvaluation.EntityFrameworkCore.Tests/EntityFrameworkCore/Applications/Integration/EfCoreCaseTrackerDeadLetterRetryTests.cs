using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

public class EfCoreCaseTrackerDeadLetterRetryTests
    : CaseTrackerDeadLetterRetryTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
