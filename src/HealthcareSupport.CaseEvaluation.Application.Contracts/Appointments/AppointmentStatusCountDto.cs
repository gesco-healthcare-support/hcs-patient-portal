using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Redesign (Prompt 10, 2026-06-14): one row of the internal appointments
/// list's per-status counts -- a raw <see cref="AppointmentStatusType"/> and
/// how many appointments hold it within the caller's visibility + the active
/// filters (the status filter itself excluded). The Angular list buckets these
/// raw counts into the six UI pills via <c>appointmentStatusToPill</c>, so the
/// chip totals stay in lockstep with the rows the table actually shows.
/// </summary>
public class AppointmentStatusCountDto
{
    public AppointmentStatusType Status { get; set; }

    public int Count { get; set; }
}
