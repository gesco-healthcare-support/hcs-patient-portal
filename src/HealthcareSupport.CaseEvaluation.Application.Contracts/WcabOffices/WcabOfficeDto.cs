using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class WcabOfficeDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Name { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string? Address { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public bool IsActive { get; set; }

    public Guid? StateId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}