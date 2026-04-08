using HealthcareSupport.CaseEvaluation.Samples;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Applications;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{

}
