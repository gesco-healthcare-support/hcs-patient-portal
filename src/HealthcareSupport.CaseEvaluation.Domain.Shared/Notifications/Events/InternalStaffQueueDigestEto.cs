namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- raised once per internal-staff
/// recipient per tenant per day by
/// <c>InternalStaffQueueDigestJob</c>. Subscriber:
/// <c>InternalStaffQueueDigestEmailHandler</c>, which dispatches the
/// OLD-parity <c>AppointmentApproveRejectInternal</c> template per
/// recipient.
///
/// <para>Mirrors OLD <c>SchedulerDomain.cs</c>:87 -- one row per
/// Staff Supervisor / Clinic Staff user with their tenant-wide
/// <see cref="PendingAppointmentCount"/> + <see cref="ApprovedAppointmentCount"/>.
/// SMS leg from OLD :105 is dropped per Phase 1 (no Twilio integration).</para>
/// </summary>
public class InternalStaffQueueDigestEto
{
    public Guid? TenantId { get; set; }

    public Guid StaffUserId { get; set; }

    public string StaffEmail { get; set; } = string.Empty;

    public string StaffFirstName { get; set; } = string.Empty;

    public int PendingAppointmentCount { get; set; }

    public int ApprovedAppointmentCount { get; set; }

    public DateTime OccurredAt { get; set; }
}
