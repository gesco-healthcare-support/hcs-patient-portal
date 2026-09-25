using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>What happened to an office's Case Tracker feed (#927).</summary>
public enum CaseTrackerFeedAlertKind
{
    /// <summary>No request from the office's consumer for the silence threshold: it looks dead.</summary>
    SilenceStarted = 0,

    /// <summary>Requests are arriving again after a silence alert.</summary>
    SilenceCleared = 1,

    /// <summary>Rows are waiting and the acknowledged position has not moved for the stall threshold.</summary>
    StallStarted = 2,

    /// <summary>The position is moving again, or nothing is waiting, after a stall alert.</summary>
    StallCleared = 3,

    /// <summary>The consumer sent a cursor beyond anything the feed ever issued; the request was refused.</summary>
    CursorAhead = 4,

    /// <summary>The consumer reported that it deliberately abandoned a row.</summary>
    SkipReported = 5,
}

/// <summary>
/// Raised by the feed and its health job (Domain); emailed to the technical recipient list by a handler in
/// Application, where the dispatcher lives. Carries ids, counts, times and a cursor -- never a payload, and no
/// patient field, so an alert cannot be the thing that puts PHI in an inbox.
/// </summary>
public class CaseTrackerFeedAlertEto
{
    public CaseTrackerFeedAlertKind Kind { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>From the tenant store, never <c>ICurrentTenant.Name</c>, which is null inside a changed scope.</summary>
    public string OfficeName { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    /// <summary>Silence alerts: when the consumer was last heard from; null if never since the feed started.</summary>
    public DateTime? LastRequestAt { get; set; }

    /// <summary>Stall alerts: rows waiting beyond the acknowledged position.</summary>
    public int? OutstandingCount { get; set; }

    /// <summary>Skip reports: the abandoned row's appointment.</summary>
    public Guid? AppointmentId { get; set; }

    /// <summary>Skip reports: the abandoned row's message type.</summary>
    public string? MessageType { get; set; }

    /// <summary>Skip reports and cursor-ahead refusals: the cursor involved, as the consumer sent it.</summary>
    public string? Cursor { get; set; }
}
