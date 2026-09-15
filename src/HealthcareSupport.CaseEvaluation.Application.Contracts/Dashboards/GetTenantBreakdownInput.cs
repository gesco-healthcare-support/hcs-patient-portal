using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// 2026-06-30 (QA item B) -- paged query for the host dashboard's per-office
/// breakdown table, so it pages independently of the monolithic dashboard
/// payload. Free-text <see cref="Filter"/> matches the tenant name; sortable
/// fields: <c>appointments</c> (default, descending), <c>pending</c>,
/// <c>approved</c>, <c>thisWeek</c>, <c>tenantName</c>.
/// </summary>
public class GetTenantBreakdownInput : PagedAndSortedResultRequestDto
{
    /// <summary>Case-insensitive substring matched on the tenant name.</summary>
    public string? Filter { get; set; }
}
