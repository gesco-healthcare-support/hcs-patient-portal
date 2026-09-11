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

        await EnsureNoOverlappingSlotAsync(locationId, availableDate, fromTime, toTime);

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

    /// <summary>
    /// 2026-09-11 -- refuses a slot that overlaps an existing one at the same location on the
    /// same day.
    ///
    /// <para>WHY HERE. Placing this in the manager rather than the AppService means it covers
    /// BOTH write paths with one rule: the single create, which previously had no clash check
    /// anywhere, and the bulk generate, whose preview flags overlaps but is not atomic with the
    /// insert that follows it.</para>
    ///
    /// <para>THE PREDICATE IS COPIED DELIBERATELY, not re-derived. It is the same half-open
    /// interval test the generation preview applies -- <c>existing.FromTime &lt; new.ToTime
    /// &amp;&amp; existing.ToTime &gt; new.FromTime</c>, scoped to LocationId and compared on the
    /// DATE component. Half-open matters: back-to-back slots (09:00-09:15 then 09:15-09:30) are
    /// adjacent, not overlapping, and generation produces exactly that shape, so a closed-interval
    /// test would refuse every slot the generator creates after the first.</para>
    ///
    /// <para>Comparison is on <c>.Date</c> rather than the raw value because the column is a
    /// datetime2 holding a calendar date, and a row written with a time component would otherwise
    /// escape the check.</para>
    /// </summary>
    protected virtual async Task EnsureNoOverlappingSlotAsync(
        Guid locationId,
        DateTime availableDate,
        TimeOnly fromTime,
        TimeOnly toTime)
    {
        var day = availableDate.Date;
        var queryable = await _doctorAvailabilityRepository.GetQueryableAsync();
        var overlapping = queryable.Where(x =>
            x.LocationId == locationId
            && x.AvailableDate.Date == day
            && x.FromTime < toTime
            && x.ToTime > fromTime);

        if (await AsyncExecuter.AnyAsync(overlapping))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.DoctorAvailabilitySlotClash)
                .WithData("availableDate", day.ToString("MM-dd-yyyy"));
        }
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
        // Date only, for the same reason as the constructor: the time component is redundant
        // (FromTime/ToTime carry the real times) and keeping it would desynchronise the
        // uniqueness index from the clash rule.
        doctorAvailability.AvailableDate = availableDate.Date;
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
