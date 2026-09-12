using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// What applying an inbound attendance report produced, plus the appointment's current status when
/// that answer was <see cref="CaseTrackerAttendanceResult.Conflict"/>.
///
/// <para>A record rather than a bare <see cref="CaseTrackerAttendanceResult"/> so the controller can
/// tell the Case Tracker WHY a conflict happened and whether to call again. Value equality is
/// relied upon by the isolation test that asserts a disabled office and an unknown appointment
/// produce an indistinguishable answer.</para>
///
/// <para><see cref="CurrentStatus"/> is populated ONLY for a conflict. Both not-found causes leave
/// it null deliberately: that ambiguity is the property preventing a token holder from discovering
/// which offices and appointments exist, and a status here would leak exactly that.</para>
/// </summary>
/// <param name="Result">The coarse outcome the controller maps to a status code.</param>
/// <param name="CurrentStatus">The status that refused the transition; null unless conflicted.</param>
public sealed record CaseTrackerAttendanceOutcome(
    CaseTrackerAttendanceResult Result,
    AppointmentStatusType? CurrentStatus)
{
    /// <summary>The outcome was recorded, or was already present (an idempotent retry).</summary>
    public static CaseTrackerAttendanceOutcome Applied { get; } =
        new(CaseTrackerAttendanceResult.Applied, CurrentStatus: null);

    /// <summary>
    /// Unknown office, unknown appointment, or an office with the integration off -- deliberately
    /// indistinguishable from one another, so there is exactly one instance.
    /// </summary>
    public static CaseTrackerAttendanceOutcome NotFound { get; } =
        new(CaseTrackerAttendanceResult.NotFound, CurrentStatus: null);

    /// <summary>
    /// The appointment exists but its status refused the transition. Safe to disclose: by the time
    /// this can be returned the caller has presented a valid token AND the appointment is known to
    /// exist, so it reveals nothing the reconcile GET would not already serve them.
    /// </summary>
    public static CaseTrackerAttendanceOutcome Conflict(AppointmentStatusType currentStatus) =>
        new(CaseTrackerAttendanceResult.Conflict, currentStatus);

    /// <summary>Whether the same call can succeed later. Meaningful only for a conflict.</summary>
    public bool IsRetryable =>
        CurrentStatus.HasValue && AttendanceConflictPolicy.IsRetryable(CurrentStatus.Value);
}
