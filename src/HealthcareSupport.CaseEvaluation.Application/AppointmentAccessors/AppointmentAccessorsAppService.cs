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
    protected IdentityUserManager _identityUserManager;
    protected AppointmentChildOwnershipGuard _childOwnershipGuard;

    public AppointmentAccessorsAppService(
        IAppointmentAccessorRepository appointmentAccessorRepository,
        AppointmentAccessorManager appointmentAccessorManager,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository,
        IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository,
        AppointmentReadAccessGuard readAccessGuard,
        IdentityUserManager identityUserManager,
        AppointmentChildOwnershipGuard childOwnershipGuard)
    {
        _appointmentAccessorRepository = appointmentAccessorRepository;
        _appointmentAccessorManager = appointmentAccessorManager;
        _identityUserRepository = identityUserRepository;
        _appointmentRepository = appointmentRepository;
        _readAccessGuard = readAccessGuard;
        _identityUserManager = identityUserManager;
        _childOwnershipGuard = childOwnershipGuard;
    }

    public virtual async Task<PagedResultDto<AppointmentAccessorWithNavigationPropertiesDto>> GetListAsync(GetAppointmentAccessorsInput input)
    {
        // An accessor row says who may reach an appointment, so it is readable exactly when the
        // appointment is. Narrowed in the query so the count and paging cannot leak the rest;
        // internal callers get null (no narrowing).
        var readableAppointmentIds = await _childOwnershipGuard.GetReadableAppointmentIdsAsync();
        var totalCount = await _appointmentAccessorRepository.GetCountAsync(input.FilterText, input.AccessTypeId, input.IdentityUserId, input.AppointmentId, restrictToAppointmentIds: readableAppointmentIds);
        var items = await _appointmentAccessorRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AccessTypeId, input.IdentityUserId, input.AppointmentId, input.Sorting, input.MaxResultCount, input.SkipCount, restrictToAppointmentIds: readableAppointmentIds);
        var dtos = ObjectMapper.Map<List<AppointmentAccessorWithNavigationProperties>, List<AppointmentAccessorWithNavigationPropertiesDto>>(items);
        await PopulateUserRoleNamesAsync(dtos);
        return new PagedResultDto<AppointmentAccessorWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = dtos
        };
    }

    /// <summary>
    /// QA item 14: stamp each row's external role (Patient / Applicant Attorney /
    /// Defense Attorney / Claim Examiner) from the user's assigned roles so the
    /// view-time authorized-users list shows the Role. The list is small (a handful
    /// of accessors per appointment), so a per-user role lookup is cheap.
    /// </summary>
    protected virtual async Task PopulateUserRoleNamesAsync(List<AppointmentAccessorWithNavigationPropertiesDto> dtos)
    {
        foreach (var dto in dtos)
        {
            var userId = dto.IdentityUser?.Id ?? dto.AppointmentAccessor?.IdentityUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                continue;
            }

            var user = await _identityUserManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                continue;
            }

            var roles = await _identityUserManager.GetRolesAsync(user);
            dto.UserRoleName = roles.FirstOrDefault(r =>
                AppointmentAccessorRules.RecognizedExternalRoles.Any(er =>
                    string.Equals(er, r, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public virtual async Task<AppointmentAccessorWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var item = await _appointmentAccessorRepository.GetWithNavigationPropertiesAsync(id) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AppointmentAccessor), id);
        await _childOwnershipGuard.EnsureIsPartyAsync(item.AppointmentAccessor.AppointmentId);
        return ObjectMapper.Map<AppointmentAccessorWithNavigationProperties, AppointmentAccessorWithNavigationPropertiesDto>(item);
    }

    public virtual async Task<AppointmentAccessorDto> GetAsync(Guid id)
    {
        var entity = await _appointmentAccessorRepository.GetAsync(id);
        await _childOwnershipGuard.EnsureIsPartyAsync(entity.AppointmentId);
        return ObjectMapper.Map<AppointmentAccessor, AppointmentAccessorDto>(entity);
    }

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

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        var readableAppointmentIds = await _childOwnershipGuard.GetReadableAppointmentIdsAsync();
        var query = (await _appointmentRepository.GetQueryableAsync()).WhereIf(readableAppointmentIds != null, x => readableAppointmentIds!.Contains(x.Id)).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.RequestConfirmationNumber != null && x.RequestConfirmationNumber.Contains(input.Filter!));
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
        // Deny-by-default. Office decisions 3 + 4 (2026-11-05): staff may remove anyone; an external caller
        // may remove only an Authorized User whose account holds a law-office role of a side on which the
        // caller is a party of THIS case (named attorney, booker or Authorized User). The target's roles
        // are read here, as Update does, so a Patient / Claim Examiner accessor is not removable by them.
        var existing = await _appointmentAccessorRepository.GetAsync(id);
        await _readAccessGuard.EnsureCanManageAccessorsAsync(
            existing.AppointmentId, await TargetOfAccountAsync(existing.IdentityUserId));
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
        // 2026-11-13: a new Authorized User must have a real email (an accessor row is always new on create).
        PartyEmailGuard.EnsureNoPlaceholder(new[] { input.Email });

        // Deny-by-default: only internal staff or the creator-attorney (AA/DA)
        // may add an accessor (blocks non-parties, Patient/CE creators, and the
        // Edit-accessor self-escalation pathway).
        // 2026-11-04 (office decision 3): an external caller may add a law-office Authorized User only of
        // their own side. Every accessor write -- including the ones inside the atomic booking submit,
        // which call this method -- passes here, so one check covers them. A role the manager will refuse
        // as not recognised is left for it to refuse, with its own code. Judged BEFORE the management gate
        // so a Patient or Claim Examiner caller (who has no law-office side) is told the side is wrong.
        // 2026-11-05 (decisions 3 + 4): the side is judged against THIS case -- the caller must be the named
        // attorney of that side, or the booker / an Authorized User holding a role of it. A non-law-office
        // role is allowed only as a Claim Examiner helper added by a claim examiner party (decision 5).
        var requestedRole = AppointmentAccessorRules.ResolveRecognizedExternalRole(input.Role);
        await _readAccessGuard.EnsureCanManageAccessorsAsync(
            input.AppointmentId,
            AccessorTarget.Of(new[] { requestedRole }));

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
        // 2026-11-04 (office decision 3): pointing the row at another account is a role change for the
        // case, so the same-side rule applies to that account's roles. An edit that keeps the account
        // (the access type) is not re-judged.
        // The sides judged are those of the existing account and, when it changes, of the new one.
        var target = await TargetOfAccountAsync(existing.IdentityUserId);
        if (input.IdentityUserId != existing.IdentityUserId)
        {
            var next = await TargetOfAccountAsync(input.IdentityUserId);
            target = new AccessorTarget(
                target.Sides.Union(next.Sides).ToList(), target.IsClaimExaminer || next.IsClaimExaminer);
        }

        await _readAccessGuard.EnsureCanManageAccessorsAsync(existing.AppointmentId, target);

        var appointmentAccessor = await _appointmentAccessorManager.UpdateAsync(id, input.IdentityUserId, input.AppointmentId, input.AccessTypeId);
        return ObjectMapper.Map<AppointmentAccessor, AppointmentAccessorDto>(appointmentAccessor);
    }

    /// <summary>What the account is by its roles (nothing when the account is gone).</summary>
    private async Task<AccessorTarget> TargetOfAccountAsync(Guid identityUserId)
    {
        var user = await _identityUserManager.FindByIdAsync(identityUserId.ToString());
        return AccessorTarget.Of(user == null ? null : await _identityUserManager.GetRolesAsync(user));
    }
}
