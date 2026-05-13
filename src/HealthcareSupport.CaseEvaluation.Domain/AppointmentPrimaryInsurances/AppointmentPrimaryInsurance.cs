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

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

[Audited]
public class AppointmentPrimaryInsurance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public Guid AppointmentInjuryDetailId { get; set; }

    [CanBeNull]
    public virtual string? Name { get; set; }

    // Issue 2.3 (2026-05-12): renamed from InsuranceNumber -> Suite.
    // The on-screen label remained "STE" (USPS postal abbreviation for
    // Suite, per Pub 28 \xa7213). The OLD column name "InsuranceNumber"
    // was a misnomer; the field always stored a suite identifier (the
    // OLD form positioned it as the address-line-2 between Street and
    // City). EF migration `RenameColumn` preserves existing values.
    [CanBeNull]
    public virtual string? Suite { get; set; }

    [CanBeNull]
    public virtual string? Attention { get; set; }

    [CanBeNull]
    public virtual string? PhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? FaxNumber { get; set; }

    [CanBeNull]
    public virtual string? Street { get; set; }

    [CanBeNull]
    public virtual string? City { get; set; }

    [CanBeNull]
    public virtual string? Zip { get; set; }

    public Guid? StateId { get; set; }

    public bool IsActive { get; set; }

    protected AppointmentPrimaryInsurance()
    {
    }

    public AppointmentPrimaryInsurance(Guid id, Guid appointmentInjuryDetailId, bool isActive)
    {
        Id = id;
        AppointmentInjuryDetailId = appointmentInjuryDetailId;
        IsActive = isActive;
    }
}
