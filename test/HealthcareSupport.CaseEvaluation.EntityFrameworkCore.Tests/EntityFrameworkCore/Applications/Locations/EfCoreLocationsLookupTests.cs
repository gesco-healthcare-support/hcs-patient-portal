using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Locations;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreLocationsLookupTests
    : LocationsLookupTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
