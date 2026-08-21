namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// A framework-agnostic projection of one ABP <c>EntityPropertyChange</c>. Lets the
/// pure <see cref="AppointmentChangeLogBuilder"/> be unit-tested without constructing
/// ABP audit entities (which have protected setters).
/// </summary>
public sealed record RawPropertyChange(string PropertyName, string? OriginalValue, string? NewValue);

/// <summary>Framework-agnostic projection of one ABP <c>EntityChange</c>.</summary>
public sealed record RawEntityChange(
    string EntityTypeFullName,
    string EntityId,
    string ChangeType,
    DateTime ChangeTime,
    IReadOnlyList<RawPropertyChange> Properties);
