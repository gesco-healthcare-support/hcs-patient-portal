using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class GetAppointmentApplicantAttorneysInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public Guid? AppointmentId { get; set; }

    public Guid? ApplicantAttorneyId { get; set; }

    public Guid? IdentityUserId { get; set; }

    public GetAppointmentApplicantAttorneysInput()
    {
    }
}