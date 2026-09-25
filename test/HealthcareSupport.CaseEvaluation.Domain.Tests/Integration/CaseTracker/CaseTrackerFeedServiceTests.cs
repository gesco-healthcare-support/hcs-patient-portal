using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed's request rules (#927) with the SQL-Server-only store substituted: which cursor is accepted, what
/// is acknowledged, what is alerted. The store's own SQL -- the no-skip guarantee -- is proved separately on a
/// real SQL Server (<c>CaseTrackerFeedSqlServerTests</c>).
/// </summary>
public class CaseTrackerFeedServiceTests
{
    private static readonly Guid OfficeId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly DateTime Now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public required CaseTrackerFeedService Service { get; init; }
        public required CaseTrackerFeedState State { get; init; }
        public required ICaseTrackerFeedStateRepository Repository { get; init; }
        public required ICaseTrackerFeedStore Store { get; init; }
        public required ILocalEventBus Bus { get; init; }
        public required List<CaseTrackerFeedRow> Rows { get; init; }
    }

    /// <summary>
    /// An office started at floor 100 that has acknowledged <paramref name="acknowledged"/> and been issued up
    /// to <paramref name="issued"/>. The store serves <see cref="Harness.Rows"/> by position, as the SQL would.
    /// </summary>
    private static Harness Build(
        long acknowledged = 100, long issued = 100, bool active = true, int rowCount = 0, bool pushSwitchOn = true)
    {
        var state = new CaseTrackerFeedState(Guid.NewGuid(), OfficeId);
        state.Start(100, Now.AddHours(-1));
        Advance(state, acknowledged, issued);
        if (!active)
        {
            state.ReturnToPush(Now.AddMinutes(-5));
        }

        var rows = Enumerable.Range(1, rowCount)
            .Select(i => new CaseTrackerFeedRow(100 + i, IntegrationMessageType.Intake, AppointmentId, "{\"data\":{}}"))
            .ToList();

        var repository = Substitute.For<ICaseTrackerFeedStateRepository>();
        repository.FindCurrentAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<CaseTrackerFeedState?>(state));

        var store = Substitute.For<ICaseTrackerFeedStore>();
        store.ReadPageAsync(OfficeId, Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(rows
                .Where(r => r.Position > ci.ArgAt<long>(1))
                .Take(ci.ArgAt<int>(2))
                .ToList()));
        store.FindRowAsync(OfficeId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(rows.FirstOrDefault(r => r.Position == ci.ArgAt<long>(1))));

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.FindAsync(OfficeId)
            .Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(OfficeId, "Sample Medical Group")));
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        var bus = Substitute.For<ILocalEventBus>();

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled)
            .Returns(pushSwitchOn ? "true" : "false");

        var service = new CaseTrackerFeedService(
            Substitute.For<ICurrentTenant>(), repository, store,
            new CaseTrackerFeedAlertPublisher(bus, tenantStore),
            new CaseTrackerDeliveryModeReader(settingProvider, repository),
            clock, NullLogger<CaseTrackerFeedService>.Instance);

        return new Harness { Service = service, State = state, Repository = repository, Store = store, Bus = bus, Rows = rows };
    }

    /// <summary>Moves the record the way the repository's monotonic UPDATE would, for test setup.</summary>
    private static void Advance(CaseTrackerFeedState state, long acknowledged, long issued)
    {
        typeof(CaseTrackerFeedState).GetProperty(nameof(CaseTrackerFeedState.AcknowledgedPosition))!.SetValue(state, acknowledged);
        typeof(CaseTrackerFeedState).GetProperty(nameof(CaseTrackerFeedState.HighestIssuedPosition))!.SetValue(state, issued);
    }

    private static string Cursor(long position) => CaseTrackerFeedCursor.Encode(position);

    private static Task<CaseTrackerFeedResult> ReadAsync(Harness h, string? cursor, params string[] skipped) =>
        h.Service.ReadAsync(OfficeId, cursor, skipped);

    [Fact]
    public async Task WithNoCursor_ServesFromTheAcknowledgedPosition_AndRecordsIt()
    {
        var h = Build(acknowledged: 102, issued: 103, rowCount: 5);

        var result = await ReadAsync(h, cursor: null);

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.Page);
        result.Rows.Select(r => r.Position).ShouldBe(new long[] { 103, 104, 105 });
        result.NextCursor.ShouldBe(Cursor(105));
        await h.Repository.Received(1).RecordRequestAsync(h.State.Id, Now, 102, 105, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ACursor_IsTheAcknowledgement_AndRowsAfterItAreServed()
    {
        var h = Build(acknowledged: 100, issued: 104, rowCount: 6);

        var result = await ReadAsync(h, Cursor(104));

        result.Rows.Select(r => r.Position).ShouldBe(new long[] { 105, 106 });
        await h.Repository.Received(1).RecordRequestAsync(h.State.Id, Now, 104, 106, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoreThanAPage_ServesExactlyAPage_AndSaysThereIsMore()
    {
        var h = Build(rowCount: CaseTrackerFeedConsts.PageSize + 5);

        var result = await ReadAsync(h, cursor: null);

        result.Rows.Count.ShouldBe(CaseTrackerFeedConsts.PageSize);
        result.HasMore.ShouldBeTrue();
        result.NextCursor.ShouldBe(Cursor(100 + CaseTrackerFeedConsts.PageSize));
    }

    [Fact]
    public async Task AnEmptyPage_HandsBackTheSameCursor_SoTheConsumerDoesNotMove()
    {
        // Normal while a write is in flight: the SQL withholds rows at or above MIN_ACTIVE_ROWVERSION().
        var h = Build(acknowledged: 100, issued: 100);

        var result = await ReadAsync(h, Cursor(100));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.Page);
        result.Rows.ShouldBeEmpty();
        result.HasMore.ShouldBeFalse();
        result.NextCursor.ShouldBe(Cursor(100));
    }

    [Fact]
    public async Task ACursorBelowTheFloor_IsRefused_AndMovesNothing()
    {
        var h = Build(acknowledged: 104, issued: 104, rowCount: 5);

        var result = await ReadAsync(h, Cursor(99));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.CursorBelowFloor);
        await h.Store.DidNotReceiveWithAnyArgs().ReadPageAsync(default, default, default, default);
        // Recorded as a sign of life, at the positions it already had.
        await h.Repository.Received(1).RecordRequestAsync(h.State.Id, Now, 104, 104, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("7D3")]
    [InlineData("not-a-cursor-at-all")]
    public async Task AMalformedCursor_IsRefused(string cursor)
    {
        var h = Build(rowCount: 3);

        var result = await ReadAsync(h, cursor);

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.CursorInvalid);
        await h.Store.DidNotReceiveWithAnyArgs().ReadPageAsync(default, default, default, default);
    }

    [Fact]
    public async Task ACursorBeyondAnythingIssued_IsRefused_AlertedOnce_AndMovesNothing()
    {
        var h = Build(acknowledged: 102, issued: 103, rowCount: 5);

        var result = await ReadAsync(h, Cursor(104));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.CursorAhead);
        await h.Store.DidNotReceiveWithAnyArgs().ReadPageAsync(default, default, default, default);
        await h.Repository.Received(1).RecordRequestAsync(h.State.Id, Now, 102, 103, Arg.Any<CancellationToken>());
        await h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerFeedAlertEto>(e =>
            e.Kind == CaseTrackerFeedAlertKind.CursorAhead
            && e.TenantId == OfficeId
            && e.OfficeName == "Sample Medical Group"
            && e.Cursor == Cursor(104)));
        await h.Repository.Received(1).SetCursorAheadAlertedAsync(h.State.Id, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ACursorBeyondAnythingIssued_WhileAlreadyAlerted_IsNotEmailedAgain()
    {
        var h = Build(acknowledged: 102, issued: 103, rowCount: 5);
        typeof(CaseTrackerFeedState).GetProperty(nameof(CaseTrackerFeedState.CursorAheadAlertedAt))!.SetValue(h.State, Now.AddMinutes(-1));

        var result = await ReadAsync(h, Cursor(104));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.CursorAhead);
        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerFeedAlertEto>());
    }

    [Fact]
    public async Task AGoodRequest_EndsACursorAheadIncident()
    {
        var h = Build(acknowledged: 102, issued: 103, rowCount: 5);
        typeof(CaseTrackerFeedState).GetProperty(nameof(CaseTrackerFeedState.CursorAheadAlertedAt))!.SetValue(h.State, Now.AddMinutes(-1));

        await ReadAsync(h, Cursor(103));

        await h.Repository.Received(1).SetCursorAheadAlertedAsync(h.State.Id, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ACursorAtTheFloor_IsValid_BecauseThatIsWhereAFeedStarts()
    {
        var h = Build(rowCount: 2);

        var result = await ReadAsync(h, Cursor(100));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.Page);
        result.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnOfficeNotOnTheFeed_IsRefused_AndNothingIsRecorded()
    {
        var h = Build(active: false, rowCount: 3);

        var result = await ReadAsync(h, cursor: null);

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.FeedNotEnabled);
        await h.Repository.DidNotReceiveWithAnyArgs().RecordRequestAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task AnOfficeWhosePushSwitchIsOff_IsRefusedLikeOneNotOnTheFeed()
    {
        // The switch is the ePHI gate: nothing leaves the portal for an office until it is switched on.
        var h = Build(rowCount: 3, pushSwitchOn: false);

        var result = await ReadAsync(h, cursor: null);

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.FeedNotEnabled);
        await h.Store.DidNotReceiveWithAnyArgs().ReadPageAsync(default, default, default, default);
    }

    [Fact]
    public async Task AnyFailureInsideTheOffice_AnswersFeedNotEnabled_NotAnError()
    {
        // An unknown office fails when its connection is resolved. A 500 would tell the caller it guessed a
        // real office; the same answer as "not on the feed" tells it nothing.
        var h = Build(rowCount: 3);
        h.Repository.FindCurrentAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("no such office"));

        var result = await ReadAsync(h, cursor: null);

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.FeedNotEnabled);
    }

    [Fact]
    public async Task ANewSkip_IsAlertedWithTheRowsAppointment_AndTheRequestIsServed()
    {
        var h = Build(acknowledged: 100, issued: 103, rowCount: 5);

        var result = await ReadAsync(h, Cursor(103), Cursor(102));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.Page);
        await h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerFeedAlertEto>(e =>
            e.Kind == CaseTrackerFeedAlertKind.SkipReported
            && e.AppointmentId == AppointmentId
            && e.MessageType == nameof(IntegrationMessageType.Intake)
            && e.Cursor == Cursor(102)));
    }

    [Fact]
    public async Task ARepeatedSkip_OnARetriedRequest_IsServed_ButNotEmailedAgain()
    {
        // The first attempt moved the position to 103 and its response was lost; the retry names 102 again.
        var h = Build(acknowledged: 103, issued: 103, rowCount: 5);

        var result = await ReadAsync(h, Cursor(103), Cursor(102));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.Page);
        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerFeedAlertEto>());
    }

    [Theory]
    [InlineData(104L)] // beyond the cursor being acknowledged
    [InlineData(100L)] // the floor itself: never a served row
    public async Task ASkipOutsideTheRange_RefusesTheWholeRequest(long skip)
    {
        var h = Build(acknowledged: 100, issued: 103, rowCount: 5);

        var result = await ReadAsync(h, Cursor(103), Cursor(102), Cursor(skip));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.SkipInvalid);
        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerFeedAlertEto>());
        await h.Store.DidNotReceiveWithAnyArgs().ReadPageAsync(default, default, default, default);
    }

    [Fact]
    public async Task ASkipNamingNoRow_IsRefused()
    {
        // In range, but no Pending row sits there (a Sent or Failed row, or a gap).
        var h = Build(acknowledged: 100, issued: 103, rowCount: 1);

        var result = await ReadAsync(h, Cursor(103), Cursor(102));

        result.Outcome.ShouldBe(CaseTrackerFeedOutcome.SkipInvalid);
    }
}
