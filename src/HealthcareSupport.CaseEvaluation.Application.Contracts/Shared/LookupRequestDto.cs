using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Shared;

public class LookupRequestDto : PagedResultRequestDto
{
    public string? Filter { get; set; }

    public LookupRequestDto()
    {
        MaxResultCount = MaxMaxResultCount;
    }
}