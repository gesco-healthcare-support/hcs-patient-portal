using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentDefenseAttorneys;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentDefenseAttorney")]
[Route("api/app/appointment-defense-attorneys")]
public class AppointmentDefenseAttorneyController : AbpController, IAppointmentDefenseAttorneysAppService
{
    protected IAppointmentDefenseAttorneysAppService _appointmentDefenseAttorneysAppService;

    public AppointmentDefenseAttorneyController(IAppointmentDefenseAttorneysAppService appointmentDefenseAttorneysAppService)
    {
        _appointmentDefenseAttorneysAppService = appointmentDefenseAttorneysAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentDefenseAttorneyWithNavigationPropertiesDto>> GetListAsync(GetAppointmentDefenseAttorneysInput input)
    {
        return _appointmentDefenseAttorneysAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<AppointmentDefenseAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _appointmentDefenseAttorneysAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentDefenseAttorneyDto> GetAsync(Guid id)
    {
        return _appointmentDefenseAttorneysAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("appointment-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        return _appointmentDefenseAttorneysAppService.GetAppointmentLookupAsync(input);
    }

    [HttpGet]
    [Route("defense-attorney-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDefenseAttorneyLookupAsync(LookupRequestDto input)
    {
        return _appointmentDefenseAttorneysAppService.GetDefenseAttorneyLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _appointmentDefenseAttorneysAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<AppointmentDefenseAttorneyDto> CreateAsync(AppointmentDefenseAttorneyCreateDto input)
    {
        return _appointmentDefenseAttorneysAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentDefenseAttorneyDto> UpdateAsync(Guid id, AppointmentDefenseAttorneyUpdateDto input)
    {
        return _appointmentDefenseAttorneysAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentDefenseAttorneysAppService.DeleteAsync(id);
    }
}
