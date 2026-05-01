using System.Linq;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// Normalisation helpers for the 3-of-6 patient match. Used by the AppService /
/// PatientManager to canonicalise inputs before the repository runs the SQL projection.
/// </summary>
public static class PatientMatching
{
    /// <summary>Minimum matched columns to count two records as the same patient.</summary>
    public const int MinMatchCount = 3;

    /// <summary>
    /// Trim + lowercase. Returns null for null / whitespace input so the repo's match
    /// projection can short-circuit non-contributing fields.
    /// </summary>
    public static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    /// <summary>Strip everything but digits from a phone-number-shaped string.</summary>
    public static string? NormalisePhone(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : new string(value.Where(char.IsDigit).ToArray());

    /// <summary>Strip everything but digits from an SSN-shaped string.</summary>
    public static string? NormaliseSsn(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : new string(value.Where(char.IsDigit).ToArray());
}
