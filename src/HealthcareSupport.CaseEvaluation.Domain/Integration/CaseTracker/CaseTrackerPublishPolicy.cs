using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Whether an appointment's intake has been published, and therefore whether a follow-up message
/// about it can land. One definition shared by every document and change trigger, so they cannot
/// disagree about which appointments the Case Tracker knows.
/// </summary>
public static class CaseTrackerPublishPolicy
{
    /// <summary>
    /// True once approval has pushed the intake. Expressed as a DENY list rather than an allow list
    /// on purpose: the three excluded states are all reachable only from <c>Pending</c>, so they are
    /// a closed set, whereas post-approval states are open-ended. If a new lifecycle state is added
    /// later and nobody updates this, treating it as published makes the mistake LOUD -- a push that
    /// 404s and dead-letters -- instead of silently letting cases go stale on their side.
    /// </summary>
    public static bool IsPublished(AppointmentStatusType status) => status switch
    {
        // Never approved, so no case exists on the Case Tracker side.
        AppointmentStatusType.Pending => false,
        AppointmentStatusType.Rejected => false,
        AppointmentStatusType.InfoRequested => false,
        _ => true,
    };

    /// <summary>
    /// True once the appointment produced no evaluation and the Case Tracker itself said so
    /// (phase 5, 2026-08-07). Delegates to <see cref="AppointmentLifecycleValidators"/> rather than
    /// restating the pair, so a third cause later is one edit instead of a hunt.
    /// </summary>
    public static bool IsAttendanceClosed(AppointmentStatusType status) =>
        AppointmentLifecycleValidators.IsAttendanceOutcome(status);

    /// <summary>
    /// Whether a follow-up message about this appointment should go out AT ALL. Every push path asks
    /// this; <see cref="IsPublished"/> answers only half the question.
    ///
    /// <para>The halves are separate deliberately. <see cref="IsPublished"/> means "the intake
    /// landed, so a follow-up can be delivered", and a no-showed appointment WAS published --
    /// folding the attendance outcomes into its deny list would be false, and would break the
    /// invariant its own remarks rest on (those states are reachable only from <c>Pending</c>, a
    /// closed set; <c>NoShow</c> is reachable from <c>Approved</c>).</para>
    ///
    /// <para>This suppression is PERMANENT, unlike phase 4d's temporary reschedule gate -- do not
    /// remove it as leftover scaffolding. The Case Tracker AUTHORS these two statuses, so echoing
    /// them back carries nothing it did not already tell us. Accepted consequence, stated rather
    /// than discovered: once an appointment is NoShow or NotSeen, NO further edit to it reaches
    /// them. A future phase that lets the portal ORIGINATE either status must revisit this, because
    /// then the gate would withhold something they had not authored.</para>
    /// </summary>
    public static bool ShouldPublish(AppointmentStatusType status) =>
        IsPublished(status) && !IsAttendanceClosed(status);
}
