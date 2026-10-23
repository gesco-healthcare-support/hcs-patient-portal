namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Group K (G-02-03, 2026-06-06) -- raised by
/// <c>AppointmentsAppService.UpdateAsync</c> when an appointment edit changes one
/// or more intake fields. Carries the ALREADY-REDACTED field diff (sensitive
/// values are masked before publish, so PHI never crosses the event bus), so the
/// <c>IntakeChangedEmailHandler</c> only renders + dispatches.
/// </summary>
public class AppointmentIntakeChangedEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>True when the appointment date/time changed -- triggers the
    /// one-shot "rescheduled by our team" email in addition to the diff email.</summary>
    public bool DateOrTimeChanged { get; set; }

    public List<IntakeChangedField> ChangedFields { get; set; } = new();

    /// <summary>
    /// When the change was saved. Part of the email's context tag, because the same appointment can be
    /// edited many times and each edit is its own email.
    /// </summary>
    public DateTime OccurredAt { get; set; }
}

/// <summary>One redacted intake field change for the notification diff table.</summary>
public class IntakeChangedField
{
    public string Section { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    /// <summary>True when the value was masked for PHI; render "updated" with no values.</summary>
    public bool ValueRedacted { get; set; }
}
