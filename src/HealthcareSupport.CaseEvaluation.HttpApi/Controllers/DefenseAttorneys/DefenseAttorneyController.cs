using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;

namespace HealthcareSupport.CaseEvaluation.Controllers.DefenseAttorneys;

[RemoteService]
[Area("app")]
[ControllerName("DefenseAttorney")]
[Route("api/app/defense-attorneys")]
public class DefenseAttorneyController : AbpController, IDefenseAttorneysAppService
{
    protected IDefenseAttorneysAppService _defenseAttorneysAppService;

    public DefenseAttorneyController(IDefenseAttorneysAppService defenseAttorneysAppService)
    {
        _defenseAttorneysAppService = defenseAttorneysAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DefenseAttorneyWithNavigationPropertiesDto>> GetListAsync(GetDefenseAttorneysInput input)
    {
        return _defenseAttorneysAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DefenseAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _defenseAttorneysAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DefenseAttorneyDto> GetAsync(Guid id)
    {
        return _defenseAttorneysAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        return _defenseAttorneysAppService.GetStateLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _defenseAttorneysAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DefenseAttorneyDto> CreateAsync(DefenseAttorneyCreateDto input)
    {
        return _defenseAttorneysAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DefenseAttorneyDto> UpdateAsync(Guid id, DefenseAttorneyUpdateDto input)
    {
        return _defenseAttorneysAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _defenseAttorneysAppService.DeleteAsync(id);
    }
}
