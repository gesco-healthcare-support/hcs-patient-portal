using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// G-02-05 (2026-06-01) -- input for a one-step internal-staff cancel of an
/// Approved appointment (OLD AppointmentDomain.Update CancelledNoBill branch).
/// <see cref="CancellationOutcome"/> must be
/// <see cref="AppointmentStatusType.CancelledNoBill"/> or
/// <see cref="AppointmentStatusType.CancelledLate"/>. <see cref="Reason"/> is
/// required so the audit trail records why the appointment was cancelled,
/// mirroring the Reject contract (5..500 chars).
/// </summary>
public class DirectCancelAppointmentInput
{
    [Required]
    public AppointmentStatusType CancellationOutcome { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = null!;
}
