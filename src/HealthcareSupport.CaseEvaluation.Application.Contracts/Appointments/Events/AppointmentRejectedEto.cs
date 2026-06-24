using System;

namespace HealthcareSupport.CaseEvaluation.Appointments.Events;

/// <summary>
/// Phase 12 (2026-05-04) -- raised by
/// <c>AppointmentApprovalAppService.RejectAppointmentAsync</c> after the
/// state-machine transition has succeeded and the rejection notes
/// have been persisted on the appointment. Mirrors OLD's rejection
/// trigger point in
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:984-990
/// where <c>EmailTemplate.PatientAppointmentRejected</c> is sent to the
/// creator with the rejection reason.
///
/// <para>OLD does NOT auto-queue documents on rejection; only the
/// rejection email + stakeholder notifications fan out. NEW preserves
/// that contract -- <c>PackageDocumentQueueHandler</c> only subscribes
/// to <see cref="AppointmentApprovedEto"/>, not this Eto.</para>
/// </summary>
public class AppointmentRejectedEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Verbatim rejection reason supplied by the staff approver.
    /// Surfaces in the rejection email body. Required upstream by the
    /// AppService validator (`Check.NotNullOrWhiteSpace`).</summary>
    public string RejectionNotes { get; set; } = string.Empty;

    public Guid RejectedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
