namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The outcome of applying an inbound attendance report (phase 5, 2026-08-07).
///
/// <para>THREE values, deliberately, rather than the nullable
/// <see cref="CaseTrackerReconcileService"/> returns. That service collapses every failure to null
/// so an anonymous caller cannot tell an unknown appointment from a disabled office; copying its
/// shape here would make <see cref="Conflict"/> unreachable, because an invalid transition throws
/// and a catch-all would report it as <see cref="NotFound"/>.</para>
///
/// <para><see cref="NotFound"/> keeps that ambiguity exactly. Only <see cref="Conflict"/> is
/// distinguishable, and it is safe to distinguish: by the time it can be returned the caller has
/// already presented a valid token, and staying silent would break the very log this phase exists
/// to create.</para>
/// </summary>
public enum CaseTrackerAttendanceResult
{
    /// <summary>
    /// The appointment now carries the reported outcome. Also returned when it ALREADY carried it,
    /// so a Case Tracker retry is a no-op rather than a second status change.
    /// </summary>
    Applied = 1,

    /// <summary>
    /// Unknown office, unknown appointment, or an office with the integration switched off --
    /// deliberately indistinguishable from one another.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// The appointment exists but cannot take this outcome: it already holds the OTHER attendance
    /// outcome, or its status permits neither trigger (anything but Approved).
    /// </summary>
    Conflict = 3,
}
