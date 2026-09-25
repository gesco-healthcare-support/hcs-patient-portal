using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed record's two operator transitions (#927). Pure: no database.
/// </summary>
public class CaseTrackerFeedStateTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_OnANewRecord_ActivatesItWithEveryPositionAtTheFloor()
    {
        var state = new CaseTrackerFeedState(Guid.NewGuid(), Guid.NewGuid());

        state.Start(100, Now);

        state.IsActive.ShouldBeTrue();
        state.FloorPosition.ShouldBe(100);
        state.AcknowledgedPosition.ShouldBe(100);
        state.HighestIssuedPosition.ShouldBe(100);
        state.StartedAt.ShouldBe(Now);
        state.LastAdvancedAt.ShouldBe(Now); // the stall clock starts at cutover, not at the epoch
        state.LastRequestAt.ShouldBeNull(); // silence is measured from StartedAt until the first request
    }

    [Fact]
    public void Start_OnAnActiveRecord_Throws_AndChangesNothing()
    {
        var state = new CaseTrackerFeedState(Guid.NewGuid(), Guid.NewGuid());
        state.Start(100, Now);

        Should.Throw<InvalidOperationException>(() => state.Start(500, Now.AddHours(1)));

        state.FloorPosition.ShouldBe(100);
        state.StartedAt.ShouldBe(Now);
    }

    [Fact]
    public void ReturnToPush_DeactivatesAndClearsOpenAlerts_ButKeepsThePositions()
    {
        var state = new CaseTrackerFeedState(Guid.NewGuid(), Guid.NewGuid());
        state.Start(100, Now);

        state.ReturnToPush(Now.AddHours(2));

        state.IsActive.ShouldBeFalse();
        state.StoppedAt.ShouldBe(Now.AddHours(2));
        state.FloorPosition.ShouldBe(100);
        state.SilenceAlertedAt.ShouldBeNull();
        state.StallAlertedAt.ShouldBeNull();
    }

    [Fact]
    public void ReturnToPush_OnAnInactiveRecord_Throws()
    {
        var state = new CaseTrackerFeedState(Guid.NewGuid(), Guid.NewGuid());

        Should.Throw<InvalidOperationException>(() => state.ReturnToPush(Now));
    }

    [Fact]
    public void Start_AfterReturningToPush_StartsAFreshIncidentFreeRecordAtTheNewFloor()
    {
        var state = new CaseTrackerFeedState(Guid.NewGuid(), Guid.NewGuid());
        state.Start(100, Now);
        state.ReturnToPush(Now.AddHours(1));

        state.Start(900, Now.AddHours(2));

        state.IsActive.ShouldBeTrue();
        state.FloorPosition.ShouldBe(900);
        state.AcknowledgedPosition.ShouldBe(900);
        state.StoppedAt.ShouldBeNull();
    }
}
