using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Background-job payload for <c>SendAppointmentEmailJob</c>. Carries a fully
/// rendered email so the job worker only needs SMTP credentials, not domain
/// repositories.
/// </summary>
[Serializable]
public class SendAppointmentEmailArgs
{
    public string To { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsBodyHtml { get; set; } = true;

    /// <summary>
    /// Free-text label for log correlation. Conventional shape:
    /// "Submission/Office/{appointmentId}", "Transition/Approved/{appointmentId}", etc.
    /// </summary>
    public string Context { get; set; } = string.Empty;
}
