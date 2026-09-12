using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- input DTO for
/// <c>RejectCancellationAsync</c> + <c>RejectRescheduleAsync</c>.
/// Mirrors OLD's <c>CancellationRejectionReason</c> /
/// <c>ReScheduleRejectionReason</c> required-field gate: rejection
/// notes must be supplied so the requester knows why their request
/// was denied.
/// </summary>
public class RejectChangeRequestInput
{
    [Required]
    [StringLength(2000)]
    public string Reason { get; set; } = null!;

    [CanBeNull]
    public string? ConcurrencyStamp { get; set; }
}
