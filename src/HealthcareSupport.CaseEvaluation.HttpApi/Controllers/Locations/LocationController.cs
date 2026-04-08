using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.Locations;

namespace HealthcareSupport.CaseEvaluation.Controllers.Locations;

[RemoteService]
[Area("app")]
[ControllerName("Location")]
[Route("api/app/locations")]
public class LocationController : AbpController, ILocationsAppService
{
    protected ILocationsAppService _locationsAppService;

    public LocationController(ILocationsAppService locationsAppService)
    {
        _locationsAppService = locationsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<LocationWithNavigationPropertiesDto>> GetListAsync(GetLocationsInput input)
    {
        return _locationsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<LocationWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _locationsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<LocationDto> GetAsync(Guid id)
    {
        return _locationsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        return _locationsAppService.GetStateLookupAsync(input);
    }

    [HttpGet]
    [Route("appointment-type-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input)
    {
        return _locationsAppService.GetAppointmentTypeLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<LocationDto> CreateAsync(LocationCreateDto input)
    {
        return _locationsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<LocationDto> UpdateAsync(Guid id, LocationUpdateDto input)
    {
        return _locationsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _locationsAppService.DeleteAsync(id);
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> locationIds)
    {
        return _locationsAppService.DeleteByIdsAsync(locationIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetLocationsInput input)
    {
        return _locationsAppService.DeleteAllAsync(input);
    }
}