namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Delivery state of one outbound Case Tracker message. Mirrors
/// <c>NotificationOutboxStatus</c> so both ledgers read the same way.
///
/// <para><see cref="Failed"/> is terminal (a dead letter): it is reached either by
/// exhausting <c>MaxAttempts</c> on retryable errors, or immediately via
/// <c>IntegrationOutboxItem.MarkFatal</c> when the response says a retry can never
/// succeed (bad token, malformed payload). Terminal rows are surfaced to staff
/// rather than retried forever -- appointment timelines are legally significant, so
/// a stuck case must reach a human quickly.</para>
///
/// Numeric values start at 1 to avoid the C# <c>default(int) = 0</c> trap that would
/// map an unset enum to a valid state.
/// </summary>
public enum IntegrationOutboxStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3,

    /// <summary>
    /// A dead-lettered row a human has dealt with: staff hit Retry on the admin screen, which enqueued
    /// a FRESH push from current data rather than replaying this row's stored payload. Kept as an audit
    /// record but removed from the outstanding-failures list, so that list only ever shows work still
    /// needing attention.
    ///
    /// <para>Not leasable -- <c>TryClaim</c> admits only <see cref="Pending"/> -- so a resolved row can
    /// never be picked up and re-sent.</para>
    /// </summary>
    Resolved = 4,
}
