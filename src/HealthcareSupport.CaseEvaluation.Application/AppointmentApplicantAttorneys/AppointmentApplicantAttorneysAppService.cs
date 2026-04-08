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

    public AppointmentApplicantAttorneysAppService(IAppointmentApplicantAttorneyRepository appointmentApplicantAttorneyRepository, AppointmentApplicantAttorneyManager appointmentApplicantAttorneyManager, IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository, IRepository<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney, Guid> applicantAttorneyRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
        _appointmentApplicantAttorneyManager = appointmentApplicantAttorneyManager;
        _appointmentRepository = appointmentRepository;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<AppointmentApplicantAttorneyWithNavigationPropertiesDto>> GetListAsync(GetAppointmentApplicantAttorneysInput input)
    {
        var totalCount = await _appointmentApplicantAttorneyRepository.GetCountAsync(input.FilterText, input.AppointmentId, input.ApplicantAttorneyId, input.IdentityUserId);
        var items = await _appointmentApplicantAttorneyRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AppointmentId, input.ApplicantAttorneyId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentApplicantAttorneyWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentApplicantAttorneyWithNavigationProperties>, List<AppointmentApplicantAttorneyWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<AppointmentApplicantAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentApplicantAttorneyWithNavigationProperties, AppointmentApplicantAttorneyWithNavigationPropertiesDto>(await _appointmentApplicantAttorneyRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<AppointmentApplicantAttorneyDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentApplicantAttorney, AppointmentApplicantAttorneyDto>(await _appointmentApplicantAttorneyRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        var query = (await _appointmentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.RequestConfirmationNumber != null && x.RequestConfirmationNumber.Contains(input.Filter));
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
        var query = (await _applicantAttorneyRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.FirmName != null && x.FirmName.Contains(input.Filter));
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
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter));
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
        if (input.AppointmentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        if (input.ApplicantAttorneyId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ApplicantAttorney"]]);
        }

        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var appointmentApplicantAttorney = await _appointmentApplicantAttorneyManager.CreateAsync(input.AppointmentId, input.ApplicantAttorneyId, input.IdentityUserId);
        return ObjectMapper.Map<AppointmentApplicantAttorney, AppointmentApplicantAttorneyDto>(appointmentApplicantAttorney);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Edit)]
    public virtual async Task<AppointmentApplicantAttorneyDto> UpdateAsync(Guid id, AppointmentApplicantAttorneyUpdateDto input)
    {
        if (input.AppointmentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        if (input.ApplicantAttorneyId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ApplicantAttorney"]]);
        }

        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var appointmentApplicantAttorney = await _appointmentApplicantAttorneyManager.UpdateAsync(id, input.AppointmentId, input.ApplicantAttorneyId, input.IdentityUserId, input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentApplicantAttorney, AppointmentApplicantAttorneyDto>(appointmentApplicantAttorney);
    }
}