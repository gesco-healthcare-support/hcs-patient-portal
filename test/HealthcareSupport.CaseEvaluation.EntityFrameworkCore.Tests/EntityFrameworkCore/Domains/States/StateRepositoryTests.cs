using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.States;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class StateRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IStateRepository _stateRepository;

    public StateRepositoryTests()
    {
        _stateRepository = GetRequiredService<IStateRepository>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesNameFilter()
    {
        // The custom IStateRepository.GetListAsync(name) overload is what the
        // AppService delegates to. Confirm it filters by Name + returns only
        // the matching state row inside a unit-of-work scope.
        await WithUnitOfWorkAsync(async () =>
        {
            var results = await _stateRepository.GetListAsync(name: "Nevada");

            results.Any(x => x.Id == StatesTestData.State2Id).ShouldBeTrue();
            results.All(x => x.Name == StatesTestData.State2Name).ShouldBeTrue();
        });
    }
}
