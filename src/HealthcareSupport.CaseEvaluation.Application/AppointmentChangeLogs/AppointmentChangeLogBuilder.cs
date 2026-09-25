using HealthcareSupport.CaseEvaluation.Appointments.Auditing;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

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
    {
        var rows = new List<AppointmentChangeLogDto>();

        foreach (var change in changes)
        {
            var rowAppointmentId = appointmentId ?? ResolveAppointmentId(change);

            foreach (var prop in change.Properties)
            {
                var diff = AuditFieldDiff.BuildRow(
                    change.EntityTypeFullName, prop.PropertyName, prop.OriginalValue, prop.NewValue);
                if (diff == null)
                {
                    continue;
                }

                rows.Add(new AppointmentChangeLogDto
                {
                    AppointmentId = rowAppointmentId,
                    EntityType = AppointmentAuditedEntities.Label(change.EntityTypeFullName),
                    PropertyName = diff.PropertyName,
                    OldValue = diff.OldValue,
                    NewValue = diff.NewValue,
                    ValueRedacted = diff.ValueRedacted,
                    ChangeType = change.ChangeType,
                    ChangeTime = change.ChangeTime,
                });
            }
        }

        return rows;
    }

    private static Guid? ResolveAppointmentId(RawEntityChange change)
        => change.EntityTypeFullName == AppointmentAuditedEntities.Appointment
            && Guid.TryParse(change.EntityId, out var apptId)
                ? apptId
                : null;
}
