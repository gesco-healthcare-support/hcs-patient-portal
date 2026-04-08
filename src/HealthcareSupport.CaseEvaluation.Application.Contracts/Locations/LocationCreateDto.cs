using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Locations;

public class LocationCreateDto
{
    [Required]
    [StringLength(LocationConsts.NameMaxLength)]
    public string Name { get; set; } = null!;
    [StringLength(LocationConsts.AddressMaxLength)]
    public string? Address { get; set; }

    [StringLength(LocationConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(LocationConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    public decimal ParkingFee { get; set; }

    public bool IsActive { get; set; } = true;
    public Guid? StateId { get; set; }

    public Guid? AppointmentTypeId { get; set; }
}