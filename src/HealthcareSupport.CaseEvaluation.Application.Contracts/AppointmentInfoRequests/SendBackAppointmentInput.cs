using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Staff "Request info" payload: the note shown to the external user + the
/// fields they must fix. Moves the appointment Pending -&gt; InfoRequested.
/// </summary>
public class SendBackAppointmentInput
{
    [Required]
    [StringLength(AppointmentInfoRequestConsts.NoteMaxLength)]
    public string Note { get; set; } = string.Empty;

    public List<FlaggedFieldDto> FlaggedFields { get; set; } = new();
}
