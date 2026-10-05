using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
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
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Default)]
public class AppointmentApplicantAttorneysAppService : CaseEvaluationAppService, IAppointmentApplicantAttorneysAppService
{
    protected IAppointmentApplicantAttorneyRepository _appointmentApplicantAttorneyRepository;
    protected AppointmentApplicantAttorneyManager _appointmentApplicantAttorneyManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> _appointmentRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney, Guid> _applicantAttorneyRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected AppointmentChildOwnershipGuard _childOwnershipGuard;

    public AppointmentApplicantAttorneysAppService(IAppointmentApplicantAttorneyRepository appointmentApplicantAttorneyRepository, AppointmentApplicantAttorneyManager appointmentApplicantAttorneyManager, IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository, IRepository<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney, Guid> applicantAttorneyRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, AppointmentChildOwnershipGuard childOwnershipGuard)
    {
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
        _appointmentApplicantAttorneyManager = appointmentApplicantAttorneyManager;
        _appointmentRepository = appointmentRepository;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _identityUserRepository = identityUserRepository;
        _childOwnershipGuard = childOwnershipGuard;
    }

    public virtual async Task<PagedResultDto<AppointmentApplicantAttorneyWithNavigationPropertiesDto>> GetListAsync(GetAppointmentApplicantAttorneysInput input)
    {
        var readableAppointmentIds = await _childOwnershipGuard.GetReadableAppointmentIdsAsync();
        var totalCount = await _appointmentApplicantAttorneyRepository.GetCountAsync(input.FilterText, input.AppointmentId, input.ApplicantAttorneyId, input.IdentityUserId, restrictToAppointmentIds: readableAppointmentIds);
        var items = await _appointmentApplicantAttorneyRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AppointmentId, input.ApplicantAttorneyId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount, restrictToAppointmentIds: readableAppointmentIds);
        return new PagedResultDto<AppointmentApplicantAttorneyWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentApplicantAttorneyWithNavigationProperties>, List<AppointmentApplicantAttorneyWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<AppointmentApplicantAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var item = await _appointmentApplicantAttorneyRepository.GetWithNavigationPropertiesAsync(id) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AppointmentApplicantAttorney), id);
        await _childOwnershipGuard.EnsureIsPartyAsync(item.AppointmentApplicantAttorney.AppointmentId);
        return ObjectMapper.Map<AppointmentApplicantAttorneyWithNavigationProperties, AppointmentApplicantAttorneyWithNavigationPropertiesDto>(item);
    }

    public virtual async Task<AppointmentApplicantAttorneyDto> GetAsync(Guid id)
    {
        var entity = await _appointmentApplicantAttorneyRepository.GetAsync(id);
        // Reading a child row is reading its parent appointment: the .Default permission ties the caller to no appointment.
        await _childOwnershipGuard.EnsureIsPartyAsync(entity.AppointmentId);
        return ObjectMapper.Map<AppointmentApplicantAttorney, AppointmentApplicantAttorneyDto>(entity);
    }

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

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetApplicantAttorneyLookupAsync(LookupRequestDto input)
    {
        var query = (await _applicantAttorneyRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.FirmName != null && x.FirmName.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney>, List<LookupDto<Guid>>>(lookupData)
        };
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

    [Authorize(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _appointmentApplicantAttorneyRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Create)]
    public virtual async Task<AppointmentApplicantAttorneyDto> CreateAsync(AppointmentApplicantAttorneyCreateDto input)
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

        if (input.ApplicantAttorneyId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ApplicantAttorney"]]);
        }

        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var appointmentApplicantAttorney = await _appointmentApplicantAttorneyManager.CreateAsync(input.AppointmentId, input.ApplicantAttorneyId, input.IdentityUserId);
        return ObjectMapper.Map<AppointmentApplicantAttorney, AppointmentApplicantAttorneyDto>(appointmentApplicantAttorney);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Edit)]
    public virtual async Task<AppointmentApplicantAttorneyDto> UpdateAsync(Guid id, AppointmentApplicantAttorneyUpdateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        if (input.ApplicantAttorneyId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ApplicantAttorney"]]);
        }

        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }
        // The caller must be a party to the row's OWN parent, and may not move the row to a
        // different one. The parent is read from the stored row, never from the request: checking
        // the supplied id would let a caller nominate an appointment they are a party to and still
        // write to somebody else's row. Every external role holds this service's Edit permission.
        var existingChild = await _appointmentApplicantAttorneyRepository.GetAsync(id);
        await _childOwnershipGuard.EnsureCanWriteChildAsync(existingChild.AppointmentId, input.AppointmentId);


        var appointmentApplicantAttorney = await _appointmentApplicantAttorneyManager.UpdateAsync(id, input.AppointmentId, input.ApplicantAttorneyId, input.IdentityUserId, input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentApplicantAttorney, AppointmentApplicantAttorneyDto>(appointmentApplicantAttorney);
    }
}