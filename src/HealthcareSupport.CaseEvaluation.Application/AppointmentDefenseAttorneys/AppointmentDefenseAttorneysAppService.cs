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

    public AppointmentDefenseAttorneysAppService(IAppointmentDefenseAttorneyRepository appointmentDefenseAttorneyRepository, AppointmentDefenseAttorneyManager appointmentDefenseAttorneyManager, IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository, IRepository<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney, Guid> defenseAttorneyRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
        _appointmentDefenseAttorneyManager = appointmentDefenseAttorneyManager;
        _appointmentRepository = appointmentRepository;
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _identityUserRepository = identityUserRepository;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
    public virtual async Task<PagedResultDto<AppointmentDefenseAttorneyWithNavigationPropertiesDto>> GetListAsync(GetAppointmentDefenseAttorneysInput input)
    {
        var totalCount = await _appointmentDefenseAttorneyRepository.GetCountAsync(input.FilterText, input.AppointmentId, input.DefenseAttorneyId, input.IdentityUserId);
        var items = await _appointmentDefenseAttorneyRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AppointmentId, input.DefenseAttorneyId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentDefenseAttorneyWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentDefenseAttorneyWithNavigationProperties>, List<AppointmentDefenseAttorneyWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
    public virtual async Task<AppointmentDefenseAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentDefenseAttorneyWithNavigationProperties, AppointmentDefenseAttorneyWithNavigationPropertiesDto>((await _appointmentDefenseAttorneyRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
    public virtual async Task<AppointmentDefenseAttorneyDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentDefenseAttorney, AppointmentDefenseAttorneyDto>(await _appointmentDefenseAttorneyRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default)]
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

        var appointmentDefenseAttorney = await _appointmentDefenseAttorneyManager.UpdateAsync(id, input.AppointmentId, input.DefenseAttorneyId, input.IdentityUserId, input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentDefenseAttorney, AppointmentDefenseAttorneyDto>(appointmentDefenseAttorney);
    }
}
