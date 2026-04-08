using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class GetAppointmentsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? PanelNumber { get; set; }

    public DateTime? AppointmentDateMin { get; set; }

    public DateTime? AppointmentDateMax { get; set; }

    public Guid? IdentityUserId { get; set; }

    /// <summary>
    /// When set, filters appointments where the current user is assigned in AppointmentAccessor (for Applicant Attorney / Defense Attorney).
    /// </summary>
    public Guid? AccessorIdentityUserId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public Guid? LocationId { get; set; }

    public GetAppointmentsInput()
    {
    }
}