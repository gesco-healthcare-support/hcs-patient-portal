namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Group L (2026-06-05) -- pure value object turning a comma-separated
/// "T-minus / elapsed-day anchor" setting string into a firing predicate for
/// the date-driven reminder jobs. Mirrors the <c>JointDeclarationCutoff</c>
/// convention: <c>public</c>, no DI / IO, deterministic.
///
/// <para>The cadence lives in <c>CaseEvaluation.Notifications.Reminders.*</c>
/// ABP settings (one anchor list per reminder), so the schedule is editable in
/// <c>/setting-management</c> per tenant instead of being hardcoded. Each job
/// computes its own day-count (days elapsed since creation/modification, or
/// days until the appointment/due date) and asks <see cref="ShouldFire"/>
/// whether that count is one of the configured anchors.</para>
///
/// <para>Parsing is defensive (admin-editable free text): whitespace and blank
/// segments are skipped, duplicates collapse, and non-integer or negative
/// tokens are ignored. A null/empty/blank list yields no anchors, so
/// <see cref="ShouldFire"/> always returns false -- mirroring
/// <c>JointDeclarationCutoff</c>'s "no config means never fire" behavior.</para>
/// </summary>
public sealed class ReminderCadence
{
    private readonly HashSet<int> _anchors;

    /// <param name="csvAnchors">
    /// The raw setting value, e.g. <c>"14,7,3"</c>. May be null, empty, or
    /// contain blanks/dupes/garbage; all are handled defensively.
    /// </param>
    public ReminderCadence(string? csvAnchors)
    {
        _anchors = Parse(csvAnchors);
    }

    /// <summary>The parsed, de-duplicated set of day anchors (order irrelevant).</summary>
    public IReadOnlyCollection<int> Anchors => _anchors;

    /// <summary>
    /// True when <paramref name="dayCount"/> matches a configured anchor. With
    /// no anchors configured, always false.
    /// </summary>
    public bool ShouldFire(int dayCount) => _anchors.Contains(dayCount);

    private static HashSet<int> Parse(string? csv)
    {
        var anchors = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            return anchors;
        }

        foreach (var token in csv.Split(','))
        {
            var trimmed = token.Trim();
            if (trimmed.Length > 0 && int.TryParse(trimmed, out var value) && value >= 0)
            {
                anchors.Add(value);
            }
        }

        return anchors;
    }
}
