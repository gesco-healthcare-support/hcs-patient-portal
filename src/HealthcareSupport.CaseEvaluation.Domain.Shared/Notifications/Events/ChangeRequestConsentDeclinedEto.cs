using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// 2026-11-03 (email routing, row 9d) -- raised when one side of a change request declines consent,
/// from the public consent page or the in-portal Agree/Decline. Subscriber
/// <c>ChangeRequestConsentDeclinedEmailHandler</c> tells the office inbox and the Intake Staff at once;
/// the parties are told only when staff close the request.
/// </summary>
public class ChangeRequestConsentDeclinedEto
{
    public Guid AppointmentId { get; set; }

    public Guid ChangeRequestId { get; set; }

    public Guid? TenantId { get; set; }

    public ChangeRequestType ChangeRequestType { get; set; }

    /// <summary>The consent round the decline belongs to; 0 for a cancellation (no rounds).</summary>
    public int RoundNumber { get; set; }

    public ChangeRequestSide Side { get; set; }

    public DateTime OccurredAt { get; set; }
}
