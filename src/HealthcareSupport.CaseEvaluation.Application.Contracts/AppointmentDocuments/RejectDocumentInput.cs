using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// W2-11: payload for rejecting an uploaded document. Reason is required
/// (so the booker has actionable feedback) and capped to match OLD's
/// RejectionNotes column.
/// </summary>
public class RejectDocumentInput
{
    [Required]
    [StringLength(AppointmentPacketConsts.RejectionReasonMaxLength)]
    public string Reason { get; set; } = string.Empty;
}
