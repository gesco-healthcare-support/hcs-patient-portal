using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentPrimaryInsurances;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentPrimaryInsurance")]
[Route("api/app/appointment-primary-insurances")]
public class AppointmentPrimaryInsuranceController : AbpController, IAppointmentPrimaryInsurancesAppService
{
    protected IAppointmentPrimaryInsurancesAppService _service;

    public AppointmentPrimaryInsuranceController(IAppointmentPrimaryInsurancesAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentPrimaryInsuranceDto>> GetListAsync(GetAppointmentPrimaryInsurancesInput input)
        => _service.GetListAsync(input);

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentPrimaryInsuranceDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
        => _service.GetStateLookupAsync(input);

    [HttpPost]
    public virtual Task<AppointmentPrimaryInsuranceDto> CreateAsync(AppointmentPrimaryInsuranceCreateDto input)
        => _service.CreateAsync(input);

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentPrimaryInsuranceDto> UpdateAsync(Guid id, AppointmentPrimaryInsuranceUpdateDto input)
        => _service.UpdateAsync(id, input);

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
        => _service.DeleteAsync(id);
}
