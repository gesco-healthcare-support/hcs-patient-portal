namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// The kind of change a user is requesting on an Approved appointment.
/// Used by <c>AppointmentChangeRequest</c> to discriminate fields and
/// pick the right state-transition path on supervisor approval.
/// </summary>
public enum ChangeRequestType
{
    Cancel = 1,
    Reschedule = 2,
}
