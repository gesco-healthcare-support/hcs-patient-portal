using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Emailing;

public class EfCoreAccountEmailerQueueTests
    : AccountEmailerQueueTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
