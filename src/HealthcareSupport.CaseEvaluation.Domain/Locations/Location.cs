using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.States;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Locations;

public class Location : FullAuditedAggregateRoot<Guid>
{
    [NotNull]
    public virtual string Name { get; set; } = null!;

    [CanBeNull]
    public virtual string? Address { get; set; }

    [CanBeNull]
    public virtual string? City { get; set; }

    [CanBeNull]
    public virtual string? ZipCode { get; set; }

    public virtual decimal ParkingFee { get; set; }

    public virtual bool IsActive { get; set; }

    public Guid? StateId { get; set; }

    public Guid? AppointmentTypeId { get; set; }
    public virtual ICollection<DoctorLocation> DoctorLocations { get; set; } = new Collection<DoctorLocation>();

    protected Location()
    {
    }

    public Location(Guid id, Guid? stateId, Guid? appointmentTypeId, string name, decimal parkingFee, bool isActive, string? address = null, string? city = null, string? zipCode = null)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Check.Length(name, nameof(name), LocationConsts.NameMaxLength, 0);
        Check.Length(address, nameof(address), LocationConsts.AddressMaxLength, 0);
        Check.Length(city, nameof(city), LocationConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), LocationConsts.ZipCodeMaxLength, 0);
        Name = name;
        ParkingFee = parkingFee;
        IsActive = isActive;
        Address = address;
        City = city;
        ZipCode = zipCode;
        StateId = stateId;
        AppointmentTypeId = appointmentTypeId;
    }
}