using HealthcareSupport.CaseEvaluation.Appointments;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Applications.Appointments;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAttorneyUpsertBranchTests : AttorneyUpsertBranchTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
