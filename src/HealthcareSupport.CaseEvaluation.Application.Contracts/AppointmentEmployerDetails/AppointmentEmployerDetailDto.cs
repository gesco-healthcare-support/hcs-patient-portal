using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class AppointmentEmployerDetailDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string EmployerName { get; set; } = null!;
    public string Occupation { get; set; } = null!;
    public string? PhoneNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid? StateId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}