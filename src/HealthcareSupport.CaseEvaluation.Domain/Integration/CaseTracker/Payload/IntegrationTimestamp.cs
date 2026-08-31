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
/// <para>An <see cref="DateTimeKind.Unspecified"/> value is treated as already-UTC rather than
/// converted. As of 2026-08-27 that is a fact rather than an assumption: <c>AbpClockOptions.Kind</c>
/// is pinned to <c>Utc</c> in <c>CaseEvaluationDomainModule</c>, so a freshly stamped value carries
/// its own Kind and a <c>TZ</c> on the API container can no longer shift <c>submittedAtUtc</c>.
/// Unspecified now reaches here from ONE source -- an EF <c>datetime2</c> read -- where the stored
/// value is UTC by decision, so specifying the Kind is the correct reading rather than a guess.</para>
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
