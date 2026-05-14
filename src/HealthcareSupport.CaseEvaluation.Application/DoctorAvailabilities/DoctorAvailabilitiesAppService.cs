using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

[RemoteService(IsEnabled = false)]
[Authorize]
public class DoctorAvailabilitiesAppService : CaseEvaluationAppService, IDoctorAvailabilitiesAppService
{
    protected IDoctorAvailabilityRepository _doctorAvailabilityRepository;
    protected DoctorAvailabilityManager _doctorAvailabilityManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> _locationRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> _appointmentTypeRepository;
    protected IRepository<Appointment, Guid> _appointmentRepository;
    protected IRepository<AppointmentChangeRequest, Guid> _appointmentChangeRequestRepository;
    protected ISystemParameterRepository _systemParameterRepository;

    public DoctorAvailabilitiesAppService(
        IDoctorAvailabilityRepository doctorAvailabilityRepository,
        DoctorAvailabilityManager doctorAvailabilityManager,
        IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> locationRepository,
        IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentChangeRequest, Guid> appointmentChangeRequestRepository,
        ISystemParameterRepository systemParameterRepository)
    {
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _doctorAvailabilityManager = doctorAvailabilityManager;
        _locationRepository = locationRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _appointmentRepository = appointmentRepository;
        _appointmentChangeRequestRepository = appointmentChangeRequestRepository;
        _systemParameterRepository = systemParameterRepository;
    }
    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
    public virtual async Task<PagedResultDto<DoctorAvailabilityWithNavigationPropertiesDto>> GetListAsync(GetDoctorAvailabilitiesInput input)
    {
        var totalCount = await _doctorAvailabilityRepository.GetCountAsync(input.FilterText, input.AvailableDateMin, input.AvailableDateMax, input.FromTimeMin, input.FromTimeMax, input.ToTimeMin, input.ToTimeMax, input.BookingStatusId, input.LocationId, input.AppointmentTypeId);
        var items = await _doctorAvailabilityRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AvailableDateMin, input.AvailableDateMax, input.FromTimeMin, input.FromTimeMax, input.ToTimeMin, input.ToTimeMax, input.BookingStatusId, input.LocationId, input.AppointmentTypeId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DoctorAvailabilityWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DoctorAvailabilityWithNavigationProperties>, List<DoctorAvailabilityWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
    public virtual async Task<DoctorAvailabilityWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DoctorAvailabilityWithNavigationProperties, DoctorAvailabilityWithNavigationPropertiesDto>((await _doctorAvailabilityRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
    public virtual async Task<DoctorAvailabilityDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<DoctorAvailability, DoctorAvailabilityDto>(await _doctorAvailabilityRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input)
    {
        var query = (await _locationRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.Locations.Location>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.Locations.Location>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input)
    {
        var query = (await _appointmentTypeRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Phase 7 (2026-05-03): mirror OLD DoctorsAvailabilityDomain.cs:151-154.
        // Even when the slot's BookingStatus is Available (e.g., reset by a
        // manual fix), refuse single-row delete if any Appointment or
        // AppointmentChangeRequest still references it -- preserves FK
        // integrity for historical rows.
        var appointmentRefExists = await _appointmentRepository.AnyAsync(a => a.DoctorAvailabilityId == id);
        var changeRequestRefExists = await _appointmentChangeRequestRepository.AnyAsync(c => c.NewDoctorAvailabilityId == id);
        if (appointmentRefExists || changeRequestRefExists)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.DoctorAvailabilityCannotDeleteReferenced);
        }

        await _doctorAvailabilityRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Delete)]
    public virtual async Task DeleteBySlotAsync(DoctorAvailabilityDeleteBySlotInputDto input)
    {
        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }

        var query = await _doctorAvailabilityRepository.GetQueryableAsync();
        query = query.Where(x =>
            x.LocationId == input.LocationId &&
            x.AvailableDate.Date == input.AvailableDate.Date &&
            x.FromTime == input.FromTime &&
            x.ToTime == input.ToTime);

        var matches = await AsyncExecuter.ToListAsync(query);
        foreach (var item in matches)
        {
            await _doctorAvailabilityRepository.DeleteAsync(item);
        }
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Delete)]
    public virtual async Task<DoctorAvailabilityBulkDeleteResultDto> DeleteByDateAsync(DoctorAvailabilityDeleteByDateInputDto input)
    {
        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }

