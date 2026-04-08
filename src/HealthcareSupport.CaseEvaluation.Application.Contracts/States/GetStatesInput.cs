using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.States;

public class GetStatesInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? Name { get; set; }

    public GetStatesInput()
    {
    }
}