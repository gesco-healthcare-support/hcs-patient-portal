using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts.Jobs;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDraftCleanupJobTests : DraftCleanupJobTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
