using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

public class GetAppointmentPrimaryInsurancesInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public Guid? AppointmentInjuryDetailId { get; set; }

    public GetAppointmentPrimaryInsurancesInput()
    {
    }
}
