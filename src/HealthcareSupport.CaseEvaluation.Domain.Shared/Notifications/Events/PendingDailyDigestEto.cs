namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- raised once per tenant per day by
/// <c>PendingDailyDigestJob</c>. Subscriber:
/// <c>PendingDailyDigestEmailHandler</c>, which renders a digest body of
/// <see cref="Rows"/> and dispatches the OLD-parity
/// <c>PendingAppointmentDailyNotification</c> template to the tenant's
/// intake-staff inbox.
///
/// <para>Mirrors OLD <c>SchedulerDomain.cs</c>:72 -- the proc result was
/// a pre-rendered HTML block (<c>DailyNotificationContent</c>); NEW
/// renders the digest in the handler so the template-variable surface
/// matches the rest of the dispatcher pattern.</para>
/// </summary>
public class PendingDailyDigestEto
{
    public Guid? TenantId { get; set; }

    public List<PendingDailyDigestRow> Rows { get; set; } = new();

    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// One row per pending appointment in the daily digest.
/// </summary>
public class PendingDailyDigestRow
{
    public string RequestConfirmationNumber { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    public DateTime AppointmentDate { get; set; }

    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Group F (2026-06-09): when the request was booked (the appointment's
    /// ABP <c>CreationTime</c>). The handler renders the "Decision due" column
    /// as this date + the decision window (5 days), so staff see the deadline
    /// to approve/reject each pending request.
    /// </summary>
    public DateTime RequestedAt { get; set; }
}
