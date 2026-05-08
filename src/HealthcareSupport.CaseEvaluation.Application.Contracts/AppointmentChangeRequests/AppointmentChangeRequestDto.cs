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
}
