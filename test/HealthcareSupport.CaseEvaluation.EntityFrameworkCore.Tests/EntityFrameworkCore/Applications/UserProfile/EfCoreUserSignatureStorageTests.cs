using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.UserProfile;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreUserSignatureStorageTests
    : UserSignatureStorageTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
