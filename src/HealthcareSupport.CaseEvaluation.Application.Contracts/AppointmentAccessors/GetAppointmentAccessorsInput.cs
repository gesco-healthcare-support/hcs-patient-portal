using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class GetAppointmentAccessorsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public AccessType? AccessTypeId { get; set; }

    public Guid? IdentityUserId { get; set; }

    public Guid? AppointmentId { get; set; }

    public GetAppointmentAccessorsInput()
    {
    }
}