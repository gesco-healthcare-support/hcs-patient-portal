namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Tuning + column limits for the Case Tracker outbox. Mirrors
/// <c>NotificationOutboxConsts</c>; the values differ where the delivery policy does.
/// </summary>
public static class IntegrationOutboxConsts
{
    /// <summary>Relative request path, e.g. <c>api/intake/appointments</c>.</summary>
    public const int TargetPathMaxLength = 256;

    /// <summary>SHA-256 hex is 64 chars; the cap leaves room without being unbounded.</summary>
    public const int IdempotencyKeyMaxLength = 128;

    public const int LastErrorMaxLength = 500;

    /// <summary>
    /// FAIL FAST, unlike the email outbox's 5. Three attempts spaced by
    /// <see cref="RetryBackoffSeconds"/> dead-letter a row roughly 10 minutes after the
    /// first failure. Rationale: an appointment that has not reached the Case Tracker is a
    /// case nobody is working, and case timelines carry legal weight -- notifying staff
    /// early beats retrying quietly for hours. Transient blips still self-heal inside the
    /// window.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Flat backoff before a failed row is retried by the next drain. Flat (not
    /// exponential) so the proven <c>MarkFailed</c> mechanics are reused verbatim.
    /// </summary>
    public const int RetryBackoffSeconds = 300;

    /// <summary>
    /// Visibility timeout for a claimed row; must comfortably exceed one HTTP attempt so a
    /// crashed drain's row becomes reclaimable rather than stuck.
    /// </summary>
    public const int LeaseDurationSeconds = 120;

    /// <summary>Max rows a single drain claims per office per pass.</summary>
    public const int DrainBatchSize = 50;
}
