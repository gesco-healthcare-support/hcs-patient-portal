using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.CustomFields.Default)]
public class AppointmentTypeFieldConfigsAppService :
    CaseEvaluationAppService,
    IAppointmentTypeFieldConfigsAppService
{
    private readonly IRepository<AppointmentTypeFieldConfig, Guid> _repository;
    private readonly AppointmentTypeFieldConfigManager _manager;

    public AppointmentTypeFieldConfigsAppService(
        IRepository<AppointmentTypeFieldConfig, Guid> repository,
        AppointmentTypeFieldConfigManager manager)
    {
        _repository = repository;
        _manager = manager;
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Default)]
    public virtual async Task<List<AppointmentTypeFieldConfigDto>> GetByAppointmentTypeIdAsync(Guid appointmentTypeId)
    {
        var query = await _repository.GetQueryableAsync();
        var rows = query.Where(x => x.AppointmentTypeId == appointmentTypeId).ToList();
        return ObjectMapper.Map<List<AppointmentTypeFieldConfig>, List<AppointmentTypeFieldConfigDto>>(rows);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Default)]
    public virtual async Task<List<AppointmentTypeFieldConfigDto>> GetListAsync(Guid? appointmentTypeId)
    {
        var query = await _repository.GetQueryableAsync();
        if (appointmentTypeId.HasValue)
        {
            query = query.Where(x => x.AppointmentTypeId == appointmentTypeId.Value);
        }
        var rows = query.OrderBy(x => x.AppointmentTypeId).ThenBy(x => x.FieldName).ToList();
        return ObjectMapper.Map<List<AppointmentTypeFieldConfig>, List<AppointmentTypeFieldConfigDto>>(rows);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Default)]
    public virtual async Task<AppointmentTypeFieldConfigDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<AppointmentTypeFieldConfig, AppointmentTypeFieldConfigDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Create)]
    public virtual async Task<AppointmentTypeFieldConfigDto> CreateAsync(AppointmentTypeFieldConfigCreateDto input)
    {
        if (input.AppointmentTypeId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentType"]]);
        }
        if (string.IsNullOrWhiteSpace(input.FieldName))
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["FieldName"]]);
        }

        // Composite-uniqueness guard: at most one row per (TenantId, AppointmentTypeId, FieldName).
        var query = await _repository.GetQueryableAsync();
        var dup = query.Any(x =>
            x.AppointmentTypeId == input.AppointmentTypeId
            && x.FieldName == input.FieldName);
        if (dup)
        {
            throw new UserFriendlyException(L["A configuration row already exists for this AppointmentType + FieldName combination."]);
        }

        var entity = await _manager.CreateAsync(
            CurrentTenant.Id,
            input.AppointmentTypeId,
            input.FieldName,
            input.Hidden,
            input.ReadOnly,
            input.DefaultValue);
        return ObjectMapper.Map<AppointmentTypeFieldConfig, AppointmentTypeFieldConfigDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Edit)]
    public virtual async Task<AppointmentTypeFieldConfigDto> UpdateAsync(Guid id, AppointmentTypeFieldConfigUpdateDto input)
    {
        var entity = await _manager.UpdateAsync(id, input.Hidden, input.ReadOnly, input.DefaultValue, input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentTypeFieldConfig, AppointmentTypeFieldConfigDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
