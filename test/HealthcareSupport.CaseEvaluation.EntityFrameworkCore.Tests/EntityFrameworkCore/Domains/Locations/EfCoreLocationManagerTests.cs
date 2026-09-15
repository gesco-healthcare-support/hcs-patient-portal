using HealthcareSupport.CaseEvaluation.Locations;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.Locations;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreLocationManagerTests : LocationManagerTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
