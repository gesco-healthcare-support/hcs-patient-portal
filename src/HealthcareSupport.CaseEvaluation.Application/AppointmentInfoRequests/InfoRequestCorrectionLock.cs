namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Server-side lock for the fix-it flow: a correction may only touch fields the open
/// request flagged. With the generic key-&gt;value corrections map (QA item L), the rule
/// is simply "every provided key must be a flagged key" -- any other key is a violation.
/// Pure (no DI) so the security rule is unit-tested directly; the app service throws when
/// the result is non-empty.
/// </summary>
internal static class InfoRequestCorrectionLock
{
    /// <summary>
    /// Returns the provided keys that are NOT in <paramref name="flaggedKeys"/>. An empty
    /// result means the correction touches only flagged fields and is allowed.
    /// </summary>
    public static IReadOnlyList<string> FindUnflaggedChanges(
        IEnumerable<string> providedKeys,
        ISet<string> flaggedKeys)
    {
        return providedKeys
            .Where(key => !flaggedKeys.Contains(key))
            .Distinct()
            .ToList();
    }
}
