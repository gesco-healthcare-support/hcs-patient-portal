using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCorePublicChangeRequestConsentAppServiceTests
    : PublicChangeRequestConsentAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
