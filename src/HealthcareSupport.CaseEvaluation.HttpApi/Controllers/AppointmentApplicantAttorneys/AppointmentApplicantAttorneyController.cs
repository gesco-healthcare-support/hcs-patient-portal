using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentApplicantAttorneys;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentApplicantAttorney")]
[Route("api/app/appointment-applicant-attorneys")]
public class AppointmentApplicantAttorneyController : AbpController, IAppointmentApplicantAttorneysAppService
{
    protected IAppointmentApplicantAttorneysAppService _appointmentApplicantAttorneysAppService;

    public AppointmentApplicantAttorneyController(IAppointmentApplicantAttorneysAppService appointmentApplicantAttorneysAppService)
    {
        _appointmentApplicantAttorneysAppService = appointmentApplicantAttorneysAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentApplicantAttorneyWithNavigationPropertiesDto>> GetListAsync(GetAppointmentApplicantAttorneysInput input)
    {
        return _appointmentApplicantAttorneysAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<AppointmentApplicantAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _appointmentApplicantAttorneysAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentApplicantAttorneyDto> GetAsync(Guid id)
    {
        return _appointmentApplicantAttorneysAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("appointment-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        return _appointmentApplicantAttorneysAppService.GetAppointmentLookupAsync(input);
    }

    [HttpGet]
    [Route("applicant-attorney-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetApplicantAttorneyLookupAsync(LookupRequestDto input)
    {
        return _appointmentApplicantAttorneysAppService.GetApplicantAttorneyLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _appointmentApplicantAttorneysAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<AppointmentApplicantAttorneyDto> CreateAsync(AppointmentApplicantAttorneyCreateDto input)
    {
        return _appointmentApplicantAttorneysAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentApplicantAttorneyDto> UpdateAsync(Guid id, AppointmentApplicantAttorneyUpdateDto input)
    {
        return _appointmentApplicantAttorneysAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentApplicantAttorneysAppService.DeleteAsync(id);
    }
}