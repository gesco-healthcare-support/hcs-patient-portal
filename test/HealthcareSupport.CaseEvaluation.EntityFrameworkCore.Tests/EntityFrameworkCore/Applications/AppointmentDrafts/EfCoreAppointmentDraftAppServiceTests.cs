using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentDraftAppServiceTests
    : AppointmentDraftAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
