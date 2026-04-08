using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityManager : DomainService
{
    protected IDoctorAvailabilityRepository _doctorAvailabilityRepository;

    public DoctorAvailabilityManager(IDoctorAvailabilityRepository doctorAvailabilityRepository)
    {
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
    }

    public virtual async Task<DoctorAvailability> CreateAsync(Guid locationId, Guid? appointmentTypeId, DateTime availableDate, TimeOnly fromTime, TimeOnly toTime, BookingStatus bookingStatusId)
    {
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(availableDate, nameof(availableDate));
        Check.NotNull(bookingStatusId, nameof(bookingStatusId));
        var doctorAvailability = new DoctorAvailability(GuidGenerator.Create(), locationId, appointmentTypeId, availableDate, fromTime, toTime, bookingStatusId);
        return await _doctorAvailabilityRepository.InsertAsync(doctorAvailability);
    }

    public virtual async Task<DoctorAvailability> UpdateAsync(Guid id, Guid locationId, Guid? appointmentTypeId, DateTime availableDate, TimeOnly fromTime, TimeOnly toTime, BookingStatus bookingStatusId, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(availableDate, nameof(availableDate));
        Check.NotNull(bookingStatusId, nameof(bookingStatusId));
        var doctorAvailability = await _doctorAvailabilityRepository.GetAsync(id);
        doctorAvailability.LocationId = locationId;
        doctorAvailability.AppointmentTypeId = appointmentTypeId;
        doctorAvailability.AvailableDate = availableDate;
        doctorAvailability.FromTime = fromTime;
        doctorAvailability.ToTime = toTime;
        doctorAvailability.BookingStatusId = bookingStatusId;
        doctorAvailability.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _doctorAvailabilityRepository.UpdateAsync(doctorAvailability);
    }
}