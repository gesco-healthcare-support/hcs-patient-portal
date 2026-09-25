using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>The operator's feed actions (#927) over a substituted record store and SQL-Server-only reader.</summary>
public class CaseTrackerFeedManagerTests
{
    private static readonly Guid OfficeId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly DateTime Now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    private static (CaseTrackerFeedManager Manager, ICaseTrackerFeedStateRepository Repository, ICaseTrackerFeedStore Store)
        Build(CaseTrackerFeedState? existing)
    {
        var repository = Substitute.For<ICaseTrackerFeedStateRepository>();
        repository.FindCurrentAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(existing));
        var store = Substitute.For<ICaseTrackerFeedStore>();
        store.GetStartFloorAsync(OfficeId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(700L));
        store.CountOutstandingAsync(OfficeId, Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(2));
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        return (new CaseTrackerFeedManager(repository, store, clock, SimpleGuidGenerator.Instance), repository, store);
    }

    private static CaseTrackerFeedState Active()
    {
        var state = new CaseTrackerFeedState(Guid.NewGuid(), OfficeId);
        state.Start(100, Now.AddHours(-1));
        return state;
    }

    [Fact]
    public async Task StartAsync_ForANewOffice_InsertsAnActiveRecordAtTheStartFloor()
    {
        var (manager, repository, _) = Build(existing: null);

        (await manager.StartAsync(OfficeId)).ShouldBeTrue();

        await repository.Received(1).InsertAsync(
            Arg.Is<CaseTrackerFeedState>(s => s.IsActive && s.FloorPosition == 700 && s.TenantId == OfficeId && s.StartedAt == Now),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyOn_ChangesNothing_AndReadsNoFloor()
    {
        var (manager, repository, store) = Build(Active());

        (await manager.StartAsync(OfficeId)).ShouldBeFalse();

        await store.DidNotReceiveWithAnyArgs().GetStartFloorAsync(default, default);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }

    [Fact]
    public async Task StartAsync_AfterAReturnToPush_RestartsTheSameRecordAtANewFloor()
    {
        var existing = Active();
        existing.ReturnToPush(Now.AddMinutes(-30));
        var (manager, repository, _) = Build(existing);

        (await manager.StartAsync(OfficeId)).ShouldBeTrue();

        existing.IsActive.ShouldBeTrue();
        existing.FloorPosition.ShouldBe(700);
        await repository.Received(1).UpdateAsync(existing, true, Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().InsertAsync(default!, default, default);
    }

    [Fact]
    public async Task ReturnToPushAsync_OnlyActsOnAnActiveFeed()
    {
        var active = Active();
        (await Build(active).Manager.ReturnToPushAsync()).ShouldBeTrue();
        active.IsActive.ShouldBeFalse();

        (await Build(existing: null).Manager.ReturnToPushAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_CountsOutstandingRows_OnlyWhileTheFeedIsOn()
    {
        var (onFeed, _, _) = Build(Active());
        var (onPush, _, pushStore) = Build(existing: null);

        (await onFeed.GetStatusAsync(OfficeId)).ShouldBe(new CaseTrackerFeedStatus(true, Now.AddHours(-1), null, Now.AddHours(-1), 2));
        (await onPush.GetStatusAsync(OfficeId)).OutstandingCount.ShouldBeNull();
        await pushStore.DidNotReceiveWithAnyArgs().CountOutstandingAsync(default, default, default);
    }
}
