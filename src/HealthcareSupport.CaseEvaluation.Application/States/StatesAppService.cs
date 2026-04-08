using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.States;

namespace HealthcareSupport.CaseEvaluation.States;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.States.Default)]
public class StatesAppService : CaseEvaluationAppService, IStatesAppService
{
    protected IStateRepository _stateRepository;
    protected StateManager _stateManager;

    public StatesAppService(IStateRepository stateRepository, StateManager stateManager)
    {
        _stateRepository = stateRepository;
        _stateManager = stateManager;
    }

    public virtual async Task<PagedResultDto<StateDto>> GetListAsync(GetStatesInput input)
    {
        var totalCount = await _stateRepository.GetCountAsync(input.FilterText, input.Name);
        var items = await _stateRepository.GetListAsync(input.FilterText, input.Name, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<StateDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<State>, List<StateDto>>(items)
        };
    }

    public virtual async Task<StateDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<State, StateDto>(await _stateRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.States.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _stateRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.States.Create)]
    public virtual async Task<StateDto> CreateAsync(StateCreateDto input)
    {
        var state = await _stateManager.CreateAsync(input.Name);
        return ObjectMapper.Map<State, StateDto>(state);
    }

    [Authorize(CaseEvaluationPermissions.States.Edit)]
    public virtual async Task<StateDto> UpdateAsync(Guid id, StateUpdateDto input)
    {
        var state = await _stateManager.UpdateAsync(id, input.Name, input.ConcurrencyStamp);
        return ObjectMapper.Map<State, StateDto>(state);
    }
}