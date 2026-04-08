
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
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
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentEmployerDetailsAppService : CaseEvaluationAppService, IAppointmentEmployerDetailsAppService
{
    protected IAppointmentEmployerDetailRepository _appointmentEmployerDetailRepository;
    protected AppointmentEmployerDetailManager _appointmentEmployerDetailManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> _appointmentRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;

    public AppointmentEmployerDetailsAppService(IAppointmentEmployerDetailRepository appointmentEmployerDetailRepository, AppointmentEmployerDetailManager appointmentEmployerDetailManager, IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository)
    {
        _appointmentEmployerDetailRepository = appointmentEmployerDetailRepository;
        _appointmentEmployerDetailManager = appointmentEmployerDetailManager;
        _appointmentRepository = appointmentRepository;
        _stateRepository = stateRepository;
    }
    [Authorize]
    public virtual async Task<PagedResultDto<AppointmentEmployerDetailWithNavigationPropertiesDto>> GetListAsync(GetAppointmentEmployerDetailsInput input)
    {
        var totalCount = await _appointmentEmployerDetailRepository.GetCountAsync(input.FilterText, input.EmployerName, input.PhoneNumber, input.Street, input.City, input.AppointmentId, input.StateId);
        var items = await _appointmentEmployerDetailRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.EmployerName, input.PhoneNumber, input.Street, input.City, input.AppointmentId, input.StateId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentEmployerDetailWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentEmployerDetailWithNavigationProperties>, List<AppointmentEmployerDetailWithNavigationPropertiesDto>>(items)
        };
    }
    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]
    public virtual async Task<AppointmentEmployerDetailWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentEmployerDetailWithNavigationProperties, AppointmentEmployerDetailWithNavigationPropertiesDto>(await _appointmentEmployerDetailRepository.GetWithNavigationPropertiesAsync(id));
    }
    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]
    public virtual async Task<AppointmentEmployerDetailDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentEmployerDetail, AppointmentEmployerDetailDto>(await _appointmentEmployerDetailRepository.GetAsync(id));
    }
    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]
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
    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.States.State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.States.State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _appointmentEmployerDetailRepository.DeleteAsync(id);
    }

    [Authorize]
    public virtual async Task<AppointmentEmployerDetailDto> CreateAsync(AppointmentEmployerDetailCreateDto input)
    {
        if (input.AppointmentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        var appointmentEmployerDetail = await _appointmentEmployerDetailManager.CreateAsync(
            input.AppointmentId,
            input.StateId,
            input.EmployerName,
            input.Occupation,
            input.PhoneNumber,
            input.Street,
            input.City,
            input.ZipCode);
        return ObjectMapper.Map<AppointmentEmployerDetail, AppointmentEmployerDetailDto>(appointmentEmployerDetail);
    }

    [Authorize]
    public virtual async Task<AppointmentEmployerDetailDto> UpdateAsync(Guid id, AppointmentEmployerDetailUpdateDto input)
    {
        if (input.AppointmentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        var appointmentEmployerDetail = await _appointmentEmployerDetailManager.UpdateAsync(
            id,
            input.AppointmentId,
            input.StateId,
            input.EmployerName,
            input.Occupation,
            input.PhoneNumber,
            input.Street,
            input.City,
            input.ZipCode,
            input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentEmployerDetail, AppointmentEmployerDetailDto>(appointmentEmployerDetail);
    }
}