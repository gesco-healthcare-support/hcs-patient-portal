using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentPartyLinkServicesTests
    : AppointmentPartyLinkServicesTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
