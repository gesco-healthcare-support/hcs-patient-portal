using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// 2026-06-30 (QA item B) -- paged query for the host Offices/Tenants table.
/// Extends ABP's <see cref="PagedAndSortedResultRequestDto"/> with a free-text
/// <see cref="Filter"/> matched on the office name. Sortable server fields:
/// <c>name</c> (default) and <c>editionName</c>.
/// </summary>
public class GetOfficesInput : PagedAndSortedResultRequestDto
{
    /// <summary>Case-insensitive substring matched on the office (tenant) name.</summary>
    public string? Filter { get; set; }
}
