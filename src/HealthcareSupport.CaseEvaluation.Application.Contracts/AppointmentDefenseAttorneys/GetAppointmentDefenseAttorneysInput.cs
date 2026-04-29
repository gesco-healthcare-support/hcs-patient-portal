using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class GetAppointmentDefenseAttorneysInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public Guid? AppointmentId { get; set; }

    public Guid? DefenseAttorneyId { get; set; }

    public Guid? IdentityUserId { get; set; }

    public GetAppointmentDefenseAttorneysInput()
    {
    }
}
