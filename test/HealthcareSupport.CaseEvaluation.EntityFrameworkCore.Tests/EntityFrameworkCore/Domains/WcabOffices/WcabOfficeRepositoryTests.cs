using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class WcabOfficeRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IWcabOfficeRepository _officeRepository;

    public WcabOfficeRepositoryTests()
    {
        _officeRepository = GetRequiredService<IWcabOfficeRepository>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesNameFilter()
    {
        // The custom IWcabOfficeRepository.GetListAsync(name) overload powers
        // the AppService's GetListAsync (alongside its sibling
        // GetListWithNavigationPropertiesAsync). Confirm the plain overload
        // returns only the matching row.
        await WithUnitOfWorkAsync(async () =>
        {
            var results = await _officeRepository.GetListAsync(name: "Fresno");

            results.Any(x => x.Id == WcabOfficesTestData.Office2Id).ShouldBeTrue();
            results.Any(x => x.Id == WcabOfficesTestData.Office1Id).ShouldBeFalse();
        });
    }
}
