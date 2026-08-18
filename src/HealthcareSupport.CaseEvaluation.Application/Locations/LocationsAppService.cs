using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.IO;
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
using HealthcareSupport.CaseEvaluation.Locations;

namespace HealthcareSupport.CaseEvaluation.Locations;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Locations.Default)]
public class LocationsAppService : CaseEvaluationAppService, ILocationsAppService
{
    protected ILocationRepository _locationRepository;
    protected LocationManager _locationManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> _appointmentTypeRepository;

    // 2026-08-17: renders the *.InUse delete guards as their real message. Without it the
    // raw BusinessException reaches the SPA with no message and the toast falls back to
    // ABP's generic "An internal error occurred during your request!".
    protected DomainErrorTranslator _domainErrorTranslator;
    public LocationsAppService(ILocationRepository locationRepository, LocationManager locationManager, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository,
        DomainErrorTranslator domainErrorTranslator)
    {
        _domainErrorTranslator = domainErrorTranslator;
        _locationRepository = locationRepository;
        _locationManager = locationManager;
        _stateRepository = stateRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
    }

    public virtual async Task<PagedResultDto<LocationWithNavigationPropertiesDto>> GetListAsync(GetLocationsInput input)
    {
        var totalCount = await _locationRepository.GetCountAsync(input.FilterText, input.Name, input.City, input.ZipCode, input.ParkingFeeMin, input.ParkingFeeMax, input.IsActive, input.StateId, input.AppointmentTypeId);
        var items = await _locationRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Name, input.City, input.ZipCode, input.ParkingFeeMin, input.ParkingFeeMax, input.IsActive, input.StateId, input.AppointmentTypeId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<LocationWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<LocationWithNavigationProperties>, List<LocationWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<LocationWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<LocationWithNavigationProperties, LocationWithNavigationPropertiesDto>((await _locationRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    public virtual async Task<LocationDto> GetAsync(Guid id)
    {
        // I3: load the AppointmentTypes M2M so ToLocationDto can fill AppointmentTypeIds.
        var queryable = await _locationRepository.WithDetailsAsync(x => x.AppointmentTypes);
        var location = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id))
            ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Location), id);
        return ToLocationDto(location);
    }

    // I3 (2026-06-08): LocationDto.AppointmentTypeIds is filled from the M2M here
    // (the Mapperly mapper ignores it). Callers must pass a Location with its
    // AppointmentTypes collection loaded.
    private LocationDto ToLocationDto(Location location)
    {
        var dto = ObjectMapper.Map<Location, LocationDto>(location);
        dto.AppointmentTypeIds = location.AppointmentTypes.Select(x => x.AppointmentTypeId).ToList();
        return dto;
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.States.State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.States.State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

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

    [Authorize(CaseEvaluationPermissions.Locations.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // IP4: friendly pre-delete guard (soft-delete stays soft). The manager
        // throws LocationInUse when an Appointment or DoctorAvailability still
        // references the location, so the SPA gets a localized 400 instead of a
        // raw DB FK error.
        // 2026-08-17: the manager raises a bare *.InUse BusinessException. Translate it here
        // so the client gets the real reason instead of ABP's generic internal-error text.
        try
        {
            await _locationManager.EnsureCanDeleteAsync(id);
        }
        catch (BusinessException ex)
        {
            throw _domainErrorTranslator.Translate(ex);
        }
        await _locationRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.Locations.Create)]
    public virtual async Task<LocationDto> CreateAsync(LocationCreateDto input)
    {
        var location = await _locationManager.CreateAsync(input.StateId, input.AppointmentTypeIds, input.Name, input.ParkingFee, input.IsActive, input.Address, input.City, input.ZipCode, input.FacilityId);
        return ToLocationDto(location);
    }

    [Authorize(CaseEvaluationPermissions.Locations.Edit)]
    public virtual async Task<LocationDto> UpdateAsync(Guid id, LocationUpdateDto input)
    {
        var location = await _locationManager.UpdateAsync(id, input.StateId, input.AppointmentTypeIds, input.Name, input.ParkingFee, input.IsActive, input.Address, input.City, input.ZipCode, input.ConcurrencyStamp, input.FacilityId);
        return ToLocationDto(location);
    }

    [Authorize(CaseEvaluationPermissions.Locations.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> locationIds)
    {
        // IP4: bulk delete honors the same friendly pre-delete guard per id.
        foreach (var id in locationIds)
        {
            // 2026-08-17: the manager raises a bare *.InUse BusinessException. Translate it here
            // so the client gets the real reason instead of ABP's generic internal-error text.
            try
            {
                await _locationManager.EnsureCanDeleteAsync(id);
            }
            catch (BusinessException ex)
            {
                throw _domainErrorTranslator.Translate(ex);
            }
        }
        await _locationRepository.DeleteManyAsync(locationIds);
    }

    [Authorize(CaseEvaluationPermissions.Locations.Delete)]
    public virtual async Task DeleteAllAsync(GetLocationsInput input)
    {
        // IP4: resolve the rows the filter would delete and pre-check each, so a
        // filtered bulk delete cannot orphan a referenced location.
        var matches = await _locationRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Name, input.City, input.ZipCode, input.ParkingFeeMin, input.ParkingFeeMax, input.IsActive, input.StateId, input.AppointmentTypeId);
        // 2026-08-17: same translation as the single and by-ids deletes. Without it a filtered
        // bulk delete blocked by one referenced location reports ABP's generic internal-error
        // text -- the least useful place to get it, since the user cannot tell which row
        // blocked them or why.
        try
        {
            foreach (var match in matches)
            {
                await _locationManager.EnsureCanDeleteAsync(match.Location.Id);
            }
        }
        catch (BusinessException ex)
        {
            throw _domainErrorTranslator.Translate(ex);
        }
        await _locationRepository.DeleteAllAsync(input.FilterText, input.Name, input.City, input.ZipCode, input.ParkingFeeMin, input.ParkingFeeMax, input.IsActive, input.StateId, input.AppointmentTypeId);
    }
}