using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Locations;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class LocationRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly ILocationRepository _locationRepository;

    public LocationRepositoryTests()
    {
        _locationRepository = GetRequiredService<ILocationRepository>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesNameFilter()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var results = await _locationRepository.GetListAsync(
                name: LocationsTestData.Location1Name);

            results.Any(x => x.Id == LocationsTestData.Location1Id).ShouldBeTrue();
            results.All(x => x.Name != null && x.Name.Contains(LocationsTestData.Location1Name))
                   .ShouldBeTrue();
        });
    }
}
