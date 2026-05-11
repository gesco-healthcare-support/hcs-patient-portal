namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- raised by
/// <c>DueDateDocumentIncompleteJob</c> for each appointment whose
/// <c>DueDate</c> is approaching AND has outstanding (Pending or
/// Rejected) documents. Subscriber:
/// <c>DueDateDocumentIncompleteEmailHandler</c>, which dispatches the
/// OLD-parity <c>AppointmentDocumentIncomplete</c> template to every
/// stakeholder via <c>IAppointmentRecipientResolver</c>.
///
/// <para>Mirrors OLD <c>SchedulerDomain.cs</c>:176 -- date-driven
/// reminder distinct from the status-driven
/// <c>PackageDocumentReminderJob</c> (Reminder #3). Different template
/// (Incomplete vs UploadPendingDocuments) signals "due-date close + docs
/// outstanding" vs "docs outstanding regardless of date".</para>
/// </summary>
public class DueDateDocumentIncompleteEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    public int DaysUntilDue { get; set; }

    public string PendingDocList { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}
