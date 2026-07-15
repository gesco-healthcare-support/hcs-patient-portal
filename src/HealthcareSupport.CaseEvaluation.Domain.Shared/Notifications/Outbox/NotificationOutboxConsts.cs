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
}
