using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentInjuryDetails;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentInjuryDetail")]
[Route("api/app/appointment-injury-details")]
public class AppointmentInjuryDetailController : AbpController, IAppointmentInjuryDetailsAppService
{
    protected IAppointmentInjuryDetailsAppService _service;

    public AppointmentInjuryDetailController(IAppointmentInjuryDetailsAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentInjuryDetailWithNavigationPropertiesDto>> GetListAsync(GetAppointmentInjuryDetailsInput input)
        => _service.GetListAsync(input);

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<AppointmentInjuryDetailWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
        => _service.GetWithNavigationPropertiesAsync(id);

    [HttpGet]
    [Route("by-appointment/{appointmentId}")]
    public virtual Task<List<AppointmentInjuryDetailWithNavigationPropertiesDto>> GetByAppointmentIdAsync(Guid appointmentId)
        => _service.GetByAppointmentIdAsync(appointmentId);

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentInjuryDetailDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    [HttpGet]
    [Route("wcab-office-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWcabOfficeLookupAsync(LookupRequestDto input)
        => _service.GetWcabOfficeLookupAsync(input);

    [HttpPost]
    public virtual Task<AppointmentInjuryDetailDto> CreateAsync(AppointmentInjuryDetailCreateDto input)
        => _service.CreateAsync(input);

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentInjuryDetailDto> UpdateAsync(Guid id, AppointmentInjuryDetailUpdateDto input)
        => _service.UpdateAsync(id, input);

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
        => _service.DeleteAsync(id);
}
