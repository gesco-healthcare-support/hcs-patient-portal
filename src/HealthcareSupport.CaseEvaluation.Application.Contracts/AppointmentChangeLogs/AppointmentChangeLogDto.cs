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

    /// <summary>
    /// Readable action for the whole change ("Booked", "Approved", "Document rejected", ...). Every
    /// property row of one change carries the same label. Built from enum states only, never from values.
    /// </summary>
    public string ActionLabel { get; set; } = string.Empty;

    /// <summary>
    /// Who made the change: the real person's display name (a host operator acting through an office
    /// login is shown as the operator, never the login), or a fixed system label such as "Case Tracker".
    /// Null when the audit record cannot be linked.
    /// </summary>
    public string? ChangedByName { get; set; }

    /// <summary>One of <see cref="ChangeAuthorKinds"/>.</summary>
    public string? ChangedByKind { get; set; }
}

/// <summary>The kinds of actor shown beside a change-log row.</summary>
public static class ChangeAuthorKinds
{
    public const string Staff = "Staff";
    public const string HostStaff = "HostStaff";
    public const string Outside = "Outside";
    public const string System = "System";
    public const string CaseTracker = "CaseTracker";
    public const string Party = "Party";
}
