using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public class GetAppointmentLanguagesInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public GetAppointmentLanguagesInput()
    {
    }
}