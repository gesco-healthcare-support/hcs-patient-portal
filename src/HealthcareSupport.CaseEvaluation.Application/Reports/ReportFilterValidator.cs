namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01 (2026-06-06) -- pure rules for the Appointment Request Report query,
/// ported from the legacy report's client-side guards
/// (<c>appointment-request-report-search.component.ts</c>): at least one filter
/// is required, and a date range must be both-or-neither with From &lt;= To.
/// Also resolves the report's default sort -- legacy default is
/// <c>RequestConfirmationNumber DESC</c> -- which must be passed explicitly
/// because the shared appointment repository otherwise applies its own
/// CreationTime default.
///
/// <para>Returns bool/string (no throwing) so it is pure and unit-testable via
/// InternalsVisibleTo; the AppService turns a failed check into a
/// user-friendly error. Mirrors the existing AppointmentBookingValidators
/// pattern.</para>
/// </summary>
internal static class ReportFilterValidator
{
    /// <summary>
    /// Sort applied when the caller supplies none. The
    /// <c>Appointment.</c> prefix matches the WithNavigationProperties query's
    /// OrderBy convention (see AppointmentConsts.GetDefaultSorting).
    /// </summary>
    internal const string DefaultSorting = "Appointment.RequestConfirmationNumber desc";

    /// <summary>True when at least one filter is set (legacy "enter a search value" guard).</summary>
    internal static bool HasAnyFilter(GetAppointmentReportInput input)
    {
        return !string.IsNullOrWhiteSpace(input.FilterText)
            || input.AppointmentTypeId.HasValue
            || input.LocationId.HasValue
            || input.AppointmentStatus.HasValue
            || input.AppointmentDateMin.HasValue
            || input.AppointmentDateMax.HasValue;
    }

    /// <summary>
    /// True when the date range is acceptable: both unset, or both set with
    /// From &lt;= To. A single bound (one-of-two) is rejected, matching OLD.
    /// </summary>
    internal static bool IsDateRangeValid(DateTime? from, DateTime? to)
    {
        if (from is null && to is null)
        {
            return true;
        }
        if (from is null || to is null)
        {
            return false;
        }
        return from.Value <= to.Value;
    }

    /// <summary>Returns the caller's sort, or <see cref="DefaultSorting"/> when blank.</summary>
    internal static string ResolveSorting(string? sorting)
    {
        return string.IsNullOrWhiteSpace(sorting) ? DefaultSorting : sorting;
    }
}
