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

    public LocationsAppService(ILocationRepository locationRepository, LocationManager locationManager, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository)
    {
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
        return ObjectMapper.Map<Location, LocationDto>(await _locationRepository.GetAsync(id));
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
        await _locationManager.EnsureCanDeleteAsync(id);
        await _locationRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.Locations.Create)]
    public virtual async Task<LocationDto> CreateAsync(LocationCreateDto input)
    {
        var location = await _locationManager.CreateAsync(input.StateId, input.AppointmentTypeId, input.Name, input.ParkingFee, input.IsActive, input.Address, input.City, input.ZipCode);
        return ObjectMapper.Map<Location, LocationDto>(location);
    }

    [Authorize(CaseEvaluationPermissions.Locations.Edit)]
    public virtual async Task<LocationDto> UpdateAsync(Guid id, LocationUpdateDto input)
    {
        var location = await _locationManager.UpdateAsync(id, input.StateId, input.AppointmentTypeId, input.Name, input.ParkingFee, input.IsActive, input.Address, input.City, input.ZipCode, input.ConcurrencyStamp);
        return ObjectMapper.Map<Location, LocationDto>(location);
    }

    [Authorize(CaseEvaluationPermissions.Locations.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> locationIds)
    {
        // IP4: bulk delete honors the same friendly pre-delete guard per id.
        foreach (var id in locationIds)
        {
            await _locationManager.EnsureCanDeleteAsync(id);
        }
        await _locationRepository.DeleteManyAsync(locationIds);
    }

    [Authorize(CaseEvaluationPermissions.Locations.Delete)]
    public virtual async Task DeleteAllAsync(GetLocationsInput input)
    {
        // IP4: resolve the rows the filter would delete and pre-check each, so a
        // filtered bulk delete cannot orphan a referenced location.
        var matches = await _locationRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Name, input.City, input.ZipCode, input.ParkingFeeMin, input.ParkingFeeMax, input.IsActive, input.StateId, input.AppointmentTypeId);
        foreach (var match in matches)
        {
            await _locationManager.EnsureCanDeleteAsync(match.Location.Id);
        }
        await _locationRepository.DeleteAllAsync(input.FilterText, input.Name, input.City, input.ZipCode, input.ParkingFeeMin, input.ParkingFeeMax, input.IsActive, input.StateId, input.AppointmentTypeId);
    }
}