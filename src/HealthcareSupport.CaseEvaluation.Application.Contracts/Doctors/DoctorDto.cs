using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Gender Gender { get; set; }

    public Guid? IdentityUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}