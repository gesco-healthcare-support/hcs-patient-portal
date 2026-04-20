using HealthcareSupport.CaseEvaluation.Books;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Applications.Books;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreBookAppServiceTests : BookAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{

}