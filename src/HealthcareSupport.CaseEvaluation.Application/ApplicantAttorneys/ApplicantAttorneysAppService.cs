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
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;

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
    protected IRepository<AppointmentApplicantAttorney, Guid> _appointmentApplicantAttorneyRepository;

    // 2026-08-17: renders the *.InUse delete guards as their real message. Without it the
    // raw BusinessException reaches the SPA with no message and the toast falls back to
    // ABP's generic "An internal error occurred during your request!".
    public ApplicantAttorneysAppService(IApplicantAttorneyRepository applicantAttorneyRepository, ApplicantAttorneyManager applicantAttorneyManager, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, IRepository<AppointmentApplicantAttorney, Guid> appointmentApplicantAttorneyRepository)
    {
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _applicantAttorneyManager = applicantAttorneyManager;
        _stateRepository = stateRepository;
        _identityUserRepository = identityUserRepository;
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]
    public virtual async Task<PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto>> GetListAsync(GetApplicantAttorneysInput input)
    {
        // External roles hold .Default, so they only ever see their own master row. The narrowing is in
        // the query filter, so neither the items nor the total count describe anyone else's record.
        var identityUserFilter = IsInternalCaller() ? input.IdentityUserId : RequireCallerId();
        var totalCount = await _applicantAttorneyRepository.GetCountAsync(input.FilterText, input.FirmName, input.PhoneNumber, input.City, input.StateId, identityUserFilter);
        var items = await _applicantAttorneyRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirmName, input.PhoneNumber, input.City, input.StateId, identityUserFilter, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<ApplicantAttorneyWithNavigationProperties>, List<ApplicantAttorneyWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]
    public virtual async Task<ApplicantAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var item = await _applicantAttorneyRepository.GetWithNavigationPropertiesAsync(id);
        EnsureCallerMayAccess(item?.ApplicantAttorney, id);
        return ObjectMapper.Map<ApplicantAttorneyWithNavigationProperties, ApplicantAttorneyWithNavigationPropertiesDto>(item!);
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]
    public virtual async Task<ApplicantAttorneyDto> GetAsync(Guid id)
    {
        var existing = await _applicantAttorneyRepository.GetAsync(id);
        EnsureCallerMayAccess(existing, id);
        return ObjectMapper.Map<ApplicantAttorney, ApplicantAttorneyDto>(existing);
    }

    // Plain [Authorize] (inherited from class): any authenticated booker can
    // read the State lookup for the AA section of the booking form.
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

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]
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

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        if (!IsInternalCaller())
        {
            EnsureCallerMayAccess(await _applicantAttorneyRepository.FindAsync(id), id);
        }
        // Prompt 15 / item 32: block delete while any appointment references
        // this applicant attorney (AppointmentApplicantAttorney.ApplicantAttorneyId).
        if (await _appointmentApplicantAttorneyRepository.AnyAsync(x => x.ApplicantAttorneyId == id))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ApplicantAttorneyInUse);
        }
        await _applicantAttorneyRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Create)]
    public virtual async Task<ApplicantAttorneyDto> CreateAsync(ApplicantAttorneyCreateDto input)
    {
        // An external caller may create only a master bound to their own login; binding one to
        // someone else's IdentityUserId would let them plant a record that user is later matched to.
        if (!IsInternalCaller() && input.IdentityUserId != RequireCallerId())
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        }
        // BUG-042 / UM4 (2026-06-05): persist First/Last name (the manager already
        // accepts them) and allow a record with no login (identity now optional).
        var applicantAttorney = await _applicantAttorneyManager.CreateAsync(input.StateId, input.IdentityUserId, input.FirmName, input.FirmAddress, input.PhoneNumber, input.WebAddress, input.FaxNumber, input.Street, input.City, input.ZipCode, email: input.Email, firstName: input.FirstName, lastName: input.LastName);
        return ObjectMapper.Map<ApplicantAttorney, ApplicantAttorneyDto>(applicantAttorney);
    }

    [Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Edit)]
    public virtual async Task<ApplicantAttorneyDto> UpdateAsync(Guid id, ApplicantAttorneyUpdateDto input)
    {
        var identityUserId = input.IdentityUserId;
        var email = input.Email;
        if (!IsInternalCaller())
        {
            // External callers may edit only their own master and may not re-point its login or its
            // email (the email is a notification recipient). MyAttorneyProfileAppService preserves
            // both the same way.
            var existing = await _applicantAttorneyRepository.GetAsync(id);
            EnsureCallerMayAccess(existing, id);
            identityUserId = existing.IdentityUserId;
            email = existing.Email;
        }
        var applicantAttorney = await _applicantAttorneyManager.UpdateAsync(id, input.StateId, identityUserId, input.FirmName, input.FirmAddress, input.PhoneNumber, input.WebAddress, input.FaxNumber, input.Street, input.City, input.ZipCode, input.ConcurrencyStamp, email: email, firstName: input.FirstName, lastName: input.LastName);
        return ObjectMapper.Map<ApplicantAttorney, ApplicantAttorneyDto>(applicantAttorney);
    }

    private bool IsInternalCaller() => BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles);

    private Guid RequireCallerId() => CurrentUser.Id ?? throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);

    /// <summary>
    /// Internal staff pass. An external caller passes only for the master row bound to their own login;
    /// a missing row and someone else's row raise the same refusal, so ids cannot be probed.
    /// </summary>
    private void EnsureCallerMayAccess(ApplicantAttorney? row, Guid id)
    {
        if (IsInternalCaller())
        {
            if (row == null)
            {
                throw new EntityNotFoundException(typeof(ApplicantAttorney), id);
            }
            return;
        }
        if (row == null || row.IdentityUserId == null || row.IdentityUserId != RequireCallerId())
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        }
    }
}