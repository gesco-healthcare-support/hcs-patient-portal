using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class AppointmentDefenseAttorneyDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public Guid AppointmentId { get; set; }

    public Guid DefenseAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
