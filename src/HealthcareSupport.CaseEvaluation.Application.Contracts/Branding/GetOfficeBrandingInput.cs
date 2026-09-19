using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// 2026-06-30 (QA item B) -- paged query for the host-central office-branding
/// table. Free-text <see cref="Filter"/> matches the office name or its display
/// name; sortable fields: <c>officeName</c> (default) and <c>displayName</c>.
/// </summary>
public class GetOfficeBrandingInput : PagedAndSortedResultRequestDto
{
    /// <summary>Case-insensitive substring matched on office name or display name.</summary>
    public string? Filter { get; set; }
}
