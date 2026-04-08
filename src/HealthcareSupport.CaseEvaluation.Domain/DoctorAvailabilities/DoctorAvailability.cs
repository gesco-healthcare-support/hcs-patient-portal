using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailability : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual DateTime AvailableDate { get; set; }

    public virtual TimeOnly FromTime { get; set; }

    public virtual TimeOnly ToTime { get; set; }

    public virtual BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    protected DoctorAvailability()
    {
    }

    public DoctorAvailability(Guid id, Guid locationId, Guid? appointmentTypeId, DateTime availableDate, TimeOnly fromTime, TimeOnly toTime, BookingStatus bookingStatusId)
    {
        Id = id;
        AvailableDate = availableDate;
        FromTime = fromTime;
        ToTime = toTime;
        BookingStatusId = bookingStatusId;
        LocationId = locationId;
        AppointmentTypeId = appointmentTypeId;
    }
}