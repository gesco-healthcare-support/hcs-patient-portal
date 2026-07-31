using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Branding;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreBrandingAppServiceTests
    : BrandingAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
