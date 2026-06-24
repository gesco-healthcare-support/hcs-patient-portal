using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- read DTO for the cancel / reschedule
/// change request. Phase 16 reuses the same DTO for the reschedule
/// submit endpoint. Phase 17 (Session B) reuses for the supervisor
/// approve / reject endpoints.
/// </summary>
public class AppointmentChangeRequestDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public Guid AppointmentId { get; set; }

    /// <summary>
    /// Human-facing appointment confirmation number (e.g. "A00077") copied from
    /// the referenced appointment so the supervisor approval queues can display
    /// it instead of the raw appointment GUID. Read-only on this DTO; populated
    /// in <c>GetPendingChangeRequestsAsync</c>, not by the Mapperly mapper.
    /// </summary>
    public string? AppointmentConfirmationNumber { get; set; }

    public ChangeRequestType ChangeRequestType { get; set; }

    public string? CancellationReason { get; set; }

    public string? ReScheduleReason { get; set; }

    public Guid? NewDoctorAvailabilityId { get; set; }

    public RequestStatusType RequestStatus { get; set; }

    public string? RejectionNotes { get; set; }

    public Guid? RejectedById { get; set; }

    public Guid? ApprovedById { get; set; }

    public string? AdminReScheduleReason { get; set; }

    public Guid? AdminOverrideSlotId { get; set; }

    public bool IsBeyondLimit { get; set; }

    public AppointmentStatusType? CancellationOutcome { get; set; }

    /// <summary>
    /// Group D (2026-06-09): opposing-side consent state. Drives the supervisor
    /// queue buckets -- Pending = awaiting opposing-side consent; Approved = ready to
    /// finalize; Rejected/Expired = declined, needs staff mediation; NotRequired =
    /// routed straight to staff (no opposing party resolved / gating off).
    /// </summary>
    public ChangeRequestConsentStatus ConsentStatus { get; set; }

    /// <summary>Group D: which side submitted (the opposing side was solicited for consent).</summary>
    public ChangeRequestSide? RequestingSide { get; set; }
}
