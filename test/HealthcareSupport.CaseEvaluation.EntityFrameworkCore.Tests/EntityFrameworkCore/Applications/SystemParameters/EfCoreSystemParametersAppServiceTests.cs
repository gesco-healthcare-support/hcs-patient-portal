using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreSystemParametersAppServiceTests
    : SystemParametersAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
