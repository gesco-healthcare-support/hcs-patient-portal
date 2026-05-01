using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentBodyParts;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentBodyPart")]
[Route("api/app/appointment-body-parts")]
public class AppointmentBodyPartController : AbpController, IAppointmentBodyPartsAppService
{
    protected IAppointmentBodyPartsAppService _service;

    public AppointmentBodyPartController(IAppointmentBodyPartsAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentBodyPartDto>> GetListAsync(GetAppointmentBodyPartsInput input)
        => _service.GetListAsync(input);

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentBodyPartDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    [HttpPost]
    public virtual Task<AppointmentBodyPartDto> CreateAsync(AppointmentBodyPartCreateDto input)
        => _service.CreateAsync(input);

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentBodyPartDto> UpdateAsync(Guid id, AppointmentBodyPartUpdateDto input)
        => _service.UpdateAsync(id, input);

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
        => _service.DeleteAsync(id);
}
