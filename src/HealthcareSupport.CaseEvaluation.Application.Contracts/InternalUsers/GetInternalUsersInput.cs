using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// 2026-06-30 (QA item B) -- paged query for the internal-users (Staff) table.
/// Extends ABP's <see cref="PagedAndSortedResultRequestDto"/> (Sorting /
/// SkipCount / MaxResultCount) with a single free-text <see cref="Filter"/>
/// matched server-side against name / surname / username / email.
/// </summary>
public class GetInternalUsersInput : PagedAndSortedResultRequestDto
{
    /// <summary>Case-insensitive substring matched on name / surname / username / email.</summary>
    public string? Filter { get; set; }
}
