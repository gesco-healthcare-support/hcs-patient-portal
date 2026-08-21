using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentDocumentTypes;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentDocumentType")]
[Route("api/app/appointment-document-types")]
public class AppointmentDocumentTypeController : AbpController, IAppointmentDocumentTypesAppService
{
    protected IAppointmentDocumentTypesAppService _appointmentDocumentTypesAppService;

    public AppointmentDocumentTypeController(IAppointmentDocumentTypesAppService appointmentDocumentTypesAppService)
    {
        _appointmentDocumentTypesAppService = appointmentDocumentTypesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentDocumentTypeDto>> GetListAsync(GetAppointmentDocumentTypesInput input)
    {
        return _appointmentDocumentTypesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentDocumentTypeDto> GetAsync(Guid id)
    {
        return _appointmentDocumentTypesAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<AppointmentDocumentTypeDto> CreateAsync(AppointmentDocumentTypeCreateDto input)
    {
        return _appointmentDocumentTypesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentDocumentTypeDto> UpdateAsync(Guid id, AppointmentDocumentTypeUpdateDto input)
    {
        return _appointmentDocumentTypesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentDocumentTypesAppService.DeleteAsync(id);
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> appointmentDocumentTypeIds)
    {
        return _appointmentDocumentTypesAppService.DeleteByIdsAsync(appointmentDocumentTypeIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetAppointmentDocumentTypesInput input)
    {
        return _appointmentDocumentTypesAppService.DeleteAllAsync(input);
    }
}
