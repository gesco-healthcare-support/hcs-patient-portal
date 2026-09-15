using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

public class GetAppointmentPrimaryInsurancesInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public Guid? AppointmentId { get; set; }

    public GetAppointmentPrimaryInsurancesInput()
    {
    }
}
