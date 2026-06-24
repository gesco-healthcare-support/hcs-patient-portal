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
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.States;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.States.Default)]
public class StatesAppService : CaseEvaluationAppService, IStatesAppService
{
    protected IStateRepository _stateRepository;
    protected StateManager _stateManager;
    protected IRepository<Location, Guid> _locationRepository;
    protected IRepository<WcabOffice, Guid> _wcabOfficeRepository;
    protected IRepository<Patient, Guid> _patientRepository;
    protected IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    protected IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;
    protected IRepository<ClaimExaminer, Guid> _claimExaminerRepository;

    // State is host-scoped; the referencing masters are mostly tenant-scoped.
    // Disabling the IMultiTenant filter when summing references makes the
    // UsageCount reflect every tenant. Mirrors StateManager.EnsureNotInUseAsync.
    private readonly IDataFilter<IMultiTenant> _multiTenantFilter;

    public StatesAppService(
        IStateRepository stateRepository,
        StateManager stateManager,
        IRepository<Location, Guid> locationRepository,
        IRepository<WcabOffice, Guid> wcabOfficeRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository,
        IRepository<DefenseAttorney, Guid> defenseAttorneyRepository,
        IRepository<ClaimExaminer, Guid> claimExaminerRepository,
        IDataFilter<IMultiTenant> multiTenantFilter)
    {
        _stateRepository = stateRepository;
        _stateManager = stateManager;
        _locationRepository = locationRepository;
        _wcabOfficeRepository = wcabOfficeRepository;
        _patientRepository = patientRepository;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _multiTenantFilter = multiTenantFilter;
    }

    public virtual async Task<PagedResultDto<StateDto>> GetListAsync(GetStatesInput input)
    {
        var totalCount = await _stateRepository.GetCountAsync(input.FilterText, input.Name);
        var items = await _stateRepository.GetListAsync(input.FilterText, input.Name, input.Sorting, input.MaxResultCount, input.SkipCount);
        var dtoItems = ObjectMapper.Map<List<State>, List<StateDto>>(items);
        // Prompt 15 / item 32: per-row UsageCount = total references across the
        // entity masters that carry a StateId. Filter disabled so references in
        // any tenant are counted against the shared host-scoped State.
        using (_multiTenantFilter.Disable())
        {
            foreach (var dto in dtoItems)
            {
                dto.UsageCount = await CountStateReferencesAsync(dto.Id);
            }
        }
        return new PagedResultDto<StateDto>
        {
            TotalCount = totalCount,
            Items = dtoItems
        };
    }

    private async Task<int> CountStateReferencesAsync(Guid id)
    {
        var total = await _locationRepository.CountAsync(x => x.StateId == id)
            + await _wcabOfficeRepository.CountAsync(x => x.StateId == id)
            + await _patientRepository.CountAsync(x => x.StateId == id)
            + await _applicantAttorneyRepository.CountAsync(x => x.StateId == id)
            + await _defenseAttorneyRepository.CountAsync(x => x.StateId == id)
            + await _claimExaminerRepository.CountAsync(x => x.StateId == id);
        return (int)total;
    }

    public virtual async Task<StateDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<State, StateDto>(await _stateRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.States.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Route through the manager so the system-row + in-use guards apply.
        await _stateManager.DeleteAsync(id);
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