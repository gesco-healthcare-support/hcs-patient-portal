using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class GetDoctorAvailabilitiesInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public DateTime? AvailableDateMin { get; set; }

    public DateTime? AvailableDateMax { get; set; }

    public TimeOnly? FromTimeMin { get; set; }

    public TimeOnly? FromTimeMax { get; set; }

    public TimeOnly? ToTimeMin { get; set; }

    public TimeOnly? ToTimeMax { get; set; }

    public BookingStatus? BookingStatusId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public GetDoctorAvailabilitiesInput()
    {
    }
}