using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class GetDoctorsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public Guid? LocationId { get; set; }

    public GetDoctorsInput()
    {
    }
}