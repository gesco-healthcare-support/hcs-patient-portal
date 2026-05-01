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
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

[RemoteService(IsEnabled = false)]
// Class-level demoted from ApplicantAttorneys.Default to plain [Authorize].
// Booker-side `GetStateLookupAsync` needs to be callable by any authenticated
// user; admin-side list/get methods keep ApplicantAttorneys.Default at the
// per-method level so AA enumeration stays gated. (Step 1.4 / W-A-3, 2026-04-30.)
[Authorize]
public class ApplicantAttorneysAppService : CaseEvaluationAppService, IApplicantAttorneysAppService
{
    protected IApplicantAttorneyRepository _applicantAttorneyRepository;
    protected ApplicantAttorneyManager _applicantAttorneyManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public ApplicantAttorneysAppService(IApplicantAttorneyRepository applicantAttorneyRepository, ApplicantAttorneyManager applicantAttorneyManager, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _applicantAttorneyManager = applicantAttorneyManager;
        _stateRepository = stateRepository;
        _identityUserRepository = identityUserRepository;
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]
    public virtual async Task<PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto>> GetListAsync(GetApplicantAttorneysInput input)
    {
        var totalCount = await _applicantAttorneyRepository.GetCountAsync(input.FilterText, input.FirmName, input.PhoneNumber, input.City, input.StateId, input.IdentityUserId);
        var items = await _applicantAttorneyRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirmName, input.PhoneNumber, input.City, input.StateId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<ApplicantAttorneyWithNavigationProperties>, List<ApplicantAttorneyWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]
    public virtual async Task<ApplicantAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<ApplicantAttorneyWithNavigationProperties, ApplicantAttorneyWithNavigationPropertiesDto>((await _applicantAttorneyRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]
    public virtual async Task<ApplicantAttorneyDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<ApplicantAttorney, ApplicantAttorneyDto>(await _applicantAttorneyRepository.GetAsync(id));
    }

    // Plain [Authorize] (inherited from class): any authenticated booker can
    // read the State lookup for the AA section of the booking form.
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.States.State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.States.State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]
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

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _applicantAttorneyRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Create)]
    public virtual async Task<ApplicantAttorneyDto> CreateAsync(ApplicantAttorneyCreateDto input)
    {
        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var applicantAttorney = await _applicantAttorneyManager.CreateAsync(input.StateId, input.IdentityUserId, input.FirmName, input.FirmAddress, input.PhoneNumber, input.WebAddress, input.FaxNumber, input.Street, input.City, input.ZipCode);
        return ObjectMapper.Map<ApplicantAttorney, ApplicantAttorneyDto>(applicantAttorney);
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Edit)]
    public virtual async Task<ApplicantAttorneyDto> UpdateAsync(Guid id, ApplicantAttorneyUpdateDto input)
    {
        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var applicantAttorney = await _applicantAttorneyManager.UpdateAsync(id, input.StateId, input.IdentityUserId, input.FirmName, input.FirmAddress, input.PhoneNumber, input.WebAddress, input.FaxNumber, input.Street, input.City, input.ZipCode, input.ConcurrencyStamp);
        return ObjectMapper.Map<ApplicantAttorney, ApplicantAttorneyDto>(applicantAttorney);
    }
}