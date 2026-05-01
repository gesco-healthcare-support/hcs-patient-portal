using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentClaimExaminers;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentClaimExaminer")]
[Route("api/app/appointment-claim-examiners")]
public class AppointmentClaimExaminerController : AbpController, IAppointmentClaimExaminersAppService
{
    protected IAppointmentClaimExaminersAppService _service;

    public AppointmentClaimExaminerController(IAppointmentClaimExaminersAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentClaimExaminerDto>> GetListAsync(GetAppointmentClaimExaminersInput input)
        => _service.GetListAsync(input);

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentClaimExaminerDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
        => _service.GetStateLookupAsync(input);

    [HttpPost]
    public virtual Task<AppointmentClaimExaminerDto> CreateAsync(AppointmentClaimExaminerCreateDto input)
        => _service.CreateAsync(input);

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentClaimExaminerDto> UpdateAsync(Guid id, AppointmentClaimExaminerUpdateDto input)
        => _service.UpdateAsync(id, input);

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
        => _service.DeleteAsync(id);
}
