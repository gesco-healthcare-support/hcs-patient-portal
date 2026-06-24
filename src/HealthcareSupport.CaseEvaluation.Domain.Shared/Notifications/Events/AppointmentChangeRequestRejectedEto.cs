using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised when Staff Supervisor rejects a cancel
/// or reschedule change request. Mirrors OLD
/// <c>TemplateCode.AppointmentCancelledRequestRejected</c> /
/// <c>AppointmentRescheduleRequestRejected</c> + matching
/// <c>EmailTemplate.PatientAppointmentRescheduleReqRejected</c> trigger.
///
/// <para>Phase 17 emits this from
/// <c>RejectCancellationAsync</c> / <c>RejectRescheduleAsync</c>. The
/// rejection notes are required and surface in the email body.</para>
/// </summary>
public class AppointmentChangeRequestRejectedEto
{
    public Guid AppointmentId { get; set; }

    public Guid ChangeRequestId { get; set; }

    public Guid? TenantId { get; set; }

    public ChangeRequestType ChangeRequestType { get; set; }

    public string RejectionNotes { get; set; } = string.Empty;

    public Guid RejectedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
