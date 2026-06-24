namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// One redacted field-change row for the appointment change-log view + the
/// intake-changed email. Sourced from ABP audit (<c>EntityChange</c> /
/// <c>EntityPropertyChange</c>) and exploded one row per changed property, mirroring
/// OLD's per-field change-log. <see cref="OldValue"/>/<see cref="NewValue"/> are
/// populated only when the field is on the non-sensitive allowlist; otherwise they
/// are null and <see cref="ValueRedacted"/> is true (render "updated", no values).
/// </summary>
public class AppointmentChangeLogDto
{
    /// <summary>The appointment this change belongs to (known for the per-appointment view).</summary>
    public Guid? AppointmentId { get; set; }

    /// <summary>Friendly entity label, e.g. "Appointment", "Injury Detail", "Body Part".</summary>
    public string EntityType { get; set; } = string.Empty;

    public string PropertyName { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    /// <summary>True when the values were masked for PHI; the UI shows "updated" with no values.</summary>
    public bool ValueRedacted { get; set; }

    /// <summary>Created / Updated / Deleted.</summary>
    public string ChangeType { get; set; } = string.Empty;

    public DateTime ChangeTime { get; set; }
}
