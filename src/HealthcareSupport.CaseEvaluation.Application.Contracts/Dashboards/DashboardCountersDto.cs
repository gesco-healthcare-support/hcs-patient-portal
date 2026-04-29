namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// W2-6: 13-card dashboard counters DTO. 5 cards are populated from live
/// queries at MVP; the other 8 stay at zero until day-of-exam states + W3
/// AppointmentChangeRequest land.
///
/// Aggregates of PHI rows are not PHI under HIPAA Safe Harbor (no individual
/// identifier attached); per-tenant scope is applied server-side via the
/// tenant DataFilter for Tenant callers and explicitly disabled for Host
/// callers (cross-tenant aggregate view).
/// </summary>
public class DashboardCountersDto
{
    // 5 real (MVP)
    public int PendingRequests { get; set; }
    public int ApprovedThisWeek { get; set; }
    public int RejectedThisWeek { get; set; }
    public int PendingChangeRequests { get; set; }
    public int RequestsApproachingLegalDeadline { get; set; }

    // 8 placeholders (return 0 until corresponding caps land)
    public int BilledThisMonth { get; set; }
    public int NoShowThisMonth { get; set; }
    public int RescheduledThisMonth { get; set; }
    public int CancelledThisWeek { get; set; }
    public int CheckedInToday { get; set; }
    public int CheckedOutToday { get; set; }
    public int TotalDoctors { get; set; }
    public int TotalTenants { get; set; }
}
