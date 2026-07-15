namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// Column bounds + defaults for <c>NotificationOutboxItem</c> (Phase 2 email ledger).
/// Body + Cc are unbounded (nvarchar(max)) because a rendered HTML email and a
/// multi-party CC list can be large; the rest are bounded.
/// </summary>
public static class NotificationOutboxConsts
{
    public const int ToMaxLength = 256;
    public const int SubjectMaxLength = 512;
    public const int ContextMaxLength = 256;

    // SHA-256 hex digest is exactly 64 chars (see SendAppointmentEmailArgs.BuildIdempotencyKey).
    public const int IdempotencyKeyMaxLength = 64;
    public const int LastErrorMaxLength = 2000;

    /// <summary>Default retry budget (Hangfire-parity) before a row dead-letters.</summary>
    public const int DefaultMaxAttempts = 5;

    /// <summary>
    /// Visibility timeout for a claimed row. Must comfortably exceed one send
    /// attempt (including a packet-attachment blob fetch) so a crashed drain's
    /// row becomes reclaimable, but not so long that recovery stalls.
    /// </summary>
    public const int LeaseDurationSeconds = 120;

    /// <summary>Backoff before a failed row is retried by the next drain.</summary>
    public const int RetryBackoffSeconds = 300;

    /// <summary>Max rows a single drain claims per office per pass.</summary>
    public const int DrainBatchSize = 50;
}
