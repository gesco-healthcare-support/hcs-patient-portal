using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed's two health alerts (#927): each incident emails once when it starts and once when it clears, and
/// an office not on the feed is never checked. The store's SQL is substituted; its no-horizon outstanding count
/// is proved on SQL Server.
/// </summary>
public class CaseTrackerFeedHealthJobTests
{
    private static readonly Guid OfficeId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly DateTime Now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public required CaseTrackerFeedHealthJob Job { get; init; }
        public required CaseTrackerFeedState State { get; init; }
        public required ICaseTrackerFeedStateRepository Repository { get; init; }
        public required ICaseTrackerFeedStore Store { get; init; }
        public required ILocalEventBus Bus { get; init; }
    }

    /// <param name="startedMinutesAgo">When the feed started; also the stall clock's start.</param>
    /// <param name="lastRequestMinutesAgo">Null = no request since the start.</param>
    /// <param name="outstandingOld">Whether a row has waited past the stall threshold.</param>
    private static Harness Build(
        int startedMinutesAgo = 60,
        int? lastRequestMinutesAgo = 1,
        bool outstandingOld = false,
        bool active = true,
        bool pushSwitchOn = true)
    {
        var state = new CaseTrackerFeedState(Guid.NewGuid(), OfficeId);
        state.Start(100, Now.AddMinutes(-startedMinutesAgo));
        if (lastRequestMinutesAgo.HasValue)
        {
            Set(state, nameof(CaseTrackerFeedState.LastRequestAt), Now.AddMinutes(-lastRequestMinutesAgo.Value));
        }

        if (!active)
        {
            state.ReturnToPush(Now.AddMinutes(-2));
        }

        var runner = Substitute.For<ITenantWorkRunner>();
        runner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>()).Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

        var repository = Substitute.For<ICaseTrackerFeedStateRepository>();
        repository.FindCurrentAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<CaseTrackerFeedState?>(state));

        var store = Substitute.For<ICaseTrackerFeedStore>();
        store.HasOutstandingCreatedBeforeAsync(OfficeId, Arg.Any<long>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(outstandingOld));
        store.CountOutstandingAsync(OfficeId, Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(4));

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.FindAsync(OfficeId)
            .Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(OfficeId, "Sample Medical Group")));
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        var bus = Substitute.For<ILocalEventBus>();

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled)
            .Returns(pushSwitchOn ? "true" : "false");

        var job = new CaseTrackerFeedHealthJob(
            runner, repository, store,
            new CaseTrackerFeedAlertPublisher(bus, tenantStore),
            new CaseTrackerDeliveryModeReader(settingProvider, repository),
            clock, NullLogger<CaseTrackerFeedHealthJob>.Instance);
        return new Harness { Job = job, State = state, Repository = repository, Store = store, Bus = bus };
    }

    private static void Set(CaseTrackerFeedState state, string property, DateTime? value) =>
        typeof(CaseTrackerFeedState).GetProperty(property)!.SetValue(state, value);

    private static Task PublishedAsync(Harness h, CaseTrackerFeedAlertKind kind) =>
        h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerFeedAlertEto>(e =>
            e.Kind == kind && e.TenantId == OfficeId && e.OfficeName == "Sample Medical Group"));

    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(40, true)]
    public void IsSilent_FromTheFifteenthMinuteWithoutARequest(int minutesSinceLastRequest, bool expected)
    {
        var h = Build(lastRequestMinutesAgo: minutesSinceLastRequest);

        CaseTrackerFeedHealthJob.IsSilent(h.State, Now).ShouldBe(expected);
    }

    [Fact]
    public void IsSilent_WithNoRequestYet_CountsFromWhenTheFeedStarted()
    {
        CaseTrackerFeedHealthJob.IsSilent(Build(startedMinutesAgo: 10, lastRequestMinutesAgo: null).State, Now).ShouldBeFalse();
        CaseTrackerFeedHealthJob.IsSilent(Build(startedMinutesAgo: 20, lastRequestMinutesAgo: null).State, Now).ShouldBeTrue();
    }

    [Fact]
    public async Task ASilentConsumer_IsAlertedOnce_AndStamped()
    {
        var h = Build(lastRequestMinutesAgo: 20);

        await h.Job.ExecuteAsync();

        await PublishedAsync(h, CaseTrackerFeedAlertKind.SilenceStarted);
        await h.Repository.Received(1).SetSilenceAlertedAsync(h.State.Id, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ASilenceAlreadyAlerted_IsNotEmailedAgain()
    {
        var h = Build(lastRequestMinutesAgo: 40);
        Set(h.State, nameof(CaseTrackerFeedState.SilenceAlertedAt), Now.AddMinutes(-20));

        await h.Job.ExecuteAsync();

        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerFeedAlertEto>());
    }

    [Fact]
    public async Task WhenRequestsResume_TheSilenceIsClearedWithOneEmail()
    {
        var h = Build(lastRequestMinutesAgo: 1);
        Set(h.State, nameof(CaseTrackerFeedState.SilenceAlertedAt), Now.AddMinutes(-20));

        await h.Job.ExecuteAsync();

        await PublishedAsync(h, CaseTrackerFeedAlertKind.SilenceCleared);
        await h.Repository.Received(1).SetSilenceAlertedAsync(h.State.Id, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnOldWaitingRow_WithAnIdlePosition_IsAStall_WithTheOutstandingCount()
    {
        var h = Build(startedMinutesAgo: 60, outstandingOld: true);

        await h.Job.ExecuteAsync();

        await h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerFeedAlertEto>(e =>
            e.Kind == CaseTrackerFeedAlertKind.StallStarted && e.OutstandingCount == 4));
        await h.Repository.Received(1).SetStallAlertedAsync(h.State.Id, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task APositionThatMovedRecently_IsNotAStall_EvenWithAnOldRowWaiting()
    {
        // Both halves are needed: a consumer working through a backlog is advancing, not stalled.
        var h = Build(startedMinutesAgo: 60, outstandingOld: true);
        Set(h.State, nameof(CaseTrackerFeedState.LastAdvancedAt), Now.AddMinutes(-5));

        await h.Job.ExecuteAsync();

        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerFeedAlertEto>());
        await h.Store.DidNotReceiveWithAnyArgs().HasOutstandingCreatedBeforeAsync(default, default, default, default);
    }

    [Fact]
    public async Task WhenNothingOldIsWaiting_AnOpenStallIsCleared()
    {
        var h = Build(startedMinutesAgo: 60, outstandingOld: false);
        Set(h.State, nameof(CaseTrackerFeedState.StallAlertedAt), Now.AddMinutes(-10));

        await h.Job.ExecuteAsync();

        await PublishedAsync(h, CaseTrackerFeedAlertKind.StallCleared);
        await h.Repository.Received(1).SetStallAlertedAsync(h.State.Id, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnOfficeNotOnTheFeed_IsNeverChecked()
    {
        var h = Build(lastRequestMinutesAgo: 90, outstandingOld: true, active: false);

        await h.Job.ExecuteAsync();

        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerFeedAlertEto>());
        await h.Store.DidNotReceiveWithAnyArgs().HasOutstandingCreatedBeforeAsync(default, default, default, default);
    }

    [Fact]
    public async Task AnOfficeSwitchedOff_IsNotAlerted_BecauseItsQuietIsDeliberate()
    {
        var h = Build(lastRequestMinutesAgo: 90, outstandingOld: true, pushSwitchOn: false);

        await h.Job.ExecuteAsync();

        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerFeedAlertEto>());
    }

    [Fact]
    public async Task AFailingOffice_IsLoggedAndDoesNotEndTheRun()
    {
        var h = Build();
        h.Repository.FindCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CaseTrackerFeedState?>(new InvalidOperationException("office database down")));

        await Should.NotThrowAsync(() => h.Job.ExecuteAsync());
    }
}
