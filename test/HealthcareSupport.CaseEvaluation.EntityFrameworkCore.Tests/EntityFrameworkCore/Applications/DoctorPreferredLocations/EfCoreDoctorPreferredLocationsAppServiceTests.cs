using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDoctorPreferredLocationsAppServiceTests
    : DoctorPreferredLocationsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
