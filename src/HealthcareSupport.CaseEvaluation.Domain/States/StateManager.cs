using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.States;

public class StateManager : DomainService
{
    protected IStateRepository _stateRepository;

    public StateManager(IStateRepository stateRepository)
    {
        _stateRepository = stateRepository;
    }

    public virtual async Task<State> CreateAsync(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        var state = new State(GuidGenerator.Create(), name);
        return await _stateRepository.InsertAsync(state);
    }

    public virtual async Task<State> UpdateAsync(Guid id, string name, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        var state = await _stateRepository.GetAsync(id);
        state.Name = name;
        state.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _stateRepository.UpdateAsync(state);
    }
}