namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Lifecycle of one Send Back round. Open while the external user still owes a
/// fix; Resolved once they resubmit. A new round opens a new row, so the table
/// keeps the full send-back history per appointment.
/// </summary>
public enum InfoRequestStatus
{
    Open = 1,
    Resolved = 2,
}
