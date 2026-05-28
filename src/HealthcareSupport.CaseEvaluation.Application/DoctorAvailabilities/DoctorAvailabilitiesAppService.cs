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
using Volo.Abp.Uow;
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
    protected IAppointmentRepository _appointmentRepository;
    protected IRepository<AppointmentChangeRequest, Guid> _appointmentChangeRequestRepository;
    protected ISystemParameterRepository _systemParameterRepository;
    // 2026-05-15 (slot rework plan 4) -- CreateRangeAsync wraps every insert
    // in a single transactional UoW; either every non-conflicted slot is
    // persisted or none is.
    protected IUnitOfWorkManager _unitOfWorkManager;

    public DoctorAvailabilitiesAppService(
        IDoctorAvailabilityRepository doctorAvailabilityRepository,
        DoctorAvailabilityManager doctorAvailabilityManager,
        IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> locationRepository,
        IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository,
        IAppointmentRepository appointmentRepository,
        IRepository<AppointmentChangeRequest, Guid> appointmentChangeRequestRepository,
        ISystemParameterRepository systemParameterRepository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _doctorAvailabilityManager = doctorAvailabilityManager;
        _locationRepository = locationRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _appointmentRepository = appointmentRepository;
        _appointmentChangeRequestRepository = appointmentChangeRequestRepository;
        _systemParameterRepository = systemParameterRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }
    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
    public virtual async Task<PagedResultDto<DoctorAvailabilityWithNavigationPropertiesDto>> GetListAsync(GetDoctorAvailabilitiesInput input)
    {
        var totalCount = await _doctorAvailabilityRepository.GetCountAsync(input.FilterText, input.AvailableDateMin, input.AvailableDateMax, input.FromTimeMin, input.FromTimeMax, input.ToTimeMin, input.ToTimeMax, input.BookingStatusId, input.LocationId);
        var items = await _doctorAvailabilityRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AvailableDateMin, input.AvailableDateMax, input.FromTimeMin, input.FromTimeMax, input.ToTimeMin, input.ToTimeMax, input.BookingStatusId, input.LocationId, input.Sorting, input.MaxResultCount, input.SkipCount);
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

        var doctorAvailability = await _doctorAvailabilityManager.CreateAsync(input.LocationId, input.AppointmentTypeIds, input.AvailableDate, input.FromTime, input.ToTime, input.BookingStatusId, input.Capacity);
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

        var doctorAvailability = await _doctorAvailabilityManager.UpdateAsync(id, input.LocationId, input.AppointmentTypeIds, input.AvailableDate, input.FromTime, input.ToTime, input.BookingStatusId, input.Capacity, input.ConcurrencyStamp);
        return ObjectMapper.Map<DoctorAvailability, DoctorAvailabilityDto>(doctorAvailability);
    }

    // 2026-05-15 (slot rework plan 4) -- cap on slots-per-call. Wraps the
    // CreateRangeAsync transaction; the SQL Server transaction stays within
    // healthy duration even on a full year of dense scheduling.
    internal const int GenerationSlotLimit = 5000;

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
    public virtual async Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(
        DoctorAvailabilityGenerateInputDto input)
    {
        ValidateGenerationInput(input);

        var generatedSlots = ExpandToSlotPreviews(input);
        if (generatedSlots.Count == 0)
        {
            return new List<DoctorAvailabilitySlotsPreviewDto>();
        }

        // Pull existing slots in the date span at this location so we can
        // flag conflicts.
        var minDate = generatedSlots.Min(s => s.AvailableDate).Date;
        var maxDate = generatedSlots.Max(s => s.AvailableDate).Date;
        var existingQuery = (await _doctorAvailabilityRepository.GetQueryableAsync())
            .Where(x =>
                x.LocationId == input.LocationId &&
                x.AvailableDate >= minDate &&
                x.AvailableDate <= maxDate);
        var existing = await AsyncExecuter.ToListAsync(existingQuery);

        var location = await _locationRepository.FindAsync(input.LocationId);
        var groupedByDate = generatedSlots
            .GroupBy(s => s.AvailableDate.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var previewList = new List<DoctorAvailabilitySlotsPreviewDto>();
        var monthIndex = 1;
        foreach (var group in groupedByDate)
        {
            var viewModel = new DoctorAvailabilitySlotsPreviewDto
            {
                Dates = group.Key.ToString("MM-dd-yyyy"),
                Days = group.Key.ToString("dddd"),
                MonthId = monthIndex,
                LocationName = location?.Name,
                // Multi-range generations no longer carry a single wall-clock
                // time string -- the SPA's slot picker (plan 5) renders the
                // per-slot times instead. Left as empty for backward shape.
                Time = string.Empty,
                DoctorAvailabilities = new List<DoctorAvailabilitySlotPreviewDto>(),
            };

            var timeId = 1;
            foreach (var slot in group.OrderBy(x => x.FromTime))
            {
                slot.TimeId = timeId++;
                viewModel.DoctorAvailabilities.Add(slot);
            }
            previewList.Add(viewModel);
            monthIndex++;
        }

        // Per-date conflict flagging. Same-location overlap with an existing
        // slot marks the new slot conflict; the message is set per-day so
        // the SPA can render which date had the collision. Reserved (manually
        // closed) overlap is messaged distinctly from a plain Available
        // overlap.
        foreach (var date in previewList)
        {
            foreach (var slot in date.DoctorAvailabilities)
            {
                var overlap = existing.FirstOrDefault(x =>
                    x.AvailableDate.Date == slot.AvailableDate.Date &&
                    x.FromTime < slot.ToTime &&
                    x.ToTime > slot.FromTime);
                if (overlap == null)
                {
                    continue;
                }
                slot.IsConflict = true;
                if (overlap.BookingStatusId == BookingStatus.Reserved)
                {
                    date.SameTimeValidation = L["DoctorAvailability:GenerationConflictReserved"].Value;
                }
                else
                {
                    date.SameTimeValidation = L["DoctorAvailability:GenerationConflictExists"].Value;
                }
            }
        }

        return previewList;
    }

    [Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Create)]
    public virtual async Task<DoctorAvailabilityCreateRangeResultDto> CreateRangeAsync(
        DoctorAvailabilityGenerateInputDto input)
    {
        // Validation + expansion + conflict flagging happen inside the
        // preview path; we re-run them server-side rather than trusting any
        // client-serialised preview rows (a concurrent admin could create a
        // colliding slot between the preview and this call).
        var preview = await GeneratePreviewAsync(input);
        var flatSlots = preview.SelectMany(d => d.DoctorAvailabilities).ToList();
        var slotsToInsert = flatSlots.Where(s => !s.IsConflict).ToList();
        var conflictedSlots = flatSlots.Where(s => s.IsConflict).ToList();

        var result = new DoctorAvailabilityCreateRangeResultDto
        {
            InsertedCount = 0,
            SkippedConflictCount = conflictedSlots.Count,
            ConflictedSlots = conflictedSlots,
        };

        if (slotsToInsert.Count == 0)
        {
            return result;
        }

        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            foreach (var slot in slotsToInsert)
            {
                await _doctorAvailabilityManager.CreateAsync(
                    slot.LocationId,
                    slot.AppointmentTypeIds,
                    slot.AvailableDate,
                    slot.FromTime,
                    slot.ToTime,
                    slot.BookingStatusId,
                    slot.Capacity);
                result.InsertedCount++;
            }
            await uow.CompleteAsync();
        }

        return result;
    }

    private void ValidateGenerationInput(DoctorAvailabilityGenerateInputDto input)
    {
        Check.NotNull(input, nameof(input));
        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }
        if (input.AppointmentDurationMinutes <= 0)
        {
            throw new UserFriendlyException(L["DoctorAvailability:DurationMustBeGreaterThanZero"]);
        }
        if (input.Capacity < 1)
        {
            throw new UserFriendlyException(L["DoctorAvailability:CapacityMustBeAtLeastOne"]);
        }
        if (input.ToDate.Date < input.FromDate.Date)
        {
            throw new UserFriendlyException(L["DoctorAvailability:ToDateBeforeFromDate"]);
        }
        if (input.FromDate.Date < DateTime.Today)
        {
            throw new UserFriendlyException(L["DoctorAvailability:CannotGenerateForPastDates"]);
        }
        if (input.TimeRanges == null || input.TimeRanges.Count == 0)
        {
            throw new UserFriendlyException(L["DoctorAvailability:AtLeastOneTimeRangeRequired"]);
        }

        foreach (var range in input.TimeRanges)
        {
            if (range.ToTime <= range.FromTime)
            {
                throw new UserFriendlyException(
                    L["DoctorAvailability:TimeRangeFromMustBeBeforeTo",
                        range.FromTime, range.ToTime]);
            }
            var duration = range.AppointmentDurationMinutes ?? input.AppointmentDurationMinutes;
            if (duration <= 0)
            {
                throw new UserFriendlyException(
                    L["DoctorAvailability:TimeRangeDurationMustBePositive",
                        range.FromTime, range.ToTime]);
            }
        }

        // Cross-range overlap rejection. Ranges are sorted by FromTime; any
        // adjacent pair where the next start is before the previous end
        // overlaps.
        var sortedRanges = input.TimeRanges.OrderBy(r => r.FromTime).ToList();
        for (var i = 1; i < sortedRanges.Count; i++)
        {
            if (sortedRanges[i].FromTime < sortedRanges[i - 1].ToTime)
            {
                throw new UserFriendlyException(
                    L["DoctorAvailability:TimeRangesOverlap",
                        sortedRanges[i - 1].FromTime, sortedRanges[i - 1].ToTime,
                        sortedRanges[i].FromTime, sortedRanges[i].ToTime]);
            }
        }

        if (input.SelectedDays != null && input.SelectedDays.Count > 0)
        {
            if (input.SelectedDays.Any(d => d < 0 || d > 6))
            {
                throw new UserFriendlyException(L["DoctorAvailability:SelectedDayOutOfRange"]);
            }
            if (input.SelectedDays.Distinct().Count() != input.SelectedDays.Count)
            {
                throw new UserFriendlyException(L["DoctorAvailability:SelectedDaysDuplicate"]);
            }
        }

        // Cap the generation before allocating the expansion. Locked
        // decision 2026-05-20 Q2: 5,000 covers a full year of dense
        // scheduling, well within SQL Server transaction norms.
        var expected = EstimateSlotCount(input);
        if (expected > GenerationSlotLimit)
        {
            throw new UserFriendlyException(
                L["DoctorAvailability:GenerationCountExceedsLimit", GenerationSlotLimit]);
        }
    }

    internal static List<DoctorAvailabilitySlotPreviewDto> ExpandToSlotPreviews(
        DoctorAvailabilityGenerateInputDto input)
    {
        var allowedDays = (input.SelectedDays == null || input.SelectedDays.Count == 0)
            ? new HashSet<int> { 0, 1, 2, 3, 4, 5, 6 }
            : new HashSet<int>(input.SelectedDays);

        var slots = new List<DoctorAvailabilitySlotPreviewDto>();
        var currentDate = input.FromDate.Date;
        var endDate = input.ToDate.Date;

        while (currentDate <= endDate)
        {
            if (allowedDays.Contains((int)currentDate.DayOfWeek))
            {
                foreach (var range in input.TimeRanges.OrderBy(r => r.FromTime))
                {
                    var duration = range.AppointmentDurationMinutes
                        ?? input.AppointmentDurationMinutes;
                    var currentTime = range.FromTime;

                    while (currentTime.AddMinutes(duration) <= range.ToTime)
                    {
                        var toTime = currentTime.AddMinutes(duration);
                        slots.Add(new DoctorAvailabilitySlotPreviewDto
                        {
                            AppointmentTypeIds = new List<Guid>(input.AppointmentTypeIds),
                            AvailableDate = currentDate,
                            BookingStatusId = input.BookingStatusId,
                            LocationId = input.LocationId,
                            FromTime = currentTime,
                            ToTime = toTime,
                            Capacity = input.Capacity,
                            IsConflict = false,
                        });
                        currentTime = toTime;
                    }
                }
            }
            currentDate = currentDate.AddDays(1);
        }

        return slots;
    }

    internal static int EstimateSlotCount(DoctorAvailabilityGenerateInputDto input)
    {
        if (input.TimeRanges == null || input.TimeRanges.Count == 0)
        {
            return 0;
        }
        var dayCount = 0;
        for (var day = input.FromDate.Date; day <= input.ToDate.Date; day = day.AddDays(1))
        {
            if (input.SelectedDays == null
                || input.SelectedDays.Count == 0
                || input.SelectedDays.Contains((int)day.DayOfWeek))
            {
                dayCount++;
            }
        }
        var slotsPerDay = input.TimeRanges.Sum(range =>
        {
            var duration = range.AppointmentDurationMinutes ?? input.AppointmentDurationMinutes;
            if (duration <= 0)
            {
                return 0;
            }
            var minutes = (range.ToTime - range.FromTime).TotalMinutes;
            return (int)Math.Floor(minutes / duration);
        });
        return dayCount * slotsPerDay;
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
            // OLD's loose-or-strict mode, ported to the M2M shape: a slot
            // with no AppointmentTypes accepts any type; a slot with a
            // specific set accepts only those types. See
            // _slot-generation-deep-dive.md.
            var typeId = input.AppointmentTypeId.Value;
            query = query.Where(x =>
                !x.AppointmentTypes.Any()
                || x.AppointmentTypes.Any(at => at.AppointmentTypeId == typeId));
        }

        query = query.OrderBy(x => x.AvailableDate).ThenBy(x => x.FromTime);

        var entities = await AsyncExecuter.ToListAsync(query);

        // 2026-05-15 (slot rework plan 3) -- compute RemainingCapacity and
        // exclude full slots from the picker. Bulk fetch active-counts to
        // avoid N+1 round-trips; missing keys mean zero active appointments.
        var slotIds = entities.Select(x => x.Id).ToList();
        var activeCounts = await _appointmentRepository.GetActiveCountsForSlotsAsync(slotIds);

        var dtos = new List<DoctorAvailabilityDto>(entities.Count);
        foreach (var slot in entities)
        {
            var dto = ObjectMapper.Map<DoctorAvailability, DoctorAvailabilityDto>(slot);
            var active = activeCounts.TryGetValue(slot.Id, out var c) ? c : 0;
            dto.RemainingCapacity = (int)Math.Max(0, slot.Capacity - active);
            if (dto.RemainingCapacity > 0)
            {
                dtos.Add(dto);
            }
        }
        return dtos;
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