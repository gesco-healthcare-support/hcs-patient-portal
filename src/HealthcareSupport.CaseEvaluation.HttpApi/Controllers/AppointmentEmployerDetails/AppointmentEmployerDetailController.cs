using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentEmployerDetails;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentEmployerDetail")]
[Route("api/app/appointment-employer-details")]
public class AppointmentEmployerDetailController : AbpController, IAppointmentEmployerDetailsAppService
{
    protected IAppointmentEmployerDetailsAppService _appointmentEmployerDetailsAppService;

    public AppointmentEmployerDetailController(IAppointmentEmployerDetailsAppService appointmentEmployerDetailsAppService)
    {
        _appointmentEmployerDetailsAppService = appointmentEmployerDetailsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentEmployerDetailWithNavigationPropertiesDto>> GetListAsync(GetAppointmentEmployerDetailsInput input)
    {
        return _appointmentEmployerDetailsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<AppointmentEmployerDetailWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _appointmentEmployerDetailsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentEmployerDetailDto> GetAsync(Guid id)
    {
        return _appointmentEmployerDetailsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("appointment-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        return _appointmentEmployerDetailsAppService.GetAppointmentLookupAsync(input);
    }

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        return _appointmentEmployerDetailsAppService.GetStateLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<AppointmentEmployerDetailDto> CreateAsync(AppointmentEmployerDetailCreateDto input)
    {
        return _appointmentEmployerDetailsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentEmployerDetailDto> UpdateAsync(Guid id, AppointmentEmployerDetailUpdateDto input)
    {
        return _appointmentEmployerDetailsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentEmployerDetailsAppService.DeleteAsync(id);
    }
}