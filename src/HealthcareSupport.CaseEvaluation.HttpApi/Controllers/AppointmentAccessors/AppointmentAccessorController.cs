using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentAccessors;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentAccessor")]
[Route("api/app/appointment-accessors")]
public class AppointmentAccessorController : AbpController, IAppointmentAccessorsAppService
{
    protected IAppointmentAccessorsAppService _appointmentAccessorsAppService;

    public AppointmentAccessorController(IAppointmentAccessorsAppService appointmentAccessorsAppService)
    {
        _appointmentAccessorsAppService = appointmentAccessorsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentAccessorWithNavigationPropertiesDto>> GetListAsync(GetAppointmentAccessorsInput input)
    {
        return _appointmentAccessorsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<AppointmentAccessorWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _appointmentAccessorsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentAccessorDto> GetAsync(Guid id)
    {
        return _appointmentAccessorsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _appointmentAccessorsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpGet]
    [Route("appointment-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        return _appointmentAccessorsAppService.GetAppointmentLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<AppointmentAccessorDto> CreateAsync(AppointmentAccessorCreateDto input)
    {
        return _appointmentAccessorsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentAccessorDto> UpdateAsync(Guid id, AppointmentAccessorUpdateDto input)
    {
        return _appointmentAccessorsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentAccessorsAppService.DeleteAsync(id);
    }
}