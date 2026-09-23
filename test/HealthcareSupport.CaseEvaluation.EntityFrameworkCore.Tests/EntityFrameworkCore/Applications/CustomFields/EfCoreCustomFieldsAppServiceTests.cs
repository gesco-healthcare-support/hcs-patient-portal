using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreCustomFieldsAppServiceTests
    : CustomFieldsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
