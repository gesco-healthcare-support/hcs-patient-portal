using System.Text.Json;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Per-round before/after snapshot of the flagged scalar field values for the Send Back
/// staff diff (Branch 2). The app service builds the key-&gt;display map via
/// <see cref="InfoRequestFields"/> (SSN masked at capture, ids resolved to names), then
/// serializes it here; this type now only handles serialization + the diff. Pure (no DI)
/// so the diff rule is unit-tested directly.
/// </summary>
internal static class InfoRequestSnapshot
{
    /// <summary>
    /// Builds the per-field old-&gt;new diff for one round, in registry order, for the
    /// flagged scalar fields. A field is "Changed" only when an AFTER snapshot exists and
    /// differs from BEFORE, so open rounds and no-op resubmits read as unchanged. Keys not
    /// in the scalar registry (e.g. <c>documents</c>) are excluded.
    /// </summary>
    public static List<InfoRequestFieldDiffDto> BuildDiff(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after,
        ISet<string> flaggedKeys)
    {
        var diffs = new List<InfoRequestFieldDiffDto>();
        foreach (var key in InfoRequestFields.ScalarKeysInOrder)
        {
            if (!flaggedKeys.Contains(key))
            {
                continue;
            }
            var hasOld = before.TryGetValue(key, out var oldValue);
            var hasNew = after.TryGetValue(key, out var newValue);
            diffs.Add(new InfoRequestFieldDiffDto
            {
                Key = key,
                OldValue = hasOld ? oldValue : null,
                NewValue = hasNew ? newValue : null,
                Changed = hasNew &&
                    !string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal),
            });
        }
        return diffs;
    }

    public static string Serialize(Dictionary<string, string> map)
    {
        return JsonSerializer.Serialize(map);
    }

    public static Dictionary<string, string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
