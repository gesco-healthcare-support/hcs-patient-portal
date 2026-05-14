using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

public class AppointmentClaimExaminerDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public Guid AppointmentInjuryDetailId { get; set; }
    public string? Name { get; set; }
    public string? Suite { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Fax { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public Guid? StateId { get; set; }
    public bool IsActive { get; set; }
    public string ConcurrencyStamp { get; set; } = null!;
}
