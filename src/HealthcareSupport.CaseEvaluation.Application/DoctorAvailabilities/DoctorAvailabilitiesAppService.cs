using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Enums;
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

    public DoctorAvailabilitiesAppService(IDoctorAvailabilityRepository doctorAvailabilityRepository, DoctorAvailabilityManager doctorAvailabilityManager, IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> locationRepository, IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository)
    {
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _doctorAvailabilityManager = doctorAvailabilityManager;
        _locationRepository = locationRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
    }
    [Authorize]
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
        var query = (await _locationRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
        var query = (await _appointmentTypeRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
    public virtual async Task DeleteByDateAsync(DoctorAvailabilityDeleteByDateInputDto input)
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
        foreach (var item in matches)
        {
            await _doctorAvailabilityRepository.DeleteAsync(item);
        }
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
}