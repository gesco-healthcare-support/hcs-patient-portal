using System;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- pure queryable filter for the supervisor's
/// pending-change-request inbox endpoint. Composable predicates so the
/// AppService can chain filters without building a string-LINQ-Dynamic
/// expression. Tests pin every individual filter against in-memory
/// data so the live IQueryable on EF Core gets the same result.
///
/// <para><c>internal static</c> for unit-testability via
/// <c>InternalsVisibleTo</c>.</para>
/// </summary>
internal static class ChangeRequestListFilter
{
    /// <summary>
    /// Applies every supplied filter parameter to the input queryable.
    /// Null parameters are skipped (no-ops). Returns the filtered
    /// queryable for further composition (paging, sorting).
    /// </summary>
    public static IQueryable<AppointmentChangeRequest> Apply(
        IQueryable<AppointmentChangeRequest> source,
        RequestStatusType? requestStatus,
        ChangeRequestType? changeRequestType,
        DateTime? createdFromUtc,
        DateTime? createdToUtc)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var filtered = source;

        if (requestStatus.HasValue)
        {
            var statusValue = requestStatus.Value;
            filtered = filtered.Where(c => c.RequestStatus == statusValue);
        }
        if (changeRequestType.HasValue)
        {
            var typeValue = changeRequestType.Value;
            filtered = filtered.Where(c => c.ChangeRequestType == typeValue);
        }
        if (createdFromUtc.HasValue)
        {
            var fromValue = createdFromUtc.Value;
            filtered = filtered.Where(c => c.CreationTime >= fromValue);
        }
        if (createdToUtc.HasValue)
        {
            var toValue = createdToUtc.Value;
            filtered = filtered.Where(c => c.CreationTime <= toValue);
        }

        return filtered;
    }
}
