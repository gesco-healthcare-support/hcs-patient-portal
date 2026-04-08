using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentTypes;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentType")]
[Route("api/app/appointment-types")]
public class AppointmentTypeController : AbpController, IAppointmentTypesAppService
{
    protected IAppointmentTypesAppService _appointmentTypesAppService;

    public AppointmentTypeController(IAppointmentTypesAppService appointmentTypesAppService)
    {
        _appointmentTypesAppService = appointmentTypesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentTypeDto>> GetListAsync(GetAppointmentTypesInput input)
    {
        return _appointmentTypesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentTypeDto> GetAsync(Guid id)
    {
        return _appointmentTypesAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<AppointmentTypeDto> CreateAsync(AppointmentTypeCreateDto input)
    {
        return _appointmentTypesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentTypeDto> UpdateAsync(Guid id, AppointmentTypeUpdateDto input)
    {
        return _appointmentTypesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentTypesAppService.DeleteAsync(id);
    }
}