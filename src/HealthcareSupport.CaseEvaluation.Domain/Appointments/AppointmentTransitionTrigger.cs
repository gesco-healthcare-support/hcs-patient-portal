namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Triggers fed into the appointment-status state machine. Each value names the
/// action a caller is requesting; the state machine maps (CurrentStatus, Trigger)
/// to a target <see cref="HealthcareSupport.CaseEvaluation.Enums.AppointmentStatusType"/>.
///
/// Wave 1 exposes endpoints for: Approve, Reject, SendBack, Respond.
/// Cancel / Reschedule / day-of-exam triggers are configured in the graph but
/// not exposed as endpoints until Wave 3 (appointment-change-requests).
/// </summary>
public enum AppointmentTransitionTrigger
{
    Approve = 1,
    Reject = 2,
    SendBack = 3,
    Respond = 4,
    RequestCancellation = 5,
    RequestReschedule = 6,
    ConfirmCancellation = 7,
    ConfirmCancellationLate = 8,
    ConfirmReschedule = 9,
    ConfirmRescheduleLate = 10,
    MarkNoShow = 11,
    CheckIn = 12,
    CheckOut = 13,
    Bill = 14,
}
