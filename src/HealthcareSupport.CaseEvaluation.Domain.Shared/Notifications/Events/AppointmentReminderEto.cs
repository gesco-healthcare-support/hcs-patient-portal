namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Group F (2026-06-09) -- raised by <c>AppointmentReminderJob</c> for each
/// active appointment whose <c>DueDate</c> falls on a configured reminder
/// anchor (default 14 / 7 / 3 days before due). Subscriber
/// <c>AppointmentReminderEmailHandler</c> sends ONE consolidated reminder (the
/// due-date nudge plus any outstanding documents) addressed To the booker with
/// the other parties + the office CC'd.
///
/// <para>Replaces the three prior reminder events
/// (<c>DueDateApproachingEto</c>, <c>DueDateDocumentIncompleteEto</c>,
/// <c>PackageDocumentReminderEto</c>). One per-appointment event removes the
/// up-to-five-emails-per-day redundancy of the old three-job model.</para>
/// </summary>
public class AppointmentReminderEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    public int DaysUntilDue { get; set; }

    public DateTime OccurredAt { get; set; }
}
