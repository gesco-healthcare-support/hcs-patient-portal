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
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

[RemoteService(IsEnabled = false)]
// Class-level demoted from CustomFields.Default to plain [Authorize] so the
// booking form's per-AppointmentType field-config read (used to drive
// hidden/readonly/default-value visibility on form rows) is callable by any
// authenticated booker. Admin-only list/get/edit endpoints keep their
// CustomFields.Default per-method gate. (Step 1.4 / W-A-3, 2026-04-30.)
[Authorize]
public class AppointmentTypeFieldConfigsAppService :
    CaseEvaluationAppService,
    IAppointmentTypeFieldConfigsAppService
{
    private readonly IRepository<AppointmentTypeFieldConfig, Guid> _repository;
    private readonly AppointmentTypeFieldConfigManager _manager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public AppointmentTypeFieldConfigsAppService(
        IRepository<AppointmentTypeFieldConfig, Guid> repository,
        AppointmentTypeFieldConfigManager manager,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _repository = repository;
        _manager = manager;
        _unitOfWorkManager = unitOfWorkManager;
    }

    // Plain [Authorize]: the booking form reads field configs to determine
    // hidden/readonly/default-value behavior; any authenticated booker needs
    // this. (Step 1.4 / W-A-3, 2026-04-30.)
    [Authorize]
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
            input.DefaultValue,
            input.Required);
        return ObjectMapper.Map<AppointmentTypeFieldConfig, AppointmentTypeFieldConfigDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Edit)]
    public virtual async Task<AppointmentTypeFieldConfigDto> UpdateAsync(Guid id, AppointmentTypeFieldConfigUpdateDto input)
    {
        var entity = await _manager.UpdateAsync(
            id, input.Hidden, input.ReadOnly, input.DefaultValue, input.ConcurrencyStamp, input.Required);
        return ObjectMapper.Map<AppointmentTypeFieldConfig, AppointmentTypeFieldConfigDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    // Prompt 15 (2026-06-15): replace-set batch save for the admin Field
    // Configuration panel. Gated by CustomFields.Edit (the save action); the
    // pure diff lives in FieldConfigReconciler, applied here via the manager.
    [Authorize(CaseEvaluationPermissions.CustomFields.Edit)]
    public virtual async Task<List<AppointmentTypeFieldConfigDto>> SaveForAppointmentTypeAsync(
        Guid appointmentTypeId,
        List<AppointmentTypeFieldConfigBatchItemDto> items)
    {
        if (appointmentTypeId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentType"]]);
        }
        items ??= new List<AppointmentTypeFieldConfigBatchItemDto>();

        var query = await _repository.GetQueryableAsync();
        var existingEntities = query.Where(x => x.AppointmentTypeId == appointmentTypeId).ToList();

        var existing = existingEntities
            .Select(e => new FieldConfigReconciler.Existing(
                e.Id, e.FieldName, e.Hidden, e.ReadOnly, e.Required, e.DefaultValue))
            .ToList();
        var desired = items
            .Where(i => !string.IsNullOrWhiteSpace(i.FieldName))
            .Select(i => new FieldConfigReconciler.Desired(
                i.FieldName, i.Hidden, i.ReadOnly, i.Required, i.DefaultValue))
            .ToList();

        var plan = FieldConfigReconciler.Reconcile(existing, desired);

        foreach (var id in plan.ToDelete)
        {
            await _repository.DeleteAsync(id);
        }
        foreach (var (id, v) in plan.ToUpdate)
        {
            await _manager.UpdateAsync(id, v.Hidden, v.ReadOnly, v.DefaultValue, null, v.Required);
        }
        foreach (var d in plan.ToCreate)
        {
            await _manager.CreateAsync(
                CurrentTenant.Id, appointmentTypeId, d.FieldName, d.Hidden, d.ReadOnly, d.DefaultValue, d.Required);
        }

        // Flush so the read-back reflects the create/update/delete in this UoW.
        if (_unitOfWorkManager.Current != null)
        {
            await _unitOfWorkManager.Current.SaveChangesAsync();
        }

        return await GetByAppointmentTypeIdAsync(appointmentTypeId);
    }
}
