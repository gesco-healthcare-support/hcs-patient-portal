using System;
using System.Globalization;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Formats timestamps for the integration wire format: ISO-8601, UTC, always with a trailing
/// <c>Z</c>.
///
/// <para>This exists because <c>DateTimeKind</c> cannot be trusted at this boundary. EF Core reads
/// <c>datetime2</c> back as <see cref="DateTimeKind.Unspecified"/>, and System.Text.Json then emits
/// no <c>Z</c> -- which the receiver would read as a local time. Some values are also explicitly
/// UTC (<c>AppointmentApproveDate = DateTime.UtcNow</c>) while ABP audit columns come from
/// <c>IClock.Now</c>, so the Kind genuinely varies by field.</para>
///
/// <para>ASSUMPTION worth knowing: an <see cref="DateTimeKind.Unspecified"/> value is treated as
/// already-UTC rather than converted. That is correct while the API container's clock is UTC (which
/// it is), because ABP's default <c>AbpClockOptions.Kind</c> is Unspecified and therefore
/// <c>IClock.Now</c> returns <c>DateTime.Now</c>. If a <c>TZ</c> is ever set on the API container,
/// audit-sourced timestamps such as <c>submittedAtUtc</c> would silently shift; pinning
/// <c>AbpClockOptions.Kind = DateTimeKind.Utc</c> would remove the assumption entirely.</para>
///
/// <para>Round-trip ("O") format is used rather than second precision so sub-second ordering
/// survives: the receiver uses <c>updatedAt</c> as a monotonic skip-if-older guard, and truncating
/// to whole seconds could make two rapid edits compare equal and drop the newer one.</para>
/// </summary>
public static class IntegrationTimestamp
{
    /// <summary>Formats as ISO-8601 UTC with <c>Z</c>, preserving sub-second precision.</summary>
    public static string ToIsoUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        return utc.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>Nullable overload; returns null so an absent timestamp stays absent on the wire.</summary>
    public static string? ToIsoUtcOrNull(DateTime? value)
    {
        return value.HasValue ? ToIsoUtc(value.Value) : null;
    }

    /// <summary>Date-only, <c>yyyy-MM-dd</c>. Used for the clinic-local slot date and the patient DOB.</summary>
    public static string ToDateOnly(DateTime value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
