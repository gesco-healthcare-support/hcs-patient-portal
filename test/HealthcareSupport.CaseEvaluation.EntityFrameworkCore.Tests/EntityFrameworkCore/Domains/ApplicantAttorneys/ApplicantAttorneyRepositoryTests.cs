using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class ApplicantAttorneyRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IApplicantAttorneyRepository _attorneyRepository;
    private readonly ICurrentTenant _currentTenant;

    public ApplicantAttorneyRepositoryTests()
    {
        _attorneyRepository = GetRequiredService<IApplicantAttorneyRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesFirmNameFilter()
    {
        // ApplicantAttorney is IMultiTenant. Seeded Attorney1 lives in TenantA;
        // wrap in TenantA context so ABP's IMultiTenant auto-filter matches.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var results = await _attorneyRepository.GetListAsync(
                    firmName: ApplicantAttorneysTestData.Attorney1FirmName);

                results.Any(x => x.Id == ApplicantAttorneysTestData.Attorney1Id).ShouldBeTrue();
                results.All(x => x.FirmName != null
                                 && x.FirmName.Contains(ApplicantAttorneysTestData.Attorney1FirmName))
                       .ShouldBeTrue();
            }
        });
    }
}
