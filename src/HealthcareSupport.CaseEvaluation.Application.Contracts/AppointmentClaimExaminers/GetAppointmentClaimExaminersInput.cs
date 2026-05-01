using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

public class GetAppointmentClaimExaminersInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public Guid? AppointmentInjuryDetailId { get; set; }

    public GetAppointmentClaimExaminersInput()
    {
    }
}
