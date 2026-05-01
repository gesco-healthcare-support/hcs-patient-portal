using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentTypeFieldConfigs;

[RemoteService(Name = "Default")]
[Area("app")]
[ControllerName("AppointmentTypeFieldConfig")]
[Route("api/app/appointment-type-field-configs")]
[ApiVersion("1.0")]
public class AppointmentTypeFieldConfigController : AbpController, IAppointmentTypeFieldConfigsAppService
{
    private readonly IAppointmentTypeFieldConfigsAppService _appService;

    public AppointmentTypeFieldConfigController(IAppointmentTypeFieldConfigsAppService appService)
    {
        _appService = appService;
    }

    [HttpGet("by-appointment-type/{appointmentTypeId}")]
    public Task<List<AppointmentTypeFieldConfigDto>> GetByAppointmentTypeIdAsync(Guid appointmentTypeId)
    {
        return _appService.GetByAppointmentTypeIdAsync(appointmentTypeId);
    }

    [HttpGet]
    public Task<List<AppointmentTypeFieldConfigDto>> GetListAsync(Guid? appointmentTypeId)
    {
        return _appService.GetListAsync(appointmentTypeId);
    }

    [HttpGet("{id}")]
    public Task<AppointmentTypeFieldConfigDto> GetAsync(Guid id)
    {
        return _appService.GetAsync(id);
    }

    [HttpPost]
    public Task<AppointmentTypeFieldConfigDto> CreateAsync(AppointmentTypeFieldConfigCreateDto input)
    {
        return _appService.CreateAsync(input);
    }

    [HttpPut("{id}")]
    public Task<AppointmentTypeFieldConfigDto> UpdateAsync(Guid id, AppointmentTypeFieldConfigUpdateDto input)
    {
        return _appService.UpdateAsync(id, input);
    }

    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _appService.DeleteAsync(id);
    }
}
