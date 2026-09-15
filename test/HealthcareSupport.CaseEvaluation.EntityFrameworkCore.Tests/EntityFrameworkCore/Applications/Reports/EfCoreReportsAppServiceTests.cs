using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreReportsAppServiceTests : ReportsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
