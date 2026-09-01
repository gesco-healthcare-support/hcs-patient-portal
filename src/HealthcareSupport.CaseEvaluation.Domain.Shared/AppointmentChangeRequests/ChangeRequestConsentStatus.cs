namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- opposing-side consent state for a change request.
/// Distinct from <c>RequestStatusType</c> (which the Staff Supervisor drives):
/// this tracks whether the OTHER side has agreed to the requested
/// cancel/reschedule before the supervisor may finalize it.
///
/// <list type="bullet">
///   <item><c>NotRequired</c> -- consent gating off / legacy flow.</item>
///   <item><c>Pending</c> -- token issued, awaiting the opposing side.</item>
///   <item><c>Approved</c> -- opposing side clicked Yes; ready for supervisor finalize.</item>
///   <item><c>Rejected</c> -- opposing side clicked No; routes to staff mediation.</item>
///   <item><c>Expired</c> -- token lapsed; defaults to a No, staff notified.</item>
/// </list>
/// </summary>
public enum ChangeRequestConsentStatus
{
    NotRequired = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4,
}
