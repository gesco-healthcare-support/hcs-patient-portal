using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- supervisor-side approve / reject surface
/// for the cancel + reschedule lifecycle. Sibling to the existing
/// <see cref="IAppointmentChangeRequestsAppService"/> (Phase 15+16
/// submit endpoints) -- the user's locked directive said "DO NOT
/// modify the existing AppointmentChangeRequestsAppService.cs file";
/// approval-side methods land on this new interface + a paired
/// <c>AppointmentChangeRequestsApprovalAppService</c> class in
/// <c>AppointmentChangeRequestsAppService.Approval.cs</c> per the
/// Session B file-ownership rule
/// (<c>memory/project_two-session-split.md</c>).
///
/// <para>All four mutation methods raise
/// <c>BusinessException(ChangeRequestAlreadyHandled)</c> when the
/// request is no longer Pending OR the optimistic-concurrency stamp
/// loses to a parallel supervisor's update.</para>
/// </summary>
public interface IAppointmentChangeRequestsApprovalAppService : IApplicationService
{
    /// <summary>
    /// Supervisor approves a Cancel change request with the chosen
    /// outcome bucket (CancelledNoBill / CancelledLate). Transitions
    /// the parent appointment to the chosen status; <see cref="SlotCascadeHandler"/>
    /// frees the slot via the published
    /// <c>AppointmentStatusChangedEto</c>; per-feature email handler
    /// dispatches the OLD-parity stakeholder notification.
    /// </summary>
    Task<AppointmentChangeRequestDto> ApproveCancellationAsync(
        Guid changeRequestId,
        ApproveCancellationInput input);

    /// <summary>
    /// Supervisor rejects a Cancel change request. Reverts parent
    /// appointment to Approved (no slot change -- the slot stayed
    /// Booked through the Pending lifecycle). Persists rejection
    /// notes for the requester email.
    /// </summary>
    Task<AppointmentChangeRequestDto> RejectCancellationAsync(
        Guid changeRequestId,
        RejectChangeRequestInput input);

    /// <summary>
    /// Supervisor approves a Reschedule change request. Optionally
    /// overrides the user-picked slot with their own + reason. On
    /// approve (B2 2026-07-01): the SAME appointment moves IN PLACE to
    /// the new slot -- keeping its confirmation number, child rows and
    /// audit trail. An Approved source returns to Approved and a Pending
    /// source stays Pending; the transient Reserved hold on the
    /// user-picked slot is released; the RescheduledNoBill/Late outcome
    /// is recorded on the change-request row (no appointment clone).
    /// </summary>
    Task<AppointmentChangeRequestDto> ApproveRescheduleAsync(
        Guid changeRequestId,
        ApproveRescheduleInput input);

    /// <summary>
    /// Supervisor rejects a Reschedule change request. Reverts parent
    /// appointment to Approved; releases the user-picked Reserved
    /// slot back to Available so other users can book it; persists
    /// rejection notes.
    /// </summary>
    Task<AppointmentChangeRequestDto> RejectRescheduleAsync(
        Guid changeRequestId,
        RejectChangeRequestInput input);

    /// <summary>
    /// Supervisor inbox: paged list of change requests filtered by
    /// status (defaults to Pending), type, and creation-date range.
    /// </summary>
    Task<PagedResultDto<AppointmentChangeRequestDto>> GetPendingChangeRequestsAsync(
        GetChangeRequestsInput input);
}
