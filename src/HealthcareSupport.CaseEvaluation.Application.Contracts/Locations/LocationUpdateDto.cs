using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Locations;

public class LocationUpdateDto : IHasConcurrencyStamp
{
    [Required]
    [StringLength(LocationConsts.NameMaxLength)]
    public string Name { get; set; } = null!;
    [StringLength(LocationConsts.AddressMaxLength)]
    public string? Address { get; set; }

    [StringLength(LocationConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(LocationConsts.ZipCodeMaxLength)]
    [RegularExpression(@"^(\d{5}(-\d{4})?)?$")]
    public string? ZipCode { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ParkingFee { get; set; }

    public bool IsActive { get; set; }

    public Guid? StateId { get; set; }

    // I3 (2026-06-08): a Location offers multiple appointment types (M2M).
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    public string ConcurrencyStamp { get; set; } = null!;
}