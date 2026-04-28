using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Input for the office's send-back-for-info action. Carries the structured list
/// of appointment-form fields the office wants the booker to revisit (W1: free-text
/// field-name strings; future W2 custom-fields cap upgrades to a typed registry)
/// plus a freeform note shown alongside the flagged fields on the booker's
/// AwaitingMoreInfo response screen.
/// </summary>
public class SendBackAppointmentInput
{
    public List<string> FlaggedFields { get; set; } = new();

    [StringLength(2000)]
    public string? Note { get; set; }
}
