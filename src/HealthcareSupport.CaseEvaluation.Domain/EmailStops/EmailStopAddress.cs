using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- the one way an address is compared for an email stop: trimmed and upper-cased with the
/// invariant culture, so "Ann@Firm.test " and "ann@firm.test" are the same person. Both stop tables store
/// this form in <c>NormalizedEmail</c>, and every lookup normalizes its input the same way.
/// </summary>
public static class EmailStopAddress
{
    /// <summary>The normalized form, or null for a blank address.</summary>
    public static string? Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }
        return email.Trim().ToUpperInvariant();
    }

    /// <summary>The distinct normalized forms of the non-blank addresses given.</summary>
    public static List<string> NormalizeAll(IEnumerable<string?>? emails) =>
        (emails ?? Array.Empty<string?>())
            .Select(Normalize)
            .Where(e => e != null)
            .Select(e => e!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
