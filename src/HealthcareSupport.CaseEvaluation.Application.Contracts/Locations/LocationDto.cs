using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Locations;

public class LocationDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public decimal ParkingFee { get; set; }

    public bool IsActive { get; set; }

    public Guid? StateId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}