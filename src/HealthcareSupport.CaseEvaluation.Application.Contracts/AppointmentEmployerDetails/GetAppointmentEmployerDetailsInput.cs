using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class GetAppointmentEmployerDetailsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? EmployerName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public Guid? AppointmentId { get; set; }

    public Guid? StateId { get; set; }

    public GetAppointmentEmployerDetailsInput()
    {
    }
}