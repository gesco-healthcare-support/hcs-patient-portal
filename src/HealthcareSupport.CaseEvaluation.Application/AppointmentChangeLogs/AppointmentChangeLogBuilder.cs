using HealthcareSupport.CaseEvaluation.Appointments.Auditing;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>A built row plus the audit log it came from (never sent to the client).</summary>
public sealed record BuiltChangeLogRow(AppointmentChangeLogDto Row, Guid? AuditLogId);

/// <summary>
/// Explodes ABP audit changes into per-field, PHI-redacted change-log rows (mirroring
/// OLD's one-row-per-field log). Pure + deterministic so it is unit-tested directly;
/// the AppService only feeds it projected ABP <c>EntityChange</c> data.
/// </summary>
public static class AppointmentChangeLogBuilder
{
    /// <summary>
    /// One <see cref="AppointmentChangeLogDto"/> per changed property that survives the
    /// <see cref="AuditFieldPolicy"/> (noise dropped, sensitive values masked). When
    /// <paramref name="appointmentId"/> is null (global list) the appointment id is
    /// recovered from an Appointment-type row's <c>EntityId</c>; child rows stay null.
    /// </summary>
    public static List<AppointmentChangeLogDto> BuildRows(
        IEnumerable<RawEntityChange> changes,
        Guid? appointmentId)
        => BuildRowsWithSource(changes, appointmentId).ConvertAll(r => r.Row);

    /// <summary>
    /// Same rows, each paired with its audit log id so the caller can resolve who made the change
    /// for just the rows it returns. A change whose every property was dropped by the policy still
    /// yields ONE row with an empty <c>PropertyName</c> when its action is a meaningful event (an
    /// authorized user added, a document uploaded) so the event is not lost from the history.
    /// </summary>
    public static List<BuiltChangeLogRow> BuildRowsWithSource(
        IEnumerable<RawEntityChange> changes,
        Guid? appointmentId)
    {
        var rows = new List<BuiltChangeLogRow>();

        foreach (var change in changes)
        {
            var rowAppointmentId = appointmentId ?? ResolveAppointmentId(change);
            var entityLabel = AppointmentAuditedEntities.Label(change.EntityTypeFullName);
            var action = AppointmentChangeActionLabeler.Label(change);
            var before = rows.Count;

            foreach (var prop in change.Properties)
            {
                var diff = AuditFieldDiff.BuildRow(
                    change.EntityTypeFullName, prop.PropertyName, prop.OriginalValue, prop.NewValue);
                if (diff == null)
                {
                    continue;
                }

                rows.Add(new BuiltChangeLogRow(new AppointmentChangeLogDto
                {
                    AppointmentId = rowAppointmentId,
                    EntityType = entityLabel,
                    PropertyName = diff.PropertyName,
                    OldValue = diff.OldValue,
                    NewValue = diff.NewValue,
                    ValueRedacted = diff.ValueRedacted,
                    ChangeType = change.ChangeType,
                    ChangeTime = change.ChangeTime,
                    ActionLabel = action,
                }, change.AuditLogId));
            }

            if (rows.Count == before && IsEvent(action))
            {
                rows.Add(new BuiltChangeLogRow(new AppointmentChangeLogDto
                {
                    AppointmentId = rowAppointmentId,
                    EntityType = entityLabel,
                    PropertyName = string.Empty,
                    ChangeType = change.ChangeType,
                    ChangeTime = change.ChangeTime,
                    ActionLabel = action,
                }, change.AuditLogId));
            }
        }

        return rows;
    }

    // The generic fallbacks are noise when nothing survived redaction; named events are history.
    private static bool IsEvent(string action)
        => !action.EndsWith(" updated", StringComparison.Ordinal)
            && action != AppointmentChangeActionLabeler.DetailsEdited
            && action != AppointmentChangeActionLabeler.PartiesEdited;

    private static Guid? ResolveAppointmentId(RawEntityChange change)
        => change.EntityTypeFullName == AppointmentAuditedEntities.Appointment
            && Guid.TryParse(change.EntityId, out var apptId)
                ? apptId
                : null;
}
