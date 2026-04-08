using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using Volo.Abp.Content;
using HealthcareSupport.CaseEvaluation.Shared;

namespace HealthcareSupport.CaseEvaluation.Controllers.WcabOffices;

[RemoteService]
[Area("app")]
[ControllerName("WcabOffice")]
[Route("api/app/wcab-offices")]
public class WcabOfficeController : AbpController, IWcabOfficesAppService
{
    protected IWcabOfficesAppService _wcabOfficesAppService;

    public WcabOfficeController(IWcabOfficesAppService wcabOfficesAppService)
    {
        _wcabOfficesAppService = wcabOfficesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<WcabOfficeWithNavigationPropertiesDto>> GetListAsync(GetWcabOfficesInput input)
    {
        return _wcabOfficesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<WcabOfficeWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _wcabOfficesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<WcabOfficeDto> GetAsync(Guid id)
    {
        return _wcabOfficesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        return _wcabOfficesAppService.GetStateLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<WcabOfficeDto> CreateAsync(WcabOfficeCreateDto input)
    {
        return _wcabOfficesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<WcabOfficeDto> UpdateAsync(Guid id, WcabOfficeUpdateDto input)
    {
        return _wcabOfficesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _wcabOfficesAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(WcabOfficeExcelDownloadDto input)
    {
        return _wcabOfficesAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HealthcareSupport.CaseEvaluation.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _wcabOfficesAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> wcabofficeIds)
    {
        return _wcabOfficesAppService.DeleteByIdsAsync(wcabofficeIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetWcabOfficesInput input)
    {
        return _wcabOfficesAppService.DeleteAllAsync(input);
    }
}