using HealthcareSupport.CaseEvaluation.States;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class WcabOffice : FullAuditedAggregateRoot<Guid>
{
    [NotNull]
    public virtual string Name { get; set; } = null!;

    [NotNull]
    public virtual string Abbreviation { get; set; } = null!;

    [CanBeNull]
    public virtual string? Address { get; set; }

    [CanBeNull]
    public virtual string? City { get; set; }

    [CanBeNull]
    public virtual string? ZipCode { get; set; }

    public virtual bool IsActive { get; set; }

    public Guid? StateId { get; set; }

    protected WcabOffice()
    {
    }

    public WcabOffice(Guid id, Guid? stateId, string name, string abbreviation, bool isActive, string? address = null, string? city = null, string? zipCode = null)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Check.Length(name, nameof(name), WcabOfficeConsts.NameMaxLength, 0);
        Check.NotNull(abbreviation, nameof(abbreviation));
        Check.Length(abbreviation, nameof(abbreviation), WcabOfficeConsts.AbbreviationMaxLength, 0);
        Check.Length(address, nameof(address), WcabOfficeConsts.AddressMaxLength, 0);
        Check.Length(city, nameof(city), WcabOfficeConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), WcabOfficeConsts.ZipCodeMaxLength, 0);
        Name = name;
        Abbreviation = abbreviation;
        IsActive = isActive;
        Address = address;
        City = city;
        ZipCode = zipCode;
        StateId = stateId;
    }
}