using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailability : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual DateTime AvailableDate { get; set; }

    public virtual TimeOnly FromTime { get; set; }

    public virtual TimeOnly ToTime { get; set; }

    public virtual BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>
    /// 2026-05-15 -- max simultaneous appointments this slot can hold.
    /// Minimum 1. The capacity-aware bookable predicate (plan 3) compares
    /// this to the active appointment count for the slot. Default 3 (locked
    /// decision 2026-05-27) for new-slot creation; internal staff may
    /// override per slot during generation. No upper bound enforced at the
    /// entity layer.
    /// </summary>
    public virtual int Capacity { get; set; } = 3;

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentType ids this slot accepts.
    /// Empty (no join rows) means "any type accepted" -- matches OLD's
    /// null-AppointmentTypeId loose mode. Plan 3's bookable predicate
    /// consumes this set.
    /// </summary>
    public virtual ICollection<DoctorAvailabilityAppointmentType> AppointmentTypes { get; protected set; }
        = new Collection<DoctorAvailabilityAppointmentType>();

    protected DoctorAvailability()
    {
    }

    public DoctorAvailability(
        Guid id,
        Guid locationId,
        DateTime availableDate,
        TimeOnly fromTime,
        TimeOnly toTime,
        BookingStatus bookingStatusId,
        int capacity = 3)
    {
        Id = id;
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");
        }
        AvailableDate = availableDate;
        FromTime = fromTime;
        ToTime = toTime;
        BookingStatusId = bookingStatusId;
        LocationId = locationId;
        Capacity = capacity;
        AppointmentTypes = new Collection<DoctorAvailabilityAppointmentType>();
    }

    public virtual void AddAppointmentType(Guid appointmentTypeId)
    {
        if (IsInAppointmentTypes(appointmentTypeId))
        {
            return;
        }
        AppointmentTypes.Add(new DoctorAvailabilityAppointmentType(Id, appointmentTypeId, TenantId));
    }

    public virtual void RemoveAppointmentType(Guid appointmentTypeId)
    {
        var toRemove = AppointmentTypes.Where(x => x.AppointmentTypeId == appointmentTypeId).ToList();
        foreach (var item in toRemove)
        {
            AppointmentTypes.Remove(item);
        }
    }

    public virtual void RemoveAllAppointmentTypesExceptGivenIds(List<Guid> appointmentTypeIds)
    {
        Check.NotNull(appointmentTypeIds, nameof(appointmentTypeIds));
        var toRemove = AppointmentTypes.Where(x => !appointmentTypeIds.Contains(x.AppointmentTypeId)).ToList();
        foreach (var item in toRemove)
        {
            AppointmentTypes.Remove(item);
        }
    }

    public virtual void RemoveAllAppointmentTypes()
    {
        AppointmentTypes.Clear();
    }

    private bool IsInAppointmentTypes(Guid appointmentTypeId)
    {
        return AppointmentTypes.Any(x => x.AppointmentTypeId == appointmentTypeId);
    }
}
