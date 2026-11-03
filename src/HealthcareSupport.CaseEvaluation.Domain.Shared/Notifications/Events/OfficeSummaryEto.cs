namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Raised once per office per run by <c>StaffSummaryJob</c> (08:00 and 16:00 Pacific) when the office has
/// something outstanding. Subscriber: <c>OfficeSummaryEmailHandler</c>, which sends ONE email to the
/// office notification inbox plus the office's switched-on Intake Staff. Replaces the two former daily
/// digests: the pending-request list (<see cref="Rows"/>) and the queue counts (<see cref="Counts"/>) now
/// travel in one event, so they arrive in one email.
/// </summary>
public class OfficeSummaryEto
{
    public Guid? TenantId { get; set; }

    public string OfficeName { get; set; } = string.Empty;

    /// <summary>Run time (UTC). Names the run in the context tag, so the 16:00 run is its own email.</summary>
    public DateTime OccurredAt { get; set; }

    public StaffSummaryCounts Counts { get; set; } = new();

    /// <summary>One row per Pending appointment, soonest appointment date first.</summary>
    public List<OfficeSummaryRow> Rows { get; set; } = new();

    /// <summary>The office's decision window in days, quoted in the overdue banner.</summary>
    public int DecisionDueDays { get; set; }
}

/// <summary>The queue counts shared by the office summary and the supervisor summary.</summary>
public class StaffSummaryCounts
{
    public int Pending { get; set; }

    /// <summary>Pending requests past the office's decision deadline (same rule as the dashboard tile).</summary>
    public int PendingOverdue { get; set; }

    public int Approved { get; set; }

    public int RescheduleRequested { get; set; }

    public int CancellationRequested { get; set; }

    public int InfoRequested { get; set; }

    /// <summary>True when there is something staff must act on; Approved alone is information.</summary>
    public bool HasOutstanding =>
        Pending > 0 || RescheduleRequested > 0 || CancellationRequested > 0 || InfoRequested > 0;
}

/// <summary>One Pending appointment in the office summary.</summary>
public class OfficeSummaryRow
{
    public string RequestConfirmationNumber { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    public DateTime AppointmentDate { get; set; }

    public DateTime? DueDate { get; set; }

    /// <summary>The Pacific calendar date by which the request must be decided.</summary>
    public DateTime DecisionDue { get; set; }

    public bool IsOverdue { get; set; }
}
