using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised when Staff Supervisor approves a cancel
/// or reschedule change request. Mirrors OLD
/// <c>TemplateCode.AppointmentCancelledRequestApproved</c> /
/// <c>AppointmentRescheduleRequestApproved</c> + the matching
/// <c>EmailTemplate.Patient...</c> triggers (and
/// <c>AppointmentRescheduleRequestByAdmin</c> when the supervisor overrode
/// the user-picked slot).
///
/// <para>Phase 17 (Change-request approval) emits this from
/// <c>AppointmentChangeRequestsAppService.ApproveCancellationAsync</c> /
/// <c>ApproveRescheduleAsync</c>.</para>
/// </summary>
public class AppointmentChangeRequestApprovedEto
{
    public Guid AppointmentId { get; set; }

    public Guid ChangeRequestId { get; set; }

    public Guid? TenantId { get; set; }

    public ChangeRequestType ChangeRequestType { get; set; }

    /// <summary>
    /// CancelledNoBill / CancelledLate / RescheduledNoBill / RescheduledLate
    /// (per the supervisor's outcome bucket selection at approval time).
    /// </summary>
    public AppointmentStatusType Outcome { get; set; }

    /// <summary>
    /// True when the supervisor overrode the user-picked slot and supplied
    /// an admin reason. Drives the
    /// <c>AppointmentRescheduleRequestByAdmin</c> branch in handlers.
    /// </summary>
    public bool IsAdminOverride { get; set; }

    public Guid ApprovedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
