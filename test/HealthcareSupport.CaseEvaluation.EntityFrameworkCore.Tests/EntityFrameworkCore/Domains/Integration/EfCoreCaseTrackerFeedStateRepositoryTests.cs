using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed record's set-based writes (#927) against a real (SQLite) EF context. The rules live in the UPDATE
/// statements, so this is the only layer that can prove them: a poll never moves an office backwards, and
/// <c>LastAdvancedAt</c> -- the stall alert's clock -- moves only when the position does.
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreCaseTrackerFeedStateRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly DateTime Started = new(2026, 9, 24, 17, 0, 0, DateTimeKind.Utc);

    private readonly ICaseTrackerFeedStateRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public EfCoreCaseTrackerFeedStateRepositoryTests()
    {
        _repository = GetRequiredService<ICaseTrackerFeedStateRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    /// <summary>
    /// An active record at floor 100 for a seeded office. Seeded offices rather than random ids, because entering
    /// an office resolves its connection, and every test gets a fresh database, so the one-row-per-office index
    /// never sees a previous test's record.
    /// </summary>
    private async Task<(Guid OfficeId, Guid StateId)> StartedOfficeAsync(Guid? office = null)
    {
        var officeId = office ?? TenantsTestData.TenantARef;
        var state = new CaseTrackerFeedState(Guid.NewGuid(), officeId);
        state.Start(100, Started);
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                await _repository.InsertAsync(state, autoSave: true);
            }
        });
        return (officeId, state.Id);
    }

    private Task InOfficeAsync(Guid officeId, Func<Task> work) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                await work();
            }
        });

    private async Task<CaseTrackerFeedState> ReadAsync(Guid officeId, Guid stateId)
    {
        CaseTrackerFeedState state = null!;
        await InOfficeAsync(officeId, async () => state = await _repository.GetAsync(stateId));
        return state;
    }

    [Fact]
    public async Task RecordRequestAsync_WithAHigherPosition_AdvancesIt_AndRestartsTheStallClock()
    {
        var (officeId, stateId) = await StartedOfficeAsync();
        var now = Started.AddMinutes(10);

        await InOfficeAsync(officeId, () => _repository.RecordRequestAsync(stateId, now, acknowledged: 150, highestIssued: 180));

        var state = await ReadAsync(officeId, stateId);
        state.AcknowledgedPosition.ShouldBe(150);
        state.HighestIssuedPosition.ShouldBe(180);
        state.LastAdvancedAt.ShouldBe(now);
        state.LastRequestAt.ShouldBe(now);
    }

    [Fact]
    public async Task RecordRequestAsync_WithALowerPosition_NeverMovesTheOfficeBackwards()
    {
        // A late or replayed request, or a consumer re-reading after restoring an older copy of its own state.
        var (officeId, stateId) = await StartedOfficeAsync();
        await InOfficeAsync(officeId, () => _repository.RecordRequestAsync(stateId, Started.AddMinutes(1), 150, 180));
        var later = Started.AddMinutes(2);

        await InOfficeAsync(officeId, () => _repository.RecordRequestAsync(stateId, later, acknowledged: 120, highestIssued: 130));

        var state = await ReadAsync(officeId, stateId);
        state.AcknowledgedPosition.ShouldBe(150);
        state.HighestIssuedPosition.ShouldBe(180);
        state.LastAdvancedAt.ShouldBe(Started.AddMinutes(1)); // the stall clock did NOT restart
        state.LastRequestAt.ShouldBe(later); // but the consumer is alive, so silence did
    }

    [Fact]
    public async Task RecordRequestAsync_AtTheSamePosition_DoesNotRestartTheStallClock()
    {
        // A consumer polling but not advancing is exactly what the stall alert exists to see.
        var (officeId, stateId) = await StartedOfficeAsync();

        await InOfficeAsync(officeId, () => _repository.RecordRequestAsync(stateId, Started.AddMinutes(40), 100, 100));

        var state = await ReadAsync(officeId, stateId);
        state.LastAdvancedAt.ShouldBe(Started);
        state.LastRequestAt.ShouldBe(Started.AddMinutes(40));
    }

    [Fact]
    public async Task AlertStamps_AreSetAndCleared_EachWithoutTouchingTheOther()
    {
        var (officeId, stateId) = await StartedOfficeAsync();
        var at = Started.AddMinutes(20);

        await InOfficeAsync(officeId, () => _repository.SetSilenceAlertedAsync(stateId, at));
        await InOfficeAsync(officeId, () => _repository.SetStallAlertedAsync(stateId, at.AddMinutes(1)));
        await InOfficeAsync(officeId, () => _repository.SetSilenceAlertedAsync(stateId, null));

        var state = await ReadAsync(officeId, stateId);
        state.SilenceAlertedAt.ShouldBeNull();
        state.StallAlertedAt.ShouldBe(at.AddMinutes(1));
    }

    [Fact]
    public async Task FindCurrentAsync_ReturnsThisOfficesRecord_AndNoOtherOffices()
    {
        var (officeA, stateA) = await StartedOfficeAsync(TenantsTestData.TenantARef);
        var officeWithoutFeed = TenantsTestData.TenantBRef;

        CaseTrackerFeedState? found = null;
        CaseTrackerFeedState? none = null;
        await InOfficeAsync(officeA, async () => found = await _repository.FindCurrentAsync());
        await InOfficeAsync(officeWithoutFeed, async () => none = await _repository.FindCurrentAsync());

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(stateA);
        none.ShouldBeNull();
    }
}
