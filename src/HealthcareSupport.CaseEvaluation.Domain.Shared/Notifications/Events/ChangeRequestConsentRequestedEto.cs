using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Group D (2026-06-09) -- published by the change-request submit AppService when
/// opposing-side consent is required. Carries the consent URL + the opposing-side
/// representative (one party) so the email handler can send the actionable Yes/No
/// notice. The confirmation-to-all-parties email still rides the existing
/// <c>AppointmentChangeRequestSubmittedEto</c>.
/// </summary>
public class ChangeRequestConsentRequestedEto
{
    public Guid AppointmentId { get; set; }

    public Guid ChangeRequestId { get; set; }

    public Guid? TenantId { get; set; }

    public ChangeRequestType ChangeRequestType { get; set; }

    /// <summary>Email of the opposing side's single representative (AA/DA, else patient/CE).</summary>
    public string OpposingRecipientEmail { get; set; } = string.Empty;

    /// <summary>Role tag of that representative (for the email greeting/branching).</summary>
    public RecipientRole OpposingRecipientRole { get; set; }

    /// <summary>Tenant-aware public consent landing URL carrying the raw token.</summary>
    public string ConsentUrl { get; set; } = string.Empty;

    /// <summary>
    /// Consent round this dispatch belongs to (phase 4c, 2026-08-05). 0 for CANCELLATION
    /// consent, which has no rounds and keeps its original context tag untouched.
    /// </summary>
    public int RoundNumber { get; set; }

    /// <summary>
    /// Which send attempt within <see cref="RoundNumber"/> (1 = the confirm that opened it).
    /// Load-bearing, not merely informational: the outbox idempotency key is
    /// <c>SHA256(tenantId | recipientEmail | contextTag | packetKind)</c> and
    /// <c>NotificationOutboxManager.EnqueueAsync</c> SILENTLY returns the existing row on a
    /// match, so without round + attempt in the tag every resend would vanish with no error.
    /// </summary>
    public int SendAttempt { get; set; }

    /// <summary>
    /// The round's proposed slot (phase 4c). Null for cancellation and for legacy rows; the
    /// email handler falls back to the request's <c>NewDoctorAvailabilityId</c> then.
    /// </summary>
    public Guid? ProposedDoctorAvailabilityId { get; set; }

    public DateTime OccurredAt { get; set; }
}
