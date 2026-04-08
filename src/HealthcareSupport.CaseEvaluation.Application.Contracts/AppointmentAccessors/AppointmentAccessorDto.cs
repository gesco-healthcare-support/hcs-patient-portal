using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorDto : FullAuditedEntityDto<Guid>
{
    public AccessType AccessTypeId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid AppointmentId { get; set; }
}