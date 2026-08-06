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
}
