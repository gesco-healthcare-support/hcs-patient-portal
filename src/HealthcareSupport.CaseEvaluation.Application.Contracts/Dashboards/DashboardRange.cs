namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Time-frame for the dashboard's period-based sections: the Approved / Rejected
/// KPIs (with prior-period deltas), the status-breakdown donut, the trend chart,
/// and recent activity all follow the range. Point-in-time metrics (live Pending
/// counts, decision deadlines, today's schedule) ignore it.
/// </summary>
public enum DashboardRange
{
    Week = 0,
    Month = 1,
    Quarter = 2,
}
