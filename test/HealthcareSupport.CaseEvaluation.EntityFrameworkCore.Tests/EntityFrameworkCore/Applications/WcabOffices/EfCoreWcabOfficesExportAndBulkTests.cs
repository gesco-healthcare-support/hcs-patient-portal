using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreWcabOfficesExportAndBulkTests
    : WcabOfficesExportAndBulkTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
