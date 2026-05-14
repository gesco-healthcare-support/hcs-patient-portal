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

    // Issue 2.3 (2026-05-12): renamed from ClaimExaminerNumber -> Suite.
    // Same fix as AppointmentPrimaryInsurance.Suite — the form labels
    // this "STE" (USPS abbreviation for Suite); the OLD column name was
    // a misnomer.
    [CanBeNull]
    public virtual string? Suite { get; set; }

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
