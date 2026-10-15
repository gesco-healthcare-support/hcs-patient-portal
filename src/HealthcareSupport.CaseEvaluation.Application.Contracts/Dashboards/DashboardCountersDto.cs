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

    // 2026-06-11: Pending requests past the per-tenant decision deadline
    // (PendingAppointmentOverDueNotificationDays, default 3). Escalate / notify
    // only -- no automatic status change.
    public int DecisionOverdue { get; set; }

    public int TotalDoctors { get; set; }
    public int TotalTenants { get; set; }
}
