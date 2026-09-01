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

    public virtual async Task<DoctorAvailability> CreateAsync(
        Guid locationId,
        List<Guid> appointmentTypeIds,
        DateTime availableDate,
        TimeOnly fromTime,
        TimeOnly toTime,
        BookingStatus bookingStatusId,
        int capacity)
    {
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(availableDate, nameof(availableDate));
        Check.NotNull(bookingStatusId, nameof(bookingStatusId));
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");
        }

        var doctorAvailability = new DoctorAvailability(
            GuidGenerator.Create(),
            locationId,
            availableDate,
            fromTime,
            toTime,
            bookingStatusId,
            capacity);

        if (appointmentTypeIds != null)
        {
            foreach (var id in appointmentTypeIds.Distinct())
            {
                doctorAvailability.AddAppointmentType(id);
            }
        }

        return await _doctorAvailabilityRepository.InsertAsync(doctorAvailability);
    }

    public virtual async Task<DoctorAvailability> UpdateAsync(
        Guid id,
        Guid locationId,
        List<Guid> appointmentTypeIds,
        DateTime availableDate,
        TimeOnly fromTime,
        TimeOnly toTime,
        BookingStatus bookingStatusId,
        int capacity,
        [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(availableDate, nameof(availableDate));
        Check.NotNull(bookingStatusId, nameof(bookingStatusId));
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");
        }

        // Eager-load the M2M collection so the sync (RemoveAllExceptGivenIds +
        // AddAppointmentType) operates on the materialized join set.
        var queryable = await _doctorAvailabilityRepository
            .WithDetailsAsync(x => x.AppointmentTypes);
        var query = queryable.Where(x => x.Id == id);
        var doctorAvailability = await AsyncExecuter.FirstOrDefaultAsync(query)
            ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(DoctorAvailability), id);

        doctorAvailability.LocationId = locationId;
        doctorAvailability.AvailableDate = availableDate;
        doctorAvailability.FromTime = fromTime;
        doctorAvailability.ToTime = toTime;
        doctorAvailability.BookingStatusId = bookingStatusId;
        doctorAvailability.Capacity = capacity;

        if (appointmentTypeIds == null || appointmentTypeIds.Count == 0)
        {
            doctorAvailability.RemoveAllAppointmentTypes();
        }
        else
        {
            var distinct = appointmentTypeIds.Distinct().ToList();
            doctorAvailability.RemoveAllAppointmentTypesExceptGivenIds(distinct);
            foreach (var typeId in distinct)
            {
                doctorAvailability.AddAppointmentType(typeId);
            }
        }

        doctorAvailability.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _doctorAvailabilityRepository.UpdateAsync(doctorAvailability);
    }
}
