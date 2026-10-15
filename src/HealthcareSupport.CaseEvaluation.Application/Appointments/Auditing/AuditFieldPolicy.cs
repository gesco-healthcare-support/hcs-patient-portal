namespace HealthcareSupport.CaseEvaluation.Appointments.Auditing;

/// <summary>
/// Group K PHI guard. A deny-by-default allowlist that decides, for an audited
/// (entity, property) pair, whether the change-log view + intake-changed email may
/// reveal the old/new VALUES (<see cref="ShouldShowValue"/>) or must mask them, and
/// whether the property appears in the diff at all (<see cref="ShouldInclude"/>).
///
/// Only the explicitly listed non-sensitive fields reveal values; every other field
/// is masked ("updated", no values). Audit-noise properties (ids, timestamps,
/// concurrency) are dropped from the diff entirely. The list keys by the entity's
/// SIMPLE type name so fully-qualified ABP EntityChange type strings resolve.
///
/// OLD stored + emailed raw SSN/DOB/names; this is the boundary that makes the NEW
/// replica HIPAA-safe (see docs/decisions on audit change-log redaction).
/// </summary>
public static class AuditFieldPolicy
{
    // {SimpleEntityName}.{Property} pairs whose old/new values are safe to reveal.
    private static readonly HashSet<string> ValueVisibleFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Appointment.AppointmentDate",
        "Appointment.AppointmentTypeId",
        "Appointment.LocationId",
        "Appointment.DoctorAvailabilityId",
        "Appointment.PanelNumber",
        "Appointment.DueDate",
        "Appointment.AppointmentStatus",
        "Appointment.AppointmentLanguageId",
        "Appointment.NeedsInterpreter",
        "AppointmentInjuryDetail.DateOfInjury",
    };

    // Audit-noise properties excluded from the diff regardless of entity.
    private static readonly HashSet<string> DroppedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreationTime", "CreatorId",
        "LastModificationTime", "LastModifierId",
        "DeletionTime", "DeleterId", "IsDeleted",
        "ConcurrencyStamp", "ExtraProperties", "TenantId",
    };

    /// <summary>True when the property should appear in the diff at all.</summary>
    public static bool ShouldInclude(string entityType, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }
        if (DroppedProperties.Contains(propertyName))
        {
            return false;
        }
        // Raw foreign-key / id columns are audit noise UNLESS a specific id is an
        // allowlisted display field (e.g. AppointmentTypeId).
        if (propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) &&
            !IsValueVisible(entityType, propertyName))
        {
            return false;
        }
        if (propertyName.EndsWith("ModifiedDate", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    /// <summary>True only for allowlisted non-sensitive fields; false masks the values.</summary>
    public static bool ShouldShowValue(string entityType, string propertyName)
        => IsValueVisible(entityType, propertyName);

    private static bool IsValueVisible(string entityType, string propertyName)
        => ValueVisibleFields.Contains($"{SimpleName(entityType)}.{propertyName}");

    private static string SimpleName(string entityType)
    {
        if (string.IsNullOrEmpty(entityType))
        {
            return entityType ?? string.Empty;
        }
        var lastDot = entityType.LastIndexOf('.');
        return lastDot >= 0 ? entityType[(lastDot + 1)..] : entityType;
    }
}
