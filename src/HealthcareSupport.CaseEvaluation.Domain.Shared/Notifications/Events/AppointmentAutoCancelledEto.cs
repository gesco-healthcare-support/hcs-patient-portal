using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised when the
/// <c>JointDeclarationAutoCancelJob</c> (Phase 14) auto-cancels an AME /
/// AME-REVAL appointment because the JDF was not uploaded inside the
/// configured cutoff. Mirrors OLD
/// <c>EmailTemplate.AppointmentCancelledDueDate</c> trigger.
///
/// <para>Distinct from
/// <c>AppointmentChangeRequestApprovedEto(Outcome = CancelledNoBill)</c>
/// because no user-initiated change request exists in the auto-cancel
/// path. The Hangfire job emits this event after flipping the
/// appointment status.</para>
/// </summary>
public class AppointmentAutoCancelledEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>
    /// Why the job auto-cancelled. Common values mirror OLD's reason
    /// strings: <c>"JDF-not-uploaded"</c> for AME JDF cutoff;
    /// <c>"due-date-elapsed"</c> for the document-incomplete cutoff.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}