        var query = await _doctorAvailabilityRepository.GetQueryableAsync();
        query = query.Where(x =>
            x.LocationId == input.LocationId &&
            x.AvailableDate.Date == input.AvailableDate.Date);

        var matches = await AsyncExecuter.ToListAsync(query);

        // Mirror OLD DoctorsAvailabilityDomain.Delete (P:\PatientPortalOld\
        // PatientAppointment.Domain\DoctorManagementModule\
        // DoctorsAvailabilityDomain.cs:159-177): partial-delete, NOT
        // all-or-nothing. Skip Booked + Reserved rows so they stay tied to
        // their in-flight appointments; delete only the Available rows in
        // the same date+location. Returns the per-call counts so the UI can
        // render "N of M deleted; K still booked" instead of a blanket 403.
        var result = new DoctorAvailabilityBulkDeleteResultDto();
        foreach (var item in matches)
        {
            if (HasInFlightStatus(item.BookingStatusId))
            {
                result.SkippedSlotIds.Add(item.Id);
                continue;
            }

            await _doctorAvailabilityRepository.DeleteAsync(item);
            result.DeletedCount++;
        }

        return result;
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Create)]
    public virtual async Task<DoctorAvailabilityDto> CreateAsync(DoctorAvailabilityCreateDto input)
    {
        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }

        var doctorAvailability = await _doctorAvailabilityManager.CreateAsync(input.LocationId, input.AppointmentTypeId, input.AvailableDate, input.FromTime, input.ToTime, input.BookingStatusId);
        return ObjectMapper.Map<DoctorAvailability, DoctorAvailabilityDto>(doctorAvailability);
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Edit)]
    public virtual async Task<DoctorAvailabilityDto> UpdateAsync(Guid id, DoctorAvailabilityUpdateDto input)
    {
        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }

        // Phase 7 (2026-05-03): mirror OLD DoctorsAvailabilityDomain.cs:126-130.
        // The supervisor cannot edit a slot once it is Reserved or Booked --
        // doing so would silently move an in-flight appointment to a different
        // time. Force cancellation / reschedule of the linked appointment first.
        var existing = await _doctorAvailabilityRepository.GetAsync(id);
        if (HasInFlightStatus(existing.BookingStatusId))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.DoctorAvailabilityCannotUpdateBookedOrReserved);
        }

        var doctorAvailability = await _doctorAvailabilityManager.UpdateAsync(id, input.LocationId, input.AppointmentTypeId, input.AvailableDate, input.FromTime, input.ToTime, input.BookingStatusId, input.ConcurrencyStamp);
        return ObjectMapper.Map<DoctorAvailability, DoctorAvailabilityDto>(doctorAvailability);
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
    public virtual async Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(List<DoctorAvailabilityGenerateInputDto> input)
    {
        if (input == null || input.Count == 0)
        {
            return new List<DoctorAvailabilitySlotsPreviewDto>();
        }

        foreach (var item in input)
        {
            if (item.LocationId == Guid.Empty)
            {
                throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
            }

            if (item.AppointmentDurationMinutes <= 0)
            {
                throw new UserFriendlyException("Appointment duration must be greater than zero.");
            }

            if (item.ToDate.Date < item.FromDate.Date)
            {
                throw new UserFriendlyException("To date must be greater than or equal to from date.");
            }

            if (item.ToTime <= item.FromTime)
            {
                throw new UserFriendlyException("To time must be greater than from time.");
            }
        }

        var minDate = input.Min(x => x.FromDate.Date);
        var maxDate = input.Max(x => x.ToDate.Date);

        var existingQuery = await _doctorAvailabilityRepository.GetQueryableAsync();
        existingQuery = existingQuery.Where(x => x.AvailableDate >= minDate && x.AvailableDate <= maxDate);
        var existingAvailabilities = await AsyncExecuter.ToListAsync(existingQuery);

        var generatedSlots = new List<DoctorAvailabilitySlotPreviewDto>();
        foreach (var item in input)
        {
            var currentDate = item.FromDate.Date;
            var endDate = item.ToDate.Date;

            while (currentDate <= endDate)
            {
                var currentTime = item.FromTime;

                while (currentTime.AddMinutes(item.AppointmentDurationMinutes) <= item.ToTime)
                {
                    var toTime = currentTime.AddMinutes(item.AppointmentDurationMinutes);
                    generatedSlots.Add(new DoctorAvailabilitySlotPreviewDto
                    {
                        AppointmentTypeId = item.AppointmentTypeId,
                        AvailableDate = currentDate,
                        BookingStatusId = item.BookingStatusId,
                        LocationId = item.LocationId,
                        FromTime = currentTime,
                        ToTime = toTime,
                        IsConflict = false,
                    });

                    currentTime = toTime;
                }

                currentDate = currentDate.AddDays(1);
            }
        }

        var grouped = generatedSlots
            .GroupBy(x => x.AvailableDate.Date)
            .OrderBy(x => x.Key)
            .ToList();

        var location = await _locationRepository.FindAsync(input.First().LocationId);
        var timeRange = $"{DateTime.Today.Add(input[0].FromTime.ToTimeSpan()):hh:mm tt}-{DateTime.Today.Add(input[0].ToTime.ToTimeSpan()):hh:mm tt}";

        var previewList = new List<DoctorAvailabilitySlotsPreviewDto>();
        var monthIndex = 1;
        foreach (var group in grouped)
        {
            var viewModel = new DoctorAvailabilitySlotsPreviewDto
            {
                Dates = group.Key.ToString("MM-dd-yyyy"),
                Days = group.Key.ToString("dddd"),
                MonthId = monthIndex,
                LocationName = location?.Name,
                Time = timeRange,
                DoctorAvailabilities = new List<DoctorAvailabilitySlotPreviewDto>(),
            };

            var timeId = 1;
            foreach (var slot in group.OrderBy(x => x.FromTime))
            {
                slot.TimeId = timeId;
                viewModel.DoctorAvailabilities.Add(slot);
                timeId++;
            }

            previewList.Add(viewModel);
            monthIndex++;
        }

        // Conflicts are scoped to the same Location only. The slot model is per-location:
        // two locations can independently host overlapping wall-clock slots (different doctors,
        // different rooms). Cross-location overlap is intentionally NOT a conflict.
        var isAlreadyExist = false;
        var isBookedByUser = false;
        foreach (var date in previewList)
        {
            foreach (var timeSlot in date.DoctorAvailabilities)
            {
                var overlap = existingAvailabilities.FirstOrDefault(x =>
                    x.LocationId == timeSlot.LocationId &&
                    x.AvailableDate.Date == timeSlot.AvailableDate.Date &&
                    x.FromTime < timeSlot.ToTime &&
                    x.ToTime > timeSlot.FromTime);

                if (overlap == null)
                {
                    continue;
                }

                if (overlap.BookingStatusId == BookingStatus.Booked ||
                    overlap.BookingStatusId == BookingStatus.Reserved)
                {
                    timeSlot.IsConflict = true;
                    isBookedByUser = true;
                }
                else
                {
                    timeSlot.IsConflict = true;
                    isAlreadyExist = true;
                }
            }
        }

        if (previewList.Count > 0)
        {
            if (isAlreadyExist)
            {
                previewList[0].SameTimeValidation = "Time slot already exists at this location.";
            }

            if (isBookedByUser)
            {
                previewList[0].SameTimeValidation =
                    "Time slot is already booked or reserved at this location.";
            }
        }

        return previewList;
    }

    /// <summary>
    /// Phase 7 (2026-05-03) -- booking-form slot picker. Open to any
    /// authenticated user; admin paths stay gated on
    /// <c>DoctorAvailabilities.Default</c>.
    /// </summary>
    [Authorize]
    public virtual async Task<List<DoctorAvailabilityDto>> GetDoctorAvailabilityLookupAsync(GetDoctorAvailabilityLookupInput input)
    {
        Check.NotNull(input, nameof(input));
        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }

        var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
        var leadTimeDays = systemParameter?.AppointmentLeadTime ?? 0;
        var minDate = (input.AvailableDateFrom?.Date ?? DateTime.Today).AddDays(leadTimeDays);

        var query = await _doctorAvailabilityRepository.GetQueryableAsync();
        query = query.Where(x =>
            x.LocationId == input.LocationId &&
            x.BookingStatusId == BookingStatus.Available &&
            x.AvailableDate >= minDate);

        if (input.AvailableDateTo.HasValue)
        {
            var maxDate = input.AvailableDateTo.Value.Date;
            query = query.Where(x => x.AvailableDate <= maxDate);
        }

        if (input.AppointmentTypeId.HasValue)
        {
            // OLD's loose-or-strict mode: a slot with null AppointmentTypeId
            // accepts any type; a slot with a specific type accepts only
            // that type. See _slot-generation-deep-dive.md.
            var typeId = input.AppointmentTypeId.Value;
            query = query.Where(x => x.AppointmentTypeId == null || x.AppointmentTypeId == typeId);
        }

        query = query.OrderBy(x => x.AvailableDate).ThenBy(x => x.FromTime);

        var entities = await AsyncExecuter.ToListAsync(query);
        return entities.Select(ObjectMapper.Map<DoctorAvailability, DoctorAvailabilityDto>).ToList();
    }

    /// <summary>
    /// In-flight slot statuses (<c>Reserved</c> or <c>Booked</c>) that
    /// block administrative mutation. Mirrors OLD
    /// <c>DoctorsAvailabilityDomain.cs</c>'s repeated check pattern.
    /// Extracted as <c>internal static</c> for unit-testability via
    /// <c>InternalsVisibleTo</c>.
    /// </summary>
    internal static bool HasInFlightStatus(BookingStatus status)
    {
        return status == BookingStatus.Reserved || status == BookingStatus.Booked;
    }

    /// <summary>
    /// Slot count per day from the <c>(FromTime, ToTime, durationMinutes)</c>
    /// triple. Mirrors OLD <c>DoctorsAvailabilityDomain.cs:310</c>
    /// (<c>Math.Floor(totalMinutes / appointmentDurationTime)</c>) -- the
    /// trailing partial slot is dropped silently. Returns 0 when
    /// <paramref name="durationMinutes"/> is non-positive or when the
    /// time window is empty / inverted.
    /// </summary>
    internal static int ComputeNumberOfSlotsPerDay(TimeOnly fromTime, TimeOnly toTime, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            return 0;
        }
        if (toTime <= fromTime)
        {
            return 0;
        }
        var totalMinutes = (toTime - fromTime).TotalMinutes;
        return (int)Math.Floor(totalMinutes / durationMinutes);
    }

    /// <summary>
    /// Returns true when the supplied <paramref name="fromTime"/> is
    /// strictly less than <paramref name="toTime"/>. Mirrors OLD
    /// <c>DoctorsAvailabilityDomain.cs:189-194</c>'s
    /// <c>TimeSpan.Compare</c> guard.
    /// </summary>
    internal static bool IsValidSlotTimeRange(TimeOnly fromTime, TimeOnly toTime)
    {
        return fromTime < toTime;
    }

    /// <summary>
    /// Returns true when <paramref name="toDate"/> is on or after
    /// <paramref name="fromDate"/>. Mirrors OLD
    /// <c>DoctorsAvailabilityDomain.cs:196-200</c>.
    /// </summary>
    internal static bool IsValidSlotDateRange(DateTime fromDate, DateTime toDate)
    {
        return toDate.Date >= fromDate.Date;
    }
}