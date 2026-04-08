using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentLanguages;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentLanguage")]
[Route("api/app/appointment-languages")]
public class AppointmentLanguageController : AbpController, IAppointmentLanguagesAppService
{
    protected IAppointmentLanguagesAppService _appointmentLanguagesAppService;

    public AppointmentLanguageController(IAppointmentLanguagesAppService appointmentLanguagesAppService)
    {
        _appointmentLanguagesAppService = appointmentLanguagesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentLanguageDto>> GetListAsync(GetAppointmentLanguagesInput input)
    {
        return _appointmentLanguagesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentLanguageDto> GetAsync(Guid id)
    {
        return _appointmentLanguagesAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<AppointmentLanguageDto> CreateAsync(AppointmentLanguageCreateDto input)
    {
        return _appointmentLanguagesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentLanguageDto> UpdateAsync(Guid id, AppointmentLanguageUpdateDto input)
    {
        return _appointmentLanguagesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentLanguagesAppService.DeleteAsync(id);
    }
}