using System;
using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.Enums;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- input DTO for
/// <c>IAppointmentChangeRequestsApprovalAppService.ApproveRescheduleAsync</c>.
/// Mirrors OLD's supervisor reschedule-approval surface where the
/// supervisor can either accept the user-picked slot or override with
/// a different slot + admin reason. OLD persists both fields on the
/// AppointmentChangeRequest row
/// (<c>AdminOverrideSlotId</c> + <c>AdminReScheduleReason</c>) for the
/// audit trail.
/// </summary>
public class ApproveRescheduleInput
{
    /// <summary>
    /// Supervisor-selected outcome bucket. Must be
    /// <see cref="AppointmentStatusType.RescheduledNoBill"/> or
    /// <see cref="AppointmentStatusType.RescheduledLate"/>.
    /// </summary>
    [Required]
    public AppointmentStatusType RescheduleOutcome { get; set; }

    /// <summary>
    /// Optional override slot id. When set AND different from the
    /// user-picked slot on the change request, the supervisor must
    /// also supply <see cref="AdminReScheduleReason"/>; the
    /// validator throws
    /// <c>BusinessException(ChangeRequestAdminReasonRequired)</c>
    /// otherwise.
    /// </summary>
    public Guid? OverrideSlotId { get; set; }

    /// <summary>
    /// Reason the supervisor overrode the user-picked slot. Required
    /// when <see cref="OverrideSlotId"/> differs from the user's
    /// pick; persisted on the change-request row for audit.
    /// </summary>
    [CanBeNull]
    [StringLength(2000)]
    public string? AdminReScheduleReason { get; set; }

    /// <summary>
    /// Optional ABP optimistic-concurrency stamp. See
    /// <see cref="ApproveCancellationInput.ConcurrencyStamp"/>.
    /// </summary>
    [CanBeNull]
    public string? ConcurrencyStamp { get; set; }
}
