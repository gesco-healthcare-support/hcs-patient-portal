using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDoctorAvailabilitiesGuardTests
    : DoctorAvailabilitiesGuardTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
