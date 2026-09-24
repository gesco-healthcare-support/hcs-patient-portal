using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Documents;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDocumentsStorageTests
    : DocumentsStorageTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
