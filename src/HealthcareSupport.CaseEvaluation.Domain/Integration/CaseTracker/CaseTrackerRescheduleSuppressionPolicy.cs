using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// TEMPORARY, phase 4d (2026-08-05) -- keeps the reschedule SPLIT off the wire until phase 4e
/// amends the contract. <b>DELETE THIS FILE AND ITS THREE CALL SITES IN 4E.</b>
///
/// <para>Why it has to exist: <see cref="CaseTrackerPublishPolicy.IsPublished"/> returns true for
/// every status except <c>Pending</c>, <c>Rejected</c> and <c>InfoRequested</c>, so silence is NOT
/// the default. Left alone, 4d would tell the Case Tracker two things its receiver cannot yet
/// interpret: the old appointment arriving with a NoBill/Late billing status
/// (<see cref="Payload.BillingStatusWire"/>), and the replacement arriving as a SECOND case for
/// one claim. <c>docs/integration/case-tracker-api-contract.md</c> section E2 still promises the
/// portal "never creates a second one" and signals a reschedule by a CHANGED DATE -- both false
/// once 4d ships, which is 4e's rewrite to make.</para>
///
/// <para>One policy rather than a condition inlined at each site, so 4e removes suppression in one
/// place and cannot leave an arm behind -- the failure mode that would show up as a silent
/// divergence between the two systems rather than as an error.</para>
/// </summary>
public static class CaseTrackerRescheduleSuppressionPolicy
{
    /// <summary>
    /// Whether this appointment is one half of a 4d reschedule split and must stay off the wire.
    ///
    /// <para>Keyed on the appointment's CURRENT STATE, not on the code path that reached it, because
    /// the split is reachable from several triggers: the finalize itself, a later edit to either
    /// row, a patient demographic correction fanning out, and the hourly completeness sweep.
    /// A path-keyed gate would suppress the first and let the rest through.</para>
    /// </summary>
    public static bool IsSuppressed(Appointment appointment) =>
        appointment != null &&
        (IsRescheduleClosure(appointment.AppointmentStatus) ||
         appointment.RescheduledFromAppointmentId.HasValue);

    /// <summary>
    /// The OLD appointment: closed by the split into a terminal Rescheduled status. Before 4d these
    /// two statuses were unreachable, so no publish path has ever emitted one.
    /// </summary>
    public static bool IsRescheduleClosure(AppointmentStatusType status) =>
        status is AppointmentStatusType.RescheduledNoBill or AppointmentStatusType.RescheduledLate;
}
