namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- raised by
/// <c>DueDateApproachingJob</c> for each appointment whose
/// <c>DueDate</c> falls at one of the configured reminder windows
/// (14 / 7 / 3 days before due date). Subscriber:
/// <c>DueDateApproachingEmailHandler</c>, which dispatches the
/// OLD-parity <c>AppointmentDueDateReminder</c> template to every
/// stakeholder via <c>IAppointmentRecipientResolver</c>.
///
/// <para>Mirrors OLD <c>SchedulerDomain.cs</c>:152. OLD's stored proc
/// determined the date window host-side; NEW uses fixed 14/7/3-day
/// windows per Adrian Decision (2026-05-10).</para>
/// </summary>
public class DueDateApproachingEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    public int DaysUntilDue { get; set; }

    public DateTime OccurredAt { get; set; }
}
