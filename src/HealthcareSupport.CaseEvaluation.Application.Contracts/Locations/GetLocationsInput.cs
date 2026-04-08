using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.Locations;

public class GetLocationsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? Name { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public decimal? ParkingFeeMin { get; set; }

    public decimal? ParkingFeeMax { get; set; }

    public bool? IsActive { get; set; }

    public Guid? StateId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public GetLocationsInput()
    {
    }
}