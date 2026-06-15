namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Time-frame for the dashboard's period-based KPIs (Approved / Rejected) and
/// their prior-period deltas. Snapshot metrics (live Pending counts, status
/// breakdown, deadlines, schedule, activity) ignore the range.
/// </summary>
public enum DashboardRange
{
    Week = 0,
    Month = 1,
    Quarter = 2,
}
