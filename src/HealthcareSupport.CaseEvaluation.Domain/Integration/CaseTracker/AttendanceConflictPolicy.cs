using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Whether a <c>409</c> from the attendance endpoint can still succeed on a later retry.
///
/// <para>An attendance outcome may be recorded from <see cref="AppointmentStatusType.Approved"/>
/// ONLY (<c>AppointmentManager.BuildMachine</c>), so a conflict always means the appointment was in
/// some other status. This answers the one question the caller actually has: is it worth calling
/// again?</para>
///
/// <para><b>Why this is an explicit list and NOT computed from the state machine.</b> The obvious
/// implementation -- ask the machine which statuses can still reach <c>Approved</c> -- is WRONG, and
/// wrong in a way that already cost us a contract correction (2026-08-18). Rejecting a reschedule
/// request returns the appointment to <c>Approved</c> by assigning the status DIRECTLY
/// (<c>AppointmentChangeRequestsAppService.Approval.cs</c>, <c>RejectRescheduleAsync</c>) rather
/// than firing a trigger, so no such edge exists in the machine's graph. A reachability query would
/// therefore report <see cref="AppointmentStatusType.RescheduleRequested"/> as permanent, the Case
/// Tracker would stop retrying, and a no-show reported against an appointment whose reschedule was
/// later rejected would be dropped in silence.</para>
///
/// <para>Because the list is hand-maintained, <c>AttendanceConflictPolicyTests</c> enumerates EVERY
/// <see cref="AppointmentStatusType"/> value. Adding a lifecycle status without classifying it here
/// fails that test. That test, not this method, is what keeps the Case Tracker's behaviour correct
/// without renegotiating the contract -- which is the guarantee we gave them.</para>
/// </summary>
public static class AttendanceConflictPolicy
{
    /// <summary>
    /// True when <paramref name="currentStatus"/> can still become <c>Approved</c>, so the same
    /// attendance call will succeed once it does.
    ///
    /// <para><c>Approved</c> itself returns false: it does not produce a conflict at all, so a
    /// caller seeing it here is looking at a race that has already resolved, and re-sending buys
    /// nothing the next call would not.</para>
    /// </summary>
    public static bool IsRetryable(AppointmentStatusType currentStatus) => currentStatus switch
    {
        // Awaiting a staff decision. Approval makes the same call succeed.
        AppointmentStatusType.Pending => true,

        // Staff asked the requester for more information. Returns to Pending, then Approved.
        AppointmentStatusType.InfoRequested => true,

        // A reschedule is in flight. REJECTING it returns the appointment to Approved (the direct
        // write described above); approving it lands on a terminal Rescheduled* status. The caller
        // cannot know which way it will go, so the honest answer is "try again".
        AppointmentStatusType.RescheduleRequested => true,

        // Everything else is terminal, or can only reach a terminal status:
        // Rejected; NoShow / NotSeen (the other attendance outcome, and both permit no transitions);
        // CheckedIn / CheckedOut / Billed (the patient did attend); Cancelled* / Rescheduled*;
        // CancellationRequested (unreachable today -- a pending cancellation leaves the appointment
        // Approved -- but classified rather than omitted so the exhaustive test stays honest);
        // and Approved, per the remark above.
        _ => false,
    };
}
