using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-06-14 -- buckets the 14-value <see cref="AppointmentStatusType"/> into
/// the six UI pills the redesigned dashboard donut + lists use (Pending, Info
/// Requested, Approved, Rescheduled, Cancelled, Rejected). Mirrors the Angular
/// <c>appointmentStatusToPill</c> util so the backend breakdown and the UI agree.
///
/// Statuses with no dashboard pill -- the in-flight "Requested" states
/// (RescheduleRequested / CancellationRequested, surfaced via the change-request
/// counter) and the legacy day-of-exam states (NoShow / CheckedIn / CheckedOut /
/// Billed) -- return null and are excluded from the donut. Pure + internal so it
/// is unit-testable via the existing InternalsVisibleTo wiring.
/// </summary>
internal static class StatusPillPolicy
{
    internal const string Pending = "Pending";
    internal const string InfoRequested = "InfoRequested";
    internal const string Approved = "Approved";
    internal const string Rescheduled = "Rescheduled";
    internal const string Cancelled = "Cancelled";
    internal const string Rejected = "Rejected";

    /// <summary>Donut slice order, matching the prototype DH_STATUS.</summary>
    internal static readonly string[] DonutOrder =
    {
        Pending, InfoRequested, Approved, Rescheduled, Cancelled, Rejected,
    };

    /// <summary>The UI pill for a status, or null when the status has no donut pill.</summary>
    internal static string? ToPill(AppointmentStatusType status) => status switch
    {
        AppointmentStatusType.Pending => Pending,
        AppointmentStatusType.InfoRequested => InfoRequested,
        AppointmentStatusType.Approved => Approved,
        AppointmentStatusType.Rejected => Rejected,
        AppointmentStatusType.CancelledNoBill or AppointmentStatusType.CancelledLate => Cancelled,
        AppointmentStatusType.RescheduledNoBill or AppointmentStatusType.RescheduledLate => Rescheduled,
        _ => null,
    };
}
