using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.States;
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
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.DefenseAttorneys.Default)]
public class DefenseAttorneysAppService : CaseEvaluationAppService, IDefenseAttorneysAppService
{
    protected IDefenseAttorneyRepository _defenseAttorneyRepository;
    protected DefenseAttorneyManager _defenseAttorneyManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected IRepository<AppointmentDefenseAttorney, Guid> _appointmentDefenseAttorneyRepository;

    public DefenseAttorneysAppService(IDefenseAttorneyRepository defenseAttorneyRepository, DefenseAttorneyManager defenseAttorneyManager, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, IRepository<AppointmentDefenseAttorney, Guid> appointmentDefenseAttorneyRepository)
    {
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _defenseAttorneyManager = defenseAttorneyManager;
        _stateRepository = stateRepository;
        _identityUserRepository = identityUserRepository;
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Default)]
    public virtual async Task<PagedResultDto<DefenseAttorneyWithNavigationPropertiesDto>> GetListAsync(GetDefenseAttorneysInput input)
    {
        var totalCount = await _defenseAttorneyRepository.GetCountAsync(input.FilterText, input.FirmName, input.PhoneNumber, input.City, input.StateId, input.IdentityUserId);
        var items = await _defenseAttorneyRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirmName, input.PhoneNumber, input.City, input.StateId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DefenseAttorneyWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DefenseAttorneyWithNavigationProperties>, List<DefenseAttorneyWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Default)]
    public virtual async Task<DefenseAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DefenseAttorneyWithNavigationProperties, DefenseAttorneyWithNavigationPropertiesDto>((await _defenseAttorneyRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Default)]
    public virtual async Task<DefenseAttorneyDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<DefenseAttorney, DefenseAttorneyDto>(await _defenseAttorneyRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.States.State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.States.State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Prompt 15 / item 32: block delete while any appointment references
        // this defense attorney (AppointmentDefenseAttorney.DefenseAttorneyId).
        if (await _appointmentDefenseAttorneyRepository.AnyAsync(x => x.DefenseAttorneyId == id))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.DefenseAttorneyInUse);
        }
        await _defenseAttorneyRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Create)]
    public virtual async Task<DefenseAttorneyDto> CreateAsync(DefenseAttorneyCreateDto input)
    {
        // BUG-042 / UM4 (2026-06-05): persist First/Last name (the manager already
        // accepts them) and allow a record with no login (identity now optional).
        var defenseAttorney = await _defenseAttorneyManager.CreateAsync(input.StateId, input.IdentityUserId, input.FirmName, input.FirmAddress, input.PhoneNumber, input.WebAddress, input.FaxNumber, input.Street, input.City, input.ZipCode, email: input.Email, firstName: input.FirstName, lastName: input.LastName);
        return ObjectMapper.Map<DefenseAttorney, DefenseAttorneyDto>(defenseAttorney);
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Edit)]
    public virtual async Task<DefenseAttorneyDto> UpdateAsync(Guid id, DefenseAttorneyUpdateDto input)
    {
        var defenseAttorney = await _defenseAttorneyManager.UpdateAsync(id, input.StateId, input.IdentityUserId, input.FirmName, input.FirmAddress, input.PhoneNumber, input.WebAddress, input.FaxNumber, input.Street, input.City, input.ZipCode, input.ConcurrencyStamp, email: input.Email, firstName: input.FirstName, lastName: input.LastName);
        return ObjectMapper.Map<DefenseAttorney, DefenseAttorneyDto>(defenseAttorney);
    }
}
