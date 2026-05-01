using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public class GetAppointmentInjuryDetailsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public Guid? AppointmentId { get; set; }
    public string? ClaimNumber { get; set; }

    public GetAppointmentInjuryDetailsInput()
    {
    }
}
