using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// 2026-11-03 (email routing, row 8) -- raised when staff open a reschedule consent round ("Set
/// date"). The sides' representatives get their consent LINK from
/// <see cref="ChangeRequestConsentRequestedEto"/>; this event lets
/// <c>ChangeRequestConsentInfoEmailHandler</c> tell everyone who was NOT asked (claim examiner,
/// office inbox, Intake Staff) with the no-link information notice.
/// </summary>
public class ChangeRequestConsentOpenedEto
{
    public Guid AppointmentId { get; set; }

    public Guid ChangeRequestId { get; set; }

    public Guid? TenantId { get; set; }

    public ChangeRequestType ChangeRequestType { get; set; }

    public int RoundNumber { get; set; }

    /// <summary>The slot this round asks the sides to agree to.</summary>
    public Guid? ProposedDoctorAvailabilityId { get; set; }

    /// <summary>The addresses that were sent a consent link: they do not also get the information notice.</summary>
    public List<string> ConsentLinkEmails { get; set; } = new();

    /// <summary>2026-11-04: the sides that were sent a consent link. The case followers who may answer for
    /// those sides got the same link, so they do not also get the information notice.</summary>
    public List<ChangeRequestSide> ConsentLinkSides { get; set; } = new();

    public DateTime OccurredAt { get; set; }
}
