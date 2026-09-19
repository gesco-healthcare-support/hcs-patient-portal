using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 4d (2026-08-05) -- how the OLD appointment closes when a reschedule is finalized.
///
/// <para>REPLACES <c>RescheduleInPlacePolicy</c>. That policy answered "what status does the one
/// appointment keep?", which was the right question only while B2/4c moved a single row in place.
/// 4d creates a SECOND appointment, so the old one is genuinely finished and moves to a terminal
/// Rescheduled status -- carrying the billing signal Case Tracker needs in 4e to close or bill it.</para>
///
/// <para>The two triggers this returns have existed in
/// <c>AppointmentManager.BuildMachine</c> since the state machine was written but have been
/// UNREACHABLE, because nothing ever fired them. 4d makes them live.</para>
/// </summary>
public static class RescheduleSplitPolicy
{
    /// <summary>
    /// Maps the billing outcome staff choose at finalize onto the state-machine trigger that closes
    /// the old appointment.
    ///
    /// <para>Anything outside the two reschedule buckets is rejected rather than coerced: the value
    /// arrives from an API input, and a cancellation bucket (or worse, <c>Approved</c>) would drive
    /// the old appointment into a status the reschedule flow never intends. Same error code the
    /// approval validator already raises for a bad outcome, so callers need no new branch.</para>
    /// </summary>
    public static AppointmentTransitionTrigger ResolveOldAppointmentTrigger(AppointmentStatusType outcome)
    {
        return outcome switch
        {
            AppointmentStatusType.RescheduledNoBill => AppointmentTransitionTrigger.ConfirmReschedule,
            AppointmentStatusType.RescheduledLate => AppointmentTransitionTrigger.ConfirmRescheduleLate,
            _ => throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome)
                .WithData("outcome", outcome),
        };
    }

    /// <summary>
    /// The status the REPLACEMENT appointment starts in: whatever the source was in. No
    /// re-approval, because both sides already consented to this exact date.
    ///
    /// <para>Normally that is <c>Approved</c> -- an external reschedule requires an Approved
    /// source. But B1 (2026-07-01) lets internal staff reschedule a still-<c>Pending</c>
    /// appointment, and a replacement must not arrive Approved in that case: it would slip past
    /// the approval gate and the claim-information check that guards it, purely because someone
    /// rescheduled it.</para>
    ///
    /// <para>A source in any other status should never reach finalize -- the submit validators
    /// admit only Approved and (for staff) Pending -- so anything else is rejected rather than
    /// carried into a new row.</para>
    /// </summary>
    public static AppointmentStatusType ResolveNewAppointmentStatus(AppointmentStatusType sourceStatus)
    {
        return sourceStatus switch
        {
            AppointmentStatusType.Approved => AppointmentStatusType.Approved,
            AppointmentStatusType.RescheduleRequested => AppointmentStatusType.Approved,
            AppointmentStatusType.Pending => AppointmentStatusType.Pending,
            _ => throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestAppointmentNotApproved)
                .WithData("sourceStatus", sourceStatus),
        };
    }

    /// <summary>
    /// The status the PARENT (source) appointment returns to when a reschedule request is
    /// REJECTED, given the status it holds at rejection time.
    ///
    /// <para>An Approved source moved to <c>RescheduleRequested</c> on submit, so rejecting the
    /// request reverts it to <c>Approved</c>.</para>
    ///
    /// <para>B1 (2026-07-01) lets internal staff reschedule a still-<c>Pending</c> appointment, and
    /// the submit path fires the state-machine edge ONLY for an Approved source
    /// (<c>AppointmentChangeRequestManager.SubmitRescheduleAsync</c>), so a Pending source never
    /// leaves Pending. Rejecting must therefore leave it Pending: forcing it to Approved would
    /// promote a never-approved appointment past the approval gate and the claim-information check
    /// purely because a reschedule was rejected -- the same hazard
    /// <see cref="ResolveNewAppointmentStatus"/> guards on the approve path.</para>
    ///
    /// <para>Unlike the finalize paths this does NOT throw on an unexpected status: a reject must
    /// never fail hard and strand a change request, so anything other than the two reachable
    /// sources is left untouched rather than coerced.</para>
    /// </summary>
    public static AppointmentStatusType ResolveParentStatusOnReject(AppointmentStatusType sourceStatus)
    {
        return sourceStatus switch
        {
            AppointmentStatusType.RescheduleRequested => AppointmentStatusType.Approved,
            AppointmentStatusType.Pending => AppointmentStatusType.Pending,
            _ => sourceStatus,
        };
    }
}
