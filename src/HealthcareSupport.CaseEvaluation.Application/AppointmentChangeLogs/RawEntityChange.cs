namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// A framework-agnostic projection of one ABP <c>EntityPropertyChange</c>. Lets the
/// pure <see cref="AppointmentChangeLogBuilder"/> be unit-tested without constructing
/// ABP audit entities (which have protected setters).
/// </summary>
public sealed record RawPropertyChange(string PropertyName, string? OriginalValue, string? NewValue);

/// <summary>
/// Framework-agnostic projection of one ABP <c>EntityChange</c>. <paramref name="AuditLogId"/> is the
/// audit log (request) the change was written under; it is the only link to who made the change and
/// is never sent to the client.
/// </summary>
public sealed record RawEntityChange(
    string EntityTypeFullName,
    string EntityId,
    string ChangeType,
    DateTime ChangeTime,
    IReadOnlyList<RawPropertyChange> Properties,
    Guid? AuditLogId = null);
