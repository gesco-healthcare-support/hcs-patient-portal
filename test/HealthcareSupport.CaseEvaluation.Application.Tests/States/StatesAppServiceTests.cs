using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.States;

public abstract class StatesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IStatesAppService _statesAppService;
    private readonly IStateRepository _stateRepository;
    private readonly StateManager _stateManager;

    protected StatesAppServiceTests()
    {
        _statesAppService = GetRequiredService<IStatesAppService>();
        _stateRepository = GetRequiredService<IStateRepository>();
        _stateManager = GetRequiredService<StateManager>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path -- State is host-only; no CurrentTenant wrap needed
    // because non-IMultiTenant entities bypass the filter regardless of
    // CurrentTenant context (external research Track 1.1).
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededState1()
    {
        var result = await _statesAppService.GetAsync(StatesTestData.State1Id);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(StatesTestData.State1Id);
        result.Name.ShouldBe(StatesTestData.State1Name);
    }

    [Fact]
    public async Task GetListAsync_ReturnsAllSeededStates()
    {
        var result = await _statesAppService.GetListAsync(new GetStatesInput
        {
            MaxResultCount = 100
        });

        result.Items.Any(x => x.Id == StatesTestData.State1Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == StatesTestData.State2Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == StatesTestData.State3Id).ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_FiltersByName_ReturnsMatchingState()
    {
        var result = await _statesAppService.GetListAsync(new GetStatesInput
        {
            Name = "Nevada",
            MaxResultCount = 100
        });

        result.Items.Any(x => x.Id == StatesTestData.State2Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == StatesTestData.State1Id).ShouldBeFalse();
        result.Items.Any(x => x.Id == StatesTestData.State3Id).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_PersistsNewState()
    {
        var input = new StateCreateDto { Name = "TEST-CreatedScratch" };

        var created = await _statesAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.Name.ShouldBe(input.Name);

        var persisted = await _stateRepository.FindAsync(created.Id);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        var existing = await _stateRepository.GetAsync(StatesTestData.State1Id);
        var update = new StateUpdateDto
        {
            Name = "TEST-MutatedCalifornia",
            ConcurrencyStamp = existing.ConcurrencyStamp
        };

        var result = await _statesAppService.UpdateAsync(StatesTestData.State1Id, update);
        result.Name.ShouldBe("TEST-MutatedCalifornia");

        // Restore seed value so downstream tests keep their baseline.
        var current = await _stateRepository.GetAsync(StatesTestData.State1Id);
        current.Name = StatesTestData.State1Name;
        await _stateRepository.UpdateAsync(current);
    }

    [Fact]
    public async Task DeleteAsync_RemovesState()
    {
        // Create-then-delete to avoid wiping the seeded states (Tier-1/2 tests
        // assert against State1Id; deleting it would break the suite).
        var created = await _statesAppService.CreateAsync(new StateCreateDto
        {
            Name = "TEST-ToDelete"
        });

        await _statesAppService.DeleteAsync(created.Id);

        (await _stateRepository.FindAsync(created.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenNameIsWhiteSpace_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(() => _stateManager.CreateAsync("   "));
    }
}
