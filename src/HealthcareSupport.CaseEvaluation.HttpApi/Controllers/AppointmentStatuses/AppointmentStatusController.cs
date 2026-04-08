using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentStatuses;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentStatuses;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentStatus")]
[Route("api/app/appointment-statuses")]
public class AppointmentStatusController : AbpController, IAppointmentStatusesAppService
{
    protected IAppointmentStatusesAppService _appointmentStatusesAppService;

    public AppointmentStatusController(IAppointmentStatusesAppService appointmentStatusesAppService)
    {
        _appointmentStatusesAppService = appointmentStatusesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentStatusDto>> GetListAsync(GetAppointmentStatusesInput input)
    {
        return _appointmentStatusesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentStatusDto> GetAsync(Guid id)
    {
        return _appointmentStatusesAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<AppointmentStatusDto> CreateAsync(AppointmentStatusCreateDto input)
    {
        return _appointmentStatusesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentStatusDto> UpdateAsync(Guid id, AppointmentStatusUpdateDto input)
    {
        return _appointmentStatusesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentStatusesAppService.DeleteAsync(id);
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> appointmentstatusIds)
    {
        return _appointmentStatusesAppService.DeleteByIdsAsync(appointmentstatusIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetAppointmentStatusesInput input)
    {
        return _appointmentStatusesAppService.DeleteAllAsync(input);
    }
}