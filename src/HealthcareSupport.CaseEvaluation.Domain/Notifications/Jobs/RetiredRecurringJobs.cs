namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Hangfire recurring-job ids whose job types no longer exist. Hangfire persists a registration by its
/// id, so removing a job class leaves the stored entry behind, firing at a type that is gone; the host
/// module calls <c>RecurringJob.RemoveIfExists</c> for each id here at start-up to purge it.
/// </summary>
public static class RetiredRecurringJobs
{
    /// <summary>
    /// 2026-11-03: the 09:00 pending digest and the 09:15 per-staff queue-counts email, replaced by the
    /// twice-daily <c>StaffSummaryJob</c>.
    /// </summary>
    public static readonly string[] Ids =
    {
        "appt-pending-daily-digest",
        "appt-internal-staff-queue-digest",
    };
}
