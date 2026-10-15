using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public class GetClaimExaminersInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? City { get; set; }
    public Guid? StateId { get; set; }
    public Guid? IdentityUserId { get; set; }
}
