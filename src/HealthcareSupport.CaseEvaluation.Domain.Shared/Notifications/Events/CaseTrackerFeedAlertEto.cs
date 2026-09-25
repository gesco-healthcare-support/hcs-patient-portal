using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// What happened on an office's Case Tracker integration.
///
/// <para>Named for the feed because that is what it first carried (#927). It now also carries the inbound
/// attendance path (#1043), which shares the same recipients, the same "never a patient field" rule and the
/// same handler, so a second parallel alert type would have duplicated all three to say one more thing.</para>
/// </summary>
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

    /// <summary>
    /// An INBOUND attendance report was refused (#1043). Nothing else records this: the outbox and its
    /// failure alert cover outbound pushes only, and the refusal answers a bodyless 404 the caller cannot
    /// act on.
    /// </summary>
    InboundAttendanceRefused = 6,
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

    /// <summary>
    /// Inbound attendance refusals: why it was refused. The alert says which; the 404 sent to the caller
    /// stays ambiguous.
    /// </summary>
    public CaseTrackerInboundRefusalReason? InboundRefusalReason { get; set; }

    /// <summary>Inbound attendance refusals: the outcome the Case Tracker was trying to record.</summary>
    public string? RequestedOutcome { get; set; }
}
