using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class RejectAppointmentInput
{
    [StringLength(2000)]
    public string? Reason { get; set; }
}
