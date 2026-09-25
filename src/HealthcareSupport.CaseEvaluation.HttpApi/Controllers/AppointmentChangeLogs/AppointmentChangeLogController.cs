using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentChangeLogs;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentChangeLog")]
[Route("api/app/appointment-change-logs")]
public class AppointmentChangeLogController : AbpController, IAppointmentChangeLogsAppService
{
    private readonly IAppointmentChangeLogsAppService _appointmentChangeLogsAppService;

    public AppointmentChangeLogController(IAppointmentChangeLogsAppService appointmentChangeLogsAppService)
    {
        _appointmentChangeLogsAppService = appointmentChangeLogsAppService;
    }

    [HttpGet]
    [Route("by-appointment/{appointmentId}")]
    public virtual Task<List<AppointmentChangeLogDto>> GetByAppointmentAsync(Guid appointmentId)
    {
        return _appointmentChangeLogsAppService.GetByAppointmentAsync(appointmentId);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentChangeLogDto>> GetListAsync(GetAppointmentChangeLogsInput input)
    {
        return _appointmentChangeLogsAppService.GetListAsync(input);
    }
}
