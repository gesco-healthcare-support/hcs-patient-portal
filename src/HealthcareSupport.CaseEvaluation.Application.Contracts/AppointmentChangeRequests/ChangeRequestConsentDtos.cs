namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- read model for the public consent landing page. Carries
/// only what the opposing-side recipient needs to decide: the confirmation number,
/// the change type, the reason, and (for reschedule) the requested new date/time.
/// <see cref="ConsentStatus"/> lets the page render the already-responded / expired
/// states on a replay. No PHI beyond what the recipient already holds as a party.
/// </summary>
public class ChangeRequestConsentInfoDto
{
    public string ConfirmationNumber { get; set; } = string.Empty;

    public ChangeRequestType ChangeRequestType { get; set; }

    /// <summary>Cancellation reason or reschedule reason, per the change type.</summary>
    public string? Reason { get; set; }

    /// <summary>Requested new appointment date/time (reschedule only), human-formatted.</summary>
    public string? RequestedNewDateTime { get; set; }

    public ChangeRequestConsentStatus ConsentStatus { get; set; }
}

/// <summary>
/// Group D (2026-06-09) -- the opposing side's decision posted from the public
/// landing page. <c>true</c> = agree (request proceeds to the Staff Supervisor),
/// <c>false</c> = disagree (routes to staff mediation).
/// </summary>
public class SubmitChangeRequestConsentDto
{
    public bool Approved { get; set; }
}
