using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

public class GetAppointmentClaimExaminersInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public Guid? AppointmentId { get; set; }

    public GetAppointmentClaimExaminersInput()
    {
    }
}
