using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using HealthcareSupport.CaseEvaluation.Permissions;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

[RemoteService(IsEnabled = false)]
// Class-level plain [Authorize] so the booker-side state lookup is callable by any
// authenticated user; admin list/get/CRUD keep ClaimExaminers.* at the method level.
[Authorize]
public class ClaimExaminersAppService : CaseEvaluationAppService, IClaimExaminersAppService
{
    protected IClaimExaminerRepository _claimExaminerRepository;
    protected ClaimExaminerManager _claimExaminerManager;
    protected IRepository<State, Guid> _stateRepository;
    protected IRepository<IdentityUser, Guid> _identityUserRepository;

    public ClaimExaminersAppService(IClaimExaminerRepository claimExaminerRepository, ClaimExaminerManager claimExaminerManager, IRepository<State, Guid> stateRepository, IRepository<IdentityUser, Guid> identityUserRepository)
    {
        _claimExaminerRepository = claimExaminerRepository;
        _claimExaminerManager = claimExaminerManager;
        _stateRepository = stateRepository;
        _identityUserRepository = identityUserRepository;
    }

    [Authorize(CaseEvaluationPermissions.ClaimExaminers.Default)]
    public virtual async Task<PagedResultDto<ClaimExaminerWithNavigationPropertiesDto>> GetListAsync(GetClaimExaminersInput input)
    {
        var totalCount = await _claimExaminerRepository.GetCountAsync(input.FilterText, input.Email, input.PhoneNumber, input.City, input.StateId, input.IdentityUserId);
        var items = await _claimExaminerRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Email, input.PhoneNumber, input.City, input.StateId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<ClaimExaminerWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<ClaimExaminerWithNavigationProperties>, List<ClaimExaminerWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.ClaimExaminers.Default)]
    public virtual async Task<ClaimExaminerWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<ClaimExaminerWithNavigationProperties, ClaimExaminerWithNavigationPropertiesDto>((await _claimExaminerRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    [Authorize(CaseEvaluationPermissions.ClaimExaminers.Default)]
    public virtual async Task<ClaimExaminerDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<ClaimExaminer, ClaimExaminerDto>(await _claimExaminerRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.ClaimExaminers.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.ClaimExaminers.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _claimExaminerRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.ClaimExaminers.Create)]
    public virtual async Task<ClaimExaminerDto> CreateAsync(ClaimExaminerCreateDto input)
    {
        // IP6/UM4 record-based: identity is optional (no required-identity gate, unlike
        // the legacy attorney AppServices). Names + email + contact persist on the master;
        // the identity links later on self-register by email.
        var claimExaminer = await _claimExaminerManager.CreateAsync(input.StateId, input.IdentityUserId, input.PhoneNumber, input.FaxNumber, input.Street, input.City, input.ZipCode, input.Email, input.FirstName, input.LastName);
        return ObjectMapper.Map<ClaimExaminer, ClaimExaminerDto>(claimExaminer);
    }

    [Authorize(CaseEvaluationPermissions.ClaimExaminers.Edit)]
    public virtual async Task<ClaimExaminerDto> UpdateAsync(Guid id, ClaimExaminerUpdateDto input)
    {
        var claimExaminer = await _claimExaminerManager.UpdateAsync(id, input.StateId, input.IdentityUserId, input.PhoneNumber, input.FaxNumber, input.Street, input.City, input.ZipCode, input.ConcurrencyStamp, input.Email, input.FirstName, input.LastName);
        return ObjectMapper.Map<ClaimExaminer, ClaimExaminerDto>(claimExaminer);
    }
}
