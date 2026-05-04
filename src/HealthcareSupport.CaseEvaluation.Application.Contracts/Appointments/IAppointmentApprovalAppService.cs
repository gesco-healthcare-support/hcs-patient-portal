using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 12 (2026-05-04) -- richer Approve / Reject surface that captures
/// the full audit-required approval context (responsible user,
/// patient-match decision, required rejection notes) and publishes
/// Phase-12-specific events
/// (<see cref="Events.AppointmentApprovedEto"/>,
/// <see cref="Events.AppointmentRejectedEto"/>) for the
/// package-document-queue + email handlers to subscribe to.
///
/// <para>This is a sibling to the existing thinner
/// <c>IAppointmentsAppService.ApproveAsync(Guid)</c> /
/// <c>RejectAsync(Guid, RejectAppointmentInput)</c> that Session A built
/// earlier in Phase 11. The user's Phase 12 directive locked the surface
/// in a NEW file (<c>AppointmentsAppService.Approval.cs</c>) without
/// touching the main <c>AppointmentsAppService.cs</c> -- a new
/// AppService class + interface honor that constraint without forcing
/// a partial-class declaration on the main file. Both surfaces will
/// eventually converge in a Sync-3 cleanup PR after Session A's manager
/// rewrite lands.</para>
/// </summary>
public interface IAppointmentApprovalAppService : IApplicationService
{
    /// <summary>
    /// Pending -> Approved. Sets <c>PrimaryResponsibleUserId</c>,
    /// records the patient-match decision, fires the state-machine
    /// transition (Session A's <c>AppointmentManager.ApproveAsync</c>),
    /// and publishes <see cref="Events.AppointmentApprovedEto"/>.
    /// Throws <c>BusinessException(AppointmentApprovalRequiresResponsibleUser)</c>
    /// when <see cref="ApproveAppointmentInput.PrimaryResponsibleUserId"/>
    /// is the default Guid. Throws
    /// <c>BusinessException(AppointmentNotPendingForApproval)</c> when
    /// the appointment's current status is not <c>Pending</c> -- with
    /// OLD's verbatim "Appointment Already Approved" / "Appointment
    /// Already Rejected" message body when the current status is
    /// Approved / Rejected respectively.
    /// </summary>
    Task<AppointmentDto> ApproveAppointmentAsync(Guid id, ApproveAppointmentInput input);

    /// <summary>
    /// Pending -> Rejected. Persists <c>RejectionNotes</c> +
    /// <c>RejectedById</c>, fires the state-machine transition, and
    /// publishes <see cref="Events.AppointmentRejectedEto"/>. Throws
    /// <c>BusinessException(AppointmentRejectionRequiresNotes)</c>
    /// when <see cref="RejectAppointmentInput.Reason"/> is null or
    /// whitespace -- OLD's UI required the rejection-notes textarea
    /// before enabling the Reject button. Throws
    /// <c>BusinessException(AppointmentNotPendingForRejection)</c>
    /// when the appointment is not in <c>Pending</c> status.
    /// </summary>
    Task<AppointmentDto> RejectAppointmentAsync(Guid id, RejectAppointmentInput input);
}
