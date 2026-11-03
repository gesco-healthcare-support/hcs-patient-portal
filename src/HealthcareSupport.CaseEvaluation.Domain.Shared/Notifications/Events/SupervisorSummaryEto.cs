namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Raised once per run by <c>StaffSummaryJob</c>, covering EVERY office, and handled in host scope by
/// <c>SupervisorSummaryEmailHandler</c>: one email per switched-on host Staff Supervisor, not one per
/// office. Offices with nothing outstanding are left out; no event is raised when none has anything.
/// </summary>
public class SupervisorSummaryEto
{
    public DateTime OccurredAt { get; set; }

    public List<SupervisorSummaryOffice> Offices { get; set; } = new();
}

/// <summary>One office's counts in the supervisor summary.</summary>
public class SupervisorSummaryOffice
{
    public string OfficeName { get; set; } = string.Empty;

    public StaffSummaryCounts Counts { get; set; } = new();
}
