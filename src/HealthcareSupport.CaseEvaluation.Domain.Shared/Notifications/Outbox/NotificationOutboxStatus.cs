namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// Delivery state of a notification outbox row (Phase 2 durable email ledger).
///   Pending - not yet delivered; eligible for a drain claim once due.
///   Sent    - SMTP delivery confirmed; terminal + idempotent (never re-sent).
///   Failed  - retry budget exhausted; terminal dead-letter (visible for triage).
/// </summary>
public enum NotificationOutboxStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3,
}
