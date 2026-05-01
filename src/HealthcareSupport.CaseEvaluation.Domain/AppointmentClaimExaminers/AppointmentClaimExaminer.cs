using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.Collections.Generic;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

[Audited]
public class AppointmentClaimExaminer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public Guid AppointmentInjuryDetailId { get; set; }

    [CanBeNull]
    public virtual string? Name { get; set; }

    [CanBeNull]
    public virtual string? ClaimExaminerNumber { get; set; }

    [CanBeNull]
    public virtual string? Email { get; set; }

    [CanBeNull]
    public virtual string? PhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? Fax { get; set; }

    [CanBeNull]
    public virtual string? Street { get; set; }

    [CanBeNull]
    public virtual string? City { get; set; }

    [CanBeNull]
    public virtual string? Zip { get; set; }

    public Guid? StateId { get; set; }

    public bool IsActive { get; set; }

    protected AppointmentClaimExaminer()
    {
    }

    public AppointmentClaimExaminer(Guid id, Guid appointmentInjuryDetailId, bool isActive)
    {
        Id = id;
        AppointmentInjuryDetailId = appointmentInjuryDetailId;
        IsActive = isActive;
    }
}
