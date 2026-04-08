using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.States;

public class StateDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Name { get; set; } = null!;
    public string ConcurrencyStamp { get; set; } = null!;
}