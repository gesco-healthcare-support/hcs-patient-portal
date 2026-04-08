using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.States;

namespace HealthcareSupport.CaseEvaluation.Controllers.States;

[RemoteService]
[Area("app")]
[ControllerName("State")]
[Route("api/app/states")]
public class StateController : AbpController, IStatesAppService
{
    protected IStatesAppService _statesAppService;

    public StateController(IStatesAppService statesAppService)
    {
        _statesAppService = statesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<StateDto>> GetListAsync(GetStatesInput input)
    {
        return _statesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<StateDto> GetAsync(Guid id)
    {
        return _statesAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<StateDto> CreateAsync(StateCreateDto input)
    {
        return _statesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<StateDto> UpdateAsync(Guid id, StateUpdateDto input)
    {
        return _statesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _statesAppService.DeleteAsync(id);
    }
}