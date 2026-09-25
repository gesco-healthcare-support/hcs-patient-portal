namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// QA item 9: view-only per-practice metrics for the Intake operator's landing
/// page. Each office's counts are computed inside that office's own database
/// (IMultiTenant isolation); only aggregate counts leave the server (no PHI).
/// </summary>
public class IntakeOfficeMetricsDto
{
    public Guid OfficeId { get; set; }
    public string OfficeName { get; set; } = string.Empty;

    /// <summary>Pending appointment requests awaiting a decision.</summary>
    public int PendingRequests { get; set; }

    /// <summary>Appointments scheduled for today (UTC day).</summary>
    public int TodayAppointments { get; set; }

    /// <summary>Pending reschedule/cancel change-requests.</summary>
    public int PendingChangeRequests { get; set; }
}
