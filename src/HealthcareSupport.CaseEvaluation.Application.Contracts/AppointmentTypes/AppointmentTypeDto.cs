using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public class AppointmentTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}