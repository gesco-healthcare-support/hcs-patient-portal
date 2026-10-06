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
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;

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

    // 2026-08-17: renders the *.InUse delete guards as their real message. Without it the
    // raw BusinessException reaches the SPA with no message and the toast falls back to
    // ABP's generic "An internal error occurred during your request!".
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
        // External roles hold .Default, so they only ever see their own master row. The narrowing is in
        // the query filter, so neither the items nor the total count describe anyone else's record.
        var identityUserFilter = IsInternalCaller() ? input.IdentityUserId : RequireCallerId();
        var totalCount = await _defenseAttorneyRepository.GetCountAsync(input.FilterText, input.FirmName, input.PhoneNumber, input.City, input.StateId, identityUserFilter);
        var items = await _defenseAttorneyRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirmName, input.PhoneNumber, input.City, input.StateId, identityUserFilter, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DefenseAttorneyWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DefenseAttorneyWithNavigationProperties>, List<DefenseAttorneyWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Default)]
    public virtual async Task<DefenseAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var item = await _defenseAttorneyRepository.GetWithNavigationPropertiesAsync(id);
        EnsureCallerMayAccess(item?.DefenseAttorney, id);
        return ObjectMapper.Map<DefenseAttorneyWithNavigationProperties, DefenseAttorneyWithNavigationPropertiesDto>(item!);
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Default)]
    public virtual async Task<DefenseAttorneyDto> GetAsync(Guid id)
    {
        var existing = await _defenseAttorneyRepository.GetAsync(id);
        EnsureCallerMayAccess(existing, id);
        return ObjectMapper.Map<DefenseAttorney, DefenseAttorneyDto>(existing);
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
        var query = IdentityUserLookupScope.ForCaller((await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter!)), CurrentUser);
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
        if (!IsInternalCaller())
        {
            EnsureCallerMayAccess(await _defenseAttorneyRepository.FindAsync(id), id);
        }
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
        // An external caller may create only a master bound to their own login; binding one to
        // someone else's IdentityUserId would let them plant a record that user is later matched to.
        if (!IsInternalCaller() && input.IdentityUserId != RequireCallerId())
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        }
        // BUG-042 / UM4 (2026-06-05): persist First/Last name (the manager already
        // accepts them) and allow a record with no login (identity now optional).
        var defenseAttorney = await _defenseAttorneyManager.CreateAsync(input.StateId, input.IdentityUserId, input.FirmName, input.FirmAddress, input.PhoneNumber, input.WebAddress, input.FaxNumber, input.Street, input.City, input.ZipCode, email: input.Email, firstName: input.FirstName, lastName: input.LastName);
        return ObjectMapper.Map<DefenseAttorney, DefenseAttorneyDto>(defenseAttorney);
    }

    [Authorize(CaseEvaluationPermissions.DefenseAttorneys.Edit)]
    public virtual async Task<DefenseAttorneyDto> UpdateAsync(Guid id, DefenseAttorneyUpdateDto input)
    {
        var identityUserId = input.IdentityUserId;
        var email = input.Email;
        if (!IsInternalCaller())
        {
            // External callers may edit only their own master and may not re-point its login or its
            // email (the email is a notification recipient). MyAttorneyProfileAppService preserves
            // both the same way.
            var existing = await _defenseAttorneyRepository.GetAsync(id);
            EnsureCallerMayAccess(existing, id);
            identityUserId = existing.IdentityUserId;
            email = existing.Email;
        }
        var defenseAttorney = await _defenseAttorneyManager.UpdateAsync(id, input.StateId, identityUserId, input.FirmName, input.FirmAddress, input.PhoneNumber, input.WebAddress, input.FaxNumber, input.Street, input.City, input.ZipCode, input.ConcurrencyStamp, email: email, firstName: input.FirstName, lastName: input.LastName);
        return ObjectMapper.Map<DefenseAttorney, DefenseAttorneyDto>(defenseAttorney);
    }

    private bool IsInternalCaller() => BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles);

    private Guid RequireCallerId() => CurrentUser.Id ?? throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);

    /// <summary>
    /// Internal staff pass. An external caller passes only for the master row bound to their own login;
    /// a missing row and someone else's row raise the same refusal, so ids cannot be probed.
    /// </summary>
    private void EnsureCallerMayAccess(DefenseAttorney? row, Guid id)
    {
        if (IsInternalCaller())
        {
            if (row == null)
            {
                throw new EntityNotFoundException(typeof(DefenseAttorney), id);
            }
            return;
        }
        if (row == null || row.IdentityUserId == null || row.IdentityUserId != RequireCallerId())
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        }
    }
}
