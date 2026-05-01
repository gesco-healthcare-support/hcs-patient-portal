using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public class DefenseAttorneyDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string? FirmName { get; set; }

    public string? FirmAddress { get; set; }

    public string? WebAddress { get; set; }

    public string? PhoneNumber { get; set; }

    public string? FaxNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public Guid? StateId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
