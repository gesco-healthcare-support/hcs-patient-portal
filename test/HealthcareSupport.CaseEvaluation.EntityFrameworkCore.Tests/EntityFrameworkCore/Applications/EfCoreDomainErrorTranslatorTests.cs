using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Applications;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDomainErrorTranslatorTests : DomainErrorTranslatorTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
