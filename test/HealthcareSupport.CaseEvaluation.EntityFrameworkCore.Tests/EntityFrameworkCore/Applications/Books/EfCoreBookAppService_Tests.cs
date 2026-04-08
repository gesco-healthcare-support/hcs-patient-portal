using HealthcareSupport.CaseEvaluation.Books;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Applications.Books;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreBookAppService_Tests : BookAppService_Tests<CaseEvaluationEntityFrameworkCoreTestModule>
{

}