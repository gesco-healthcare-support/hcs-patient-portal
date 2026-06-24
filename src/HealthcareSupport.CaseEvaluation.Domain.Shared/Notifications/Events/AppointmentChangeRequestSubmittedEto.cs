using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised when an external user submits a cancel
/// or reschedule change request. Mirrors OLD
/// <c>TemplateCode.AppointmentCancelledRequest</c> /
/// <c>AppointmentRescheduleRequest</c> + the matching
/// <c>EmailTemplate.PatientAppointmentRescheduleReq</c> triggers.
///
/// <para>Phase 15 (Cancellation submit) and Phase 16 (Reschedule submit)
/// emit this. The <see cref="ChangeRequestType"/> tag lets a single
/// handler resolve the right template code from the
/// (Cancel/Reschedule) x (Submitted/Approved/Rejected) lookup table
/// without requiring 6 separate Etos.</para>
/// </summary>
public class AppointmentChangeRequestSubmittedEto
{
    public Guid AppointmentId { get; set; }

    public Guid ChangeRequestId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>1 = Cancel, 2 = Reschedule. See <c>ChangeRequestType</c> enum.</summary>
    public ChangeRequestType ChangeRequestType { get; set; }

    public Guid SubmittedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
