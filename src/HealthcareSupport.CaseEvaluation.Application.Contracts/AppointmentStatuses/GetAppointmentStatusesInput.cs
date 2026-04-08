using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public class GetAppointmentStatusesInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public GetAppointmentStatusesInput()
    {
    }
}