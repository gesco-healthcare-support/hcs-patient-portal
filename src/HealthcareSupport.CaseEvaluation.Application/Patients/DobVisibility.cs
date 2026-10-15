using System.Globalization;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// G-08-01 (2026-06-06) -- date-of-birth masking for PHI surfaces; sibling to
/// <see cref="SsnVisibility"/>. The Appointment Request Report and its PDFs
/// show only the birth YEAR (Adrian's HIPAA call 2026-06-06), never the full
/// date. Pure (no DI / no DB), so it is unit-tested directly via
/// InternalsVisibleTo.
/// </summary>
internal static class DobVisibility
{
    /// <summary>
    /// Returns the 4-digit birth year (e.g. "1985"), or null when no DOB is set.
    /// </summary>
    internal static string? ToYearOnly(DateTime? dateOfBirth)
    {
        return dateOfBirth?.Year.ToString(CultureInfo.InvariantCulture);
    }
}
