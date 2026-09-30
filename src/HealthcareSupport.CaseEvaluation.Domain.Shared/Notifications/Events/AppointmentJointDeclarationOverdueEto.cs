using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Raised ONCE when an AME appointment passes its Joint Declaration Form deadline with no JDF
/// uploaded (2026-08-08).
///
/// <para>REPLACES <c>AppointmentAutoCancelledEto</c>. That event announced that the portal had
/// cancelled the appointment by itself; it no longer does. The appointment keeps its status and a
/// human decides -- Adrian: "auto-cancel without staff or anyone's involvement seems like a risky
/// thing and we should not do that."</para>
///
/// <para>Raised only on the run that FIRST detects the overdue state, never again while it stays
/// overdue, so staff are told once rather than every morning.</para>
/// </summary>
[Serializable]
public class AppointmentJointDeclarationOverdueEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>The appointment's due date -- the deadline that was missed.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>When the overdue state was detected, i.e. the value stamped on the appointment.</summary>
    public DateTime OccurredAt { get; set; }
}
