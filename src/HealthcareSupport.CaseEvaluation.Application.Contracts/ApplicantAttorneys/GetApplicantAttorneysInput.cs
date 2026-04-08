using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public class GetApplicantAttorneysInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? FirmName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? City { get; set; }

    public Guid? StateId { get; set; }

    public Guid? IdentityUserId { get; set; }

    public GetApplicantAttorneysInput()
    {
    }
}