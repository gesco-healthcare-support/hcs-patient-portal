using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// 2026-10-10 -- one change request that is waiting for the SIGNED-IN party's agreement:
/// a reschedule date staff proposed, or a cancellation. The in-portal counterpart of the
/// emailed consent link (<see cref="ChangeRequestConsentInfoDto"/>), shown on the party's own
/// appointment and home page. Carries only what the party already sees on that appointment.
/// </summary>
public class PartyConsentRequestDto
{
    public Guid ChangeRequestId { get; set; }

    public Guid AppointmentId { get; set; }

    public string ConfirmationNumber { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    /// <summary>The appointment's current date and time, before any change.</summary>
    public DateTime AppointmentDate { get; set; }

    public ChangeRequestType ChangeRequestType { get; set; }

    /// <summary>Cancellation reason or reschedule reason, per the change type.</summary>
    public string? Reason { get; set; }

    /// <summary>The proposed new date and time (reschedule only), human-formatted.</summary>
    public string? RequestedNewDateTime { get; set; }

    /// <summary>The caller's answer: Pending until they agree or decline.</summary>
    public ChangeRequestConsentStatus ConsentStatus { get; set; }
}
