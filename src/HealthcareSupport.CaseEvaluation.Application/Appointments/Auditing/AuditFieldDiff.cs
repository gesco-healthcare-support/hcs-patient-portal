namespace HealthcareSupport.CaseEvaluation.Appointments.Auditing;

/// <summary>
/// One redacted change-log row. <see cref="OldValue"/>/<see cref="NewValue"/> are
/// populated only when <see cref="AuditFieldPolicy"/> allows; otherwise they are null
/// and <see cref="ValueRedacted"/> is true (render as "updated" with no values).
/// </summary>
public sealed record AuditDiffRow(
    string EntityType,
    string PropertyName,
    string? OldValue,
    string? NewValue,
    bool ValueRedacted);

/// <summary>
/// Builds redacted diff rows from raw audited (property, old, new) tuples by applying
/// <see cref="AuditFieldPolicy"/>. This is the single seam both the change-log view and
/// the intake-changed email go through, so neither can reveal a value the other masks.
/// </summary>
public static class AuditFieldDiff
{
    /// <summary>
    /// Returns a redacted row, or <c>null</c> when the property is audit noise that
    /// must not appear in the diff at all.
    /// </summary>
    public static AuditDiffRow? BuildRow(
        string entityType,
        string propertyName,
        string? oldValue,
        string? newValue)
    {
        if (!AuditFieldPolicy.ShouldInclude(entityType, propertyName))
        {
            return null;
        }
        if (AuditFieldPolicy.ShouldShowValue(entityType, propertyName))
        {
            return new AuditDiffRow(entityType, propertyName, oldValue, newValue, ValueRedacted: false);
        }
        return new AuditDiffRow(entityType, propertyName, OldValue: null, NewValue: null, ValueRedacted: true);
    }
}
