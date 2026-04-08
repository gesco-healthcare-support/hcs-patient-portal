using HealthcareSupport.CaseEvaluation.Samples;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<CaseEvaluationEntityFrameworkCoreTestModule>
{

}
