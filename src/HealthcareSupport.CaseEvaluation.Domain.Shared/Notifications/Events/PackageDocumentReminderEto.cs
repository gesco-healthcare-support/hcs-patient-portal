using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 14b (2026-05-04) -- raised by
/// <c>PackageDocumentReminderJob</c> for each outstanding package
/// document (Pending or Rejected) on an appointment whose
/// <c>DueDate</c> is at or past the configured
/// <c>Documents.PackageDocumentReminderDays</c> cutoff. Subscriber:
/// <c>PackageDocumentReminderEmailHandler</c>, which dispatches the
/// <c>PackageDocumentsReminder</c> template via Phase 18's
/// <c>INotificationDispatcher</c>.
///
/// <para>Distinct from <c>AppointmentDocumentUploadedEto</c> so the
/// upload handler does not fire for reminder events.</para>
/// </summary>
public class PackageDocumentReminderEto
{
    public Guid AppointmentId { get; set; }

    public Guid AppointmentDocumentId { get; set; }

    public Guid? TenantId { get; set; }

    public bool IsJointDeclaration { get; set; }

    public DateTime OccurredAt { get; set; }
}
