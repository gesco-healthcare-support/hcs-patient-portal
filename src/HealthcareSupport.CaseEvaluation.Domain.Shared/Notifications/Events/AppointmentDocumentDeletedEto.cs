using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Part 2 (2026-07-28) -- raised when an appointment document row is deleted by
/// <c>AppointmentDocumentsAppService.DeleteAsync</c>. Added so the Case Tracker integration can
/// publish a tombstone; the portal itself sends no email on deletion.
///
/// <para>Deletion is soft (ABP <c>ISoftDelete</c>) and the MinIO object is deliberately RETAINED --
/// the portal guarantees retention so an 18-month re-evaluation can still reference the file. This
/// event therefore means "stop showing it", not "the bytes are gone".</para>
/// </summary>
public class AppointmentDocumentDeletedEto
{
    public Guid AppointmentId { get; set; }

    public Guid AppointmentDocumentId { get; set; }

    public Guid? TenantId { get; set; }

    public Guid DeletedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
