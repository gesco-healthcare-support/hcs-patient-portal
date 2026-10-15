using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public class ClaimExaminerDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FaxNumber { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? ZipCode { get; set; }
    public Guid? StateId { get; set; }

    // IP6/UM4 record-based: nullable -- a CE master may have no login until claimed.
    public Guid? IdentityUserId { get; set; }
    public string ConcurrencyStamp { get; set; } = null!;
}
