using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Identity;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class Doctor : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string FirstName { get; set; }

    [NotNull]
    public virtual string LastName { get; set; }

    [NotNull]
    public virtual string Email { get; set; }

    public virtual Gender Gender { get; set; }

    public Guid? IdentityUserId { get; set; }

    public virtual ICollection<DoctorAppointmentType> AppointmentTypes { get; protected set; }

    public virtual ICollection<DoctorLocation> Locations { get; protected set; }

    protected Doctor()
    {
    }

    public Doctor(Guid id, Guid? identityUserId, string firstName, string lastName, string email, Gender gender)
    {
        Id = id;
        Check.NotNull(firstName, nameof(firstName));
        Check.Length(firstName, nameof(firstName), DoctorConsts.FirstNameMaxLength, 0);
        Check.NotNull(lastName, nameof(lastName));
        Check.Length(lastName, nameof(lastName), DoctorConsts.LastNameMaxLength, 0);
        Check.NotNull(email, nameof(email));
        Check.Length(email, nameof(email), DoctorConsts.EmailMaxLength, 0);
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Gender = gender;
        IdentityUserId = identityUserId;
        AppointmentTypes = new Collection<DoctorAppointmentType>();
        Locations = new Collection<DoctorLocation>();
    }

    public virtual void AddAppointmentType(Guid appointmentTypeId)
    {
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        if (IsInAppointmentTypes(appointmentTypeId))
        {
            return;
        }

        AppointmentTypes.Add(new DoctorAppointmentType(Id, appointmentTypeId));
    }

    public virtual void RemoveAppointmentType(Guid appointmentTypeId)
    {
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        if (!IsInAppointmentTypes(appointmentTypeId))
        {
            return;
        }

        AppointmentTypes.RemoveAll(x => x.AppointmentTypeId == appointmentTypeId);
    }

    public virtual void RemoveAllAppointmentTypesExceptGivenIds(List<Guid> appointmentTypeIds)
    {
        Check.NotNullOrEmpty(appointmentTypeIds, nameof(appointmentTypeIds));
        AppointmentTypes.RemoveAll(x => !appointmentTypeIds.Contains(x.AppointmentTypeId));
    }

    public virtual void RemoveAllAppointmentTypes()
    {
        AppointmentTypes.RemoveAll(x => x.DoctorId == Id);
    }

    private bool IsInAppointmentTypes(Guid appointmentTypeId)
    {
        return AppointmentTypes.Any(x => x.AppointmentTypeId == appointmentTypeId);
    }

    public virtual void AddLocation(Guid locationId)
    {
        Check.NotNull(locationId, nameof(locationId));
        if (IsInLocations(locationId))
        {
            return;
        }

        Locations.Add(new DoctorLocation(Id, locationId));
    }

    public virtual void RemoveLocation(Guid locationId)
    {
        Check.NotNull(locationId, nameof(locationId));
        if (!IsInLocations(locationId))
        {
            return;
        }

        Locations.RemoveAll(x => x.LocationId == locationId);
    }

    public virtual void RemoveAllLocationsExceptGivenIds(List<Guid> locationIds)
    {
        Check.NotNullOrEmpty(locationIds, nameof(locationIds));
        Locations.RemoveAll(x => !locationIds.Contains(x.LocationId));
    }

    public virtual void RemoveAllLocations()
    {
        Locations.RemoveAll(x => x.DoctorId == Id);
    }

    private bool IsInLocations(Guid locationId)
    {
        return Locations.Any(x => x.LocationId == locationId);
    }
}