using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCorePackageDetailsAppServiceTests
    : PackageDetailsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
