using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- input DTO for an external user submitting
/// a cancellation request on an Approved appointment.
/// </summary>
public class RequestCancellationDto
{
    /// <summary>
    /// Verbatim reason from the user. Required: OLD's
    /// <c>AppointmentChangeRequestDomain.cs:92-95</c> rejects empty
    /// reasons with <c>ProvideCancelReason</c>.
    /// </summary>
    [Required]
    [StringLength(AppointmentChangeRequestConsts.ReasonMaxLength)]
    public string Reason { get; set; } = null!;

    /// <summary>
    /// CancelledNoBill or CancelledLate. REQUIRED when the caller is internal staff (the
    /// cancellation then completes on its own once both sides agree, so staff pick the outcome
    /// when they file it); ignored and not stored for an external caller, whose request still
    /// waits for staff to choose it at approval.
    /// </summary>
    public AppointmentStatusType? CancellationOutcome { get; set; }
}
