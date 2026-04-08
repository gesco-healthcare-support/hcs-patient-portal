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

    public AppointmentAccessorsAppService(
        IAppointmentAccessorRepository appointmentAccessorRepository,
        AppointmentAccessorManager appointmentAccessorManager,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository,
        IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository)
    {
        _appointmentAccessorRepository = appointmentAccessorRepository;
        _appointmentAccessorManager = appointmentAccessorManager;
        _identityUserRepository = identityUserRepository;
        _appointmentRepository = appointmentRepository;
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
        return ObjectMapper.Map<AppointmentAccessorWithNavigationProperties, AppointmentAccessorWithNavigationPropertiesDto>(await _appointmentAccessorRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<AppointmentAccessorDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentAccessor, AppointmentAccessorDto>(await _appointmentAccessorRepository.GetAsync(id));
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

    [Authorize]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _appointmentAccessorRepository.DeleteAsync(id);
    }

    [Authorize]
    public virtual async Task<AppointmentAccessorDto> CreateAsync(AppointmentAccessorCreateDto input)
    {
        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        if (input.AppointmentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        var appointmentAccessor = await _appointmentAccessorManager.CreateAsync(input.IdentityUserId, input.AppointmentId, input.AccessTypeId);
        return ObjectMapper.Map<AppointmentAccessor, AppointmentAccessorDto>(appointmentAccessor);
    }

    [Authorize]
    public virtual async Task<AppointmentAccessorDto> UpdateAsync(Guid id, AppointmentAccessorUpdateDto input)
    {
        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        if (input.AppointmentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        var appointmentAccessor = await _appointmentAccessorManager.UpdateAsync(id, input.IdentityUserId, input.AppointmentId, input.AccessTypeId);
        return ObjectMapper.Map<AppointmentAccessor, AppointmentAccessorDto>(appointmentAccessor);
    }
}