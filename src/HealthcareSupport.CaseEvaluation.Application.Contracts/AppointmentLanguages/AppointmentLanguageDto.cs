using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public class AppointmentLanguageDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
}