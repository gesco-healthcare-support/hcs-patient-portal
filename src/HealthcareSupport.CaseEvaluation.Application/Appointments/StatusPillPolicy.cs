using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-06-14 -- buckets <see cref="AppointmentStatusType"/> into the UI pills the
/// redesigned dashboard donut + lists use (Pending, Info Requested, Approved,
/// Rescheduled, Cancelled, No Show, Not Seen, Rejected). Mirrors the Angular
/// <c>appointmentStatusToPill</c> util so the backend breakdown and the UI agree.
///
/// <para>Phase 5 (2026-08-07) added the two attendance outcomes. They previously
/// disagreed across the boundary this class exists to keep in step: NoShow returned
/// null HERE while the Angular util mapped it onto the Cancelled pill, so the same
/// appointment was absent from the donut and mislabelled "Cancelled" in the lists.
/// Any future status must be added on BOTH sides or the same split reappears.</para>
///
/// <para>Statuses with no dashboard pill -- the in-flight "Requested" states
/// (RescheduleRequested / CancellationRequested, surfaced via the change-request
/// counter) and the legacy day-of-exam states (CheckedIn / CheckedOut / Billed) --
/// return null and are excluded from the donut. Pure + internal so it is
/// unit-testable via the existing InternalsVisibleTo wiring.</para>
/// </summary>
internal static class StatusPillPolicy
{
    internal const string Pending = "Pending";
    internal const string InfoRequested = "InfoRequested";
    internal const string Approved = "Approved";
    internal const string Rescheduled = "Rescheduled";
    internal const string Cancelled = "Cancelled";
    internal const string Rejected = "Rejected";

    /// <summary>
    /// Phase 5 (2026-08-07). The two attendance outcomes get a pill EACH rather than
    /// one shared bucket: "No Show" and "Not Seen" are long-standing names in the
    /// business, so the UI uses them verbatim instead of inventing a merged label.
    /// </summary>
    internal const string NoShow = "NoShow";
    internal const string NotSeen = "NotSeen";

    /// <summary>
    /// Donut slice order, matching the prototype DH_STATUS. The two attendance
    /// outcomes sit beside <see cref="Cancelled"/> because they are the same family
    /// of terminal non-event -- the appointment produced no evaluation.
    /// </summary>
    internal static readonly string[] DonutOrder =
    {
        Pending, InfoRequested, Approved, Rescheduled, Cancelled, NoShow, NotSeen, Rejected,
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
        AppointmentStatusType.NoShow => NoShow,
        AppointmentStatusType.NotSeen => NotSeen,
        _ => null,
    };
}
