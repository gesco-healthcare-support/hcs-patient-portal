using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreInternalUsersFailurePathTests
    : InternalUsersFailurePathTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
