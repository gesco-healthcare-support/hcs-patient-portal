using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.Identity;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

[Audited]
public class DefenseAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [CanBeNull]
    public virtual string? FirmName { get; set; }

    [CanBeNull]
    public virtual string? FirmAddress { get; set; }

    [CanBeNull]
    public virtual string? WebAddress { get; set; }

    [CanBeNull]
    public virtual string? PhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? FaxNumber { get; set; }

    [CanBeNull]
    public virtual string? Street { get; set; }

    [CanBeNull]
    public virtual string? City { get; set; }

    [CanBeNull]
    public virtual string? ZipCode { get; set; }

    public Guid? StateId { get; set; }

    public Guid? IdentityUserId { get; set; }

    [CanBeNull]
    public virtual string? Email { get; set; }

    protected DefenseAttorney()
    {
    }

    public DefenseAttorney(Guid id, Guid? stateId, Guid? identityUserId, string? firmName = null, string? firmAddress = null, string? phoneNumber = null, string? email = null)
    {
        Id = id;
        Check.Length(firmName, nameof(firmName), DefenseAttorneyConsts.FirmNameMaxLength, 0);
        Check.Length(firmAddress, nameof(firmAddress), DefenseAttorneyConsts.FirmAddressMaxLength, 0);
        Check.Length(phoneNumber, nameof(phoneNumber), DefenseAttorneyConsts.PhoneNumberMaxLength, 0);
        Check.Length(email, nameof(email), DefenseAttorneyConsts.EmailMaxLength, 0);
        FirmName = firmName;
        FirmAddress = firmAddress;
        PhoneNumber = phoneNumber;
        StateId = stateId;
        IdentityUserId = identityUserId;
        Email = email;
    }
}
