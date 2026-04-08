using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class AppointmentApplicantAttorneyDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public Guid AppointmentId { get; set; }

    public Guid ApplicantAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}