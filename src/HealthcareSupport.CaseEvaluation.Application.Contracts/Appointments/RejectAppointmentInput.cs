using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class RejectAppointmentInput
{
    /// <summary>
    /// BUG-024 (2026-05-19) -- the UI modal marks this field required with
    /// a 0/500 character counter, but the server-side DTO was previously
    /// nullable with only an upper-length cap. A direct API caller could
    /// submit `{ "reason": "" }` and the appointment would transition to
    /// Rejected with no audit-trail explanation. Required + 5-char floor
    /// keeps the data integrity contract aligned with the UI contract.
    /// 500 mirrors the UI counter; the 5-char floor keeps drive-by
    /// submissions like "x" out of the audit trail without being
    /// arbitrary.
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = null!;
}
