using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Paged query for the internal "Pending Invites" management list. The list
/// returns every invitation in the caller's tenant (Pending / Accepted /
/// Expired / Revoked) so the UI can facet by status client-side; the optional
/// <see cref="Filter"/> narrows by email substring.
/// </summary>
public class GetInvitesInput : PagedAndSortedResultRequestDto
{
    /// <summary>Case-insensitive email substring filter. Null/blank = no filter.</summary>
    public string? Filter { get; set; }
}
