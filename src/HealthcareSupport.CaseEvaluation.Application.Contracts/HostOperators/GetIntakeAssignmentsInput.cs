using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// 2026-06-30 (QA item B) -- paged query for the intake-assignments management
/// grid. Free-text <see cref="Filter"/> matches operator name / email / office
/// name; sortable fields: <c>operatorName</c> (default, then office),
/// <c>operatorEmail</c>, <c>officeName</c>.
/// </summary>
public class GetIntakeAssignmentsInput : PagedAndSortedResultRequestDto
{
    /// <summary>Case-insensitive substring matched on operator name / email / office name.</summary>
    public string? Filter { get; set; }
}
