using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public class AppointmentStatusDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
}