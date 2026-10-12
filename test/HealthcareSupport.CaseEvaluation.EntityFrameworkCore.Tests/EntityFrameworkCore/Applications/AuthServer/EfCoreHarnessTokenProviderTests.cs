using HealthcareSupport.CaseEvaluation.AuthServer.Tests;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.AuthServer;

public class EfCoreHarnessTokenProviderTests
    : HarnessTokenProviderTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
