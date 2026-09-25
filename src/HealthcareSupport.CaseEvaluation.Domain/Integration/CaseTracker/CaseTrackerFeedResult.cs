using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>How a feed request ended (#927). The controller maps each to its status and error code.</summary>
public enum CaseTrackerFeedOutcome
{
    /// <summary>A page was served (possibly empty).</summary>
    Page = 0,

    /// <summary>The office is not on the feed, or is unknown -- deliberately the same answer (403).</summary>
    FeedNotEnabled = 1,

    /// <summary>The cursor is not a cursor this feed writes (409).</summary>
    CursorInvalid = 2,

    /// <summary>The cursor is below the floor set at cutover (409).</summary>
    CursorBelowFloor = 3,

    /// <summary>The cursor is beyond anything the feed has issued (409; alerted).</summary>
    CursorAhead = 4,

    /// <summary>A reported skip does not name a row inside the range being acknowledged (409).</summary>
    SkipInvalid = 5,
}

/// <summary>The feed's answer to one request (#927). Only <see cref="CaseTrackerFeedOutcome.Page"/> carries rows.</summary>
public sealed record CaseTrackerFeedResult(
    CaseTrackerFeedOutcome Outcome,
    IReadOnlyList<CaseTrackerFeedRow> Rows,
    string? NextCursor,
    bool HasMore)
{
    public static CaseTrackerFeedResult Refused(CaseTrackerFeedOutcome outcome) =>
        new(outcome, [], null, false);

    public static CaseTrackerFeedResult Served(IReadOnlyList<CaseTrackerFeedRow> rows, long nextPosition, bool hasMore) =>
        new(CaseTrackerFeedOutcome.Page, rows, CaseTrackerFeedCursor.Encode(nextPosition), hasMore);
}
