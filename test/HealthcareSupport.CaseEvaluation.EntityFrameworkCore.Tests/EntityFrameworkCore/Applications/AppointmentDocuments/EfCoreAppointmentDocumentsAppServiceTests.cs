using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentDocumentsAppServiceTests
    : AppointmentDocumentsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
