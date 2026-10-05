using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
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
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
public class AppointmentDefenseAttorneysAppService : CaseEvaluationAppService, IAppointmentDefenseAttorneysAppService
{
    protected IAppointmentDefenseAttorneyRepository _appointmentDefenseAttorneyRepository;
    protected AppointmentDefenseAttorneyManager _appointmentDefenseAttorneyManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> _appointmentRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney, Guid> _defenseAttorneyRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected AppointmentChildOwnershipGuard _childOwnershipGuard;

    public AppointmentDefenseAttorneysAppService(IAppointmentDefenseAttorneyRepository appointmentDefenseAttorneyRepository, AppointmentDefenseAttorneyManager appointmentDefenseAttorneyManager, IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository, IRepository<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney, Guid> defenseAttorneyRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, AppointmentChildOwnershipGuard childOwnershipGuard)
    {
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
        _appointmentDefenseAttorneyManager = appointmentDefenseAttorneyManager;
        _appointmentRepository = appointmentRepository;
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _identityUserRepository = identityUserRepository;
        _childOwnershipGuard = childOwnershipGuard;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
    public virtual async Task<PagedResultDto<AppointmentDefenseAttorneyWithNavigationPropertiesDto>> GetListAsync(GetAppointmentDefenseAttorneysInput input)
    {
        var readableAppointmentIds = await _childOwnershipGuard.GetReadableAppointmentIdsAsync();
        var totalCount = await _appointmentDefenseAttorneyRepository.GetCountAsync(input.FilterText, input.AppointmentId, input.DefenseAttorneyId, input.IdentityUserId, restrictToAppointmentIds: readableAppointmentIds);
        var items = await _appointmentDefenseAttorneyRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AppointmentId, input.DefenseAttorneyId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount, restrictToAppointmentIds: readableAppointmentIds);
        return new PagedResultDto<AppointmentDefenseAttorneyWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentDefenseAttorneyWithNavigationProperties>, List<AppointmentDefenseAttorneyWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
    public virtual async Task<AppointmentDefenseAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var item = await _appointmentDefenseAttorneyRepository.GetWithNavigationPropertiesAsync(id) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AppointmentDefenseAttorney), id);
        await _childOwnershipGuard.EnsureIsPartyAsync(item.AppointmentDefenseAttorney.AppointmentId);
        return ObjectMapper.Map<AppointmentDefenseAttorneyWithNavigationProperties, AppointmentDefenseAttorneyWithNavigationPropertiesDto>(item);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
    public virtual async Task<AppointmentDefenseAttorneyDto> GetAsync(Guid id)
    {
        var entity = await _appointmentDefenseAttorneyRepository.GetAsync(id);
        // Reading a child row is reading its parent appointment: the .Default permission ties the caller to no appointment.
        await _childOwnershipGuard.EnsureIsPartyAsync(entity.AppointmentId);
        return ObjectMapper.Map<AppointmentDefenseAttorney, AppointmentDefenseAttorneyDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        // Narrowed to appointments the caller may reach, in the query itself so the count and paging cannot leak the rest. Internal callers get null (no narrowing).
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

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDefenseAttorneyLookupAsync(LookupRequestDto input)
    {
        var query = (await _defenseAttorneyRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.FirmName != null && x.FirmName.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
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

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _appointmentDefenseAttorneyRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Create)]
    public virtual async Task<AppointmentDefenseAttorneyDto> CreateAsync(AppointmentDefenseAttorneyCreateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        // The caller must be a party to the appointment the new row hangs off. The external booking roles
        // hold this service's Create permission, so the permission alone ties the caller to no appointment.
        // Booking does not call this (it upserts attorneys through AppointmentsAppService), so the only
        // caller is this endpoint. Checked FIRST, so a non-party is refused the same way whatever they send.
        await _childOwnershipGuard.EnsureIsPartyAsync(input.AppointmentId);

        if (input.DefenseAttorneyId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["DefenseAttorney"]]);
        }

        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var appointmentDefenseAttorney = await _appointmentDefenseAttorneyManager.CreateAsync(input.AppointmentId, input.DefenseAttorneyId, input.IdentityUserId);
        return ObjectMapper.Map<AppointmentDefenseAttorney, AppointmentDefenseAttorneyDto>(appointmentDefenseAttorney);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Edit)]
    public virtual async Task<AppointmentDefenseAttorneyDto> UpdateAsync(Guid id, AppointmentDefenseAttorneyUpdateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        if (input.DefenseAttorneyId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["DefenseAttorney"]]);
        }

        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }
        // The caller must be a party to the row's OWN parent, and may not move the row to a
        // different one. The parent is read from the stored row, never from the request: checking
        // the supplied id would let a caller nominate an appointment they are a party to and still
        // write to somebody else's row. Every external role holds this service's Edit permission.
        var existingChild = await _appointmentDefenseAttorneyRepository.GetAsync(id);
        await _childOwnershipGuard.EnsureCanWriteChildAsync(existingChild.AppointmentId, input.AppointmentId);


        var appointmentDefenseAttorney = await _appointmentDefenseAttorneyManager.UpdateAsync(id, input.AppointmentId, input.DefenseAttorneyId, input.IdentityUserId, input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentDefenseAttorney, AppointmentDefenseAttorneyDto>(appointmentDefenseAttorney);
    }
}
