using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.Identity;
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
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentAccessorsAppService : CaseEvaluationAppService, IAppointmentAccessorsAppService
{
    protected IAppointmentAccessorRepository _appointmentAccessorRepository;
    protected AppointmentAccessorManager _appointmentAccessorManager;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> _appointmentRepository;
    protected AppointmentReadAccessGuard _readAccessGuard;

    public AppointmentAccessorsAppService(
        IAppointmentAccessorRepository appointmentAccessorRepository,
        AppointmentAccessorManager appointmentAccessorManager,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository,
        IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository,
        AppointmentReadAccessGuard readAccessGuard)
    {
        _appointmentAccessorRepository = appointmentAccessorRepository;
        _appointmentAccessorManager = appointmentAccessorManager;
        _identityUserRepository = identityUserRepository;
        _appointmentRepository = appointmentRepository;
        _readAccessGuard = readAccessGuard;
    }

    public virtual async Task<PagedResultDto<AppointmentAccessorWithNavigationPropertiesDto>> GetListAsync(GetAppointmentAccessorsInput input)
    {
        var totalCount = await _appointmentAccessorRepository.GetCountAsync(input.FilterText, input.AccessTypeId, input.IdentityUserId, input.AppointmentId);
        var items = await _appointmentAccessorRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AccessTypeId, input.IdentityUserId, input.AppointmentId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentAccessorWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentAccessorWithNavigationProperties>, List<AppointmentAccessorWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<AppointmentAccessorWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentAccessorWithNavigationProperties, AppointmentAccessorWithNavigationPropertiesDto>((await _appointmentAccessorRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    public virtual async Task<AppointmentAccessorDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentAccessor, AppointmentAccessorDto>(await _appointmentAccessorRepository.GetAsync(id));
    }

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

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        var query = (await _appointmentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.RequestConfirmationNumber != null && x.RequestConfirmationNumber.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.Appointments.Appointment>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.Appointments.Appointment>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Deny-by-default: only internal staff or the creator-attorney (AA/DA)
        // may remove an accessor; the Edit-accessor pathway is dropped.
        var existing = await _appointmentAccessorRepository.GetAsync(id);
        await _readAccessGuard.EnsureCanManageAccessorsAsync(existing.AppointmentId);
        await _appointmentAccessorRepository.DeleteAsync(id);
    }

    [Authorize]
    public virtual async Task<AppointmentAccessorDto> CreateAsync(AppointmentAccessorCreateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        if (string.IsNullOrWhiteSpace(input.Email))
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Email"]]);
        }
        if (string.IsNullOrWhiteSpace(input.Role))
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Role"]]);
        }

        // Deny-by-default: only internal staff or the creator-attorney (AA/DA)
        // may add an accessor (blocks non-parties, Patient/CE creators, and the
        // Edit-accessor self-escalation pathway).
        await _readAccessGuard.EnsureCanManageAccessorsAsync(input.AppointmentId);

        // Email-based create-or-link: resolves the email to a user or
        // auto-provisions + invites one, applies the role-conflict check, and
        // fires the accessor-invite email.
        var appointmentAccessor = await _appointmentAccessorManager.CreateOrLinkAsync(
            appointmentId: input.AppointmentId,
            email: input.Email,
            requestedRoleName: input.Role,
            accessTypeId: input.AccessTypeId,
            tenantId: CurrentTenant.Id,
            firstName: input.FirstName,
            lastName: input.LastName);
        return ObjectMapper.Map<AppointmentAccessor, AppointmentAccessorDto>(appointmentAccessor);
    }

    [Authorize]
    public virtual async Task<AppointmentAccessorDto> UpdateAsync(Guid id, AppointmentAccessorUpdateDto input)
    {
        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        // Deny-by-default: gate by the accessor's ACTUAL appointment (not a
        // caller-supplied id); only internal staff or the creator-attorney
        // (AA/DA) may edit an accessor.
        var existing = await _appointmentAccessorRepository.GetAsync(id);
        await _readAccessGuard.EnsureCanManageAccessorsAsync(existing.AppointmentId);

        var appointmentAccessor = await _appointmentAccessorManager.UpdateAsync(id, input.IdentityUserId, input.AppointmentId, input.AccessTypeId);
        return ObjectMapper.Map<AppointmentAccessor, AppointmentAccessorDto>(appointmentAccessor);
    }
}