using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

public class GetAppointmentBodyPartsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public Guid? AppointmentInjuryDetailId { get; set; }

    public GetAppointmentBodyPartsInput()
    {
    }
}
