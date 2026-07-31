using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Default)]
public class AppointmentDocumentTypesAppService : CaseEvaluationAppService, IAppointmentDocumentTypesAppService
{
    protected IAppointmentDocumentTypeRepository _appointmentDocumentTypeRepository;
    protected AppointmentDocumentTypeManager _appointmentDocumentTypeManager;
    protected IRepository<AppointmentDocument, Guid> _appointmentDocumentRepository;

    public AppointmentDocumentTypesAppService(
        IAppointmentDocumentTypeRepository appointmentDocumentTypeRepository,
        AppointmentDocumentTypeManager appointmentDocumentTypeManager,
        IRepository<AppointmentDocument, Guid> appointmentDocumentRepository)
    {
        _appointmentDocumentTypeRepository = appointmentDocumentTypeRepository;
        _appointmentDocumentTypeManager = appointmentDocumentTypeManager;
        _appointmentDocumentRepository = appointmentDocumentRepository;
    }

    public virtual async Task<PagedResultDto<AppointmentDocumentTypeDto>> GetListAsync(GetAppointmentDocumentTypesInput input)
    {
        var totalCount = await _appointmentDocumentTypeRepository.GetCountAsync(input.FilterText, input.AppointmentTypeId);
        var items = await _appointmentDocumentTypeRepository.GetListAsync(input.FilterText, input.AppointmentTypeId, input.Sorting, input.MaxResultCount, input.SkipCount);
        var dtoItems = new List<AppointmentDocumentTypeDto>(items.Count);
        foreach (var entity in items)
        {
            var dto = MapWithAppointmentTypes(entity);
            // Prompt 15 / item 32: per-row UsageCount = referencing AppointmentDocument rows.
            dto.UsageCount = (int)await _appointmentDocumentRepository.CountAsync(d => d.AppointmentDocumentTypeId == entity.Id);
            dtoItems.Add(dto);
        }
        return new PagedResultDto<AppointmentDocumentTypeDto>
        {
            TotalCount = totalCount,
            Items = dtoItems
        };
    }

    public virtual async Task<AppointmentDocumentTypeDto> GetAsync(Guid id)
    {
        return MapWithAppointmentTypes(await _appointmentDocumentTypeRepository.GetWithAppointmentTypesAsync(id));
    }

    /// <summary>Maps an entity plus its M2M set (the join collection is not auto-
    /// mapped, like UsageCount; it is projected here from the loaded join rows).</summary>
    private AppointmentDocumentTypeDto MapWithAppointmentTypes(AppointmentDocumentType entity)
    {
        var dto = ObjectMapper.Map<AppointmentDocumentType, AppointmentDocumentTypeDto>(entity);
        dto.AppointmentTypeIds = entity.AppointmentTypes.Select(j => j.AppointmentTypeId).ToList();
        return dto;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _appointmentDocumentTypeManager.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Create)]
    public virtual async Task<AppointmentDocumentTypeDto> CreateAsync(AppointmentDocumentTypeCreateDto input)
    {
        var appointmentDocumentType = await _appointmentDocumentTypeManager.CreateAsync(
            input.Name, input.AppointmentTypeIds, input.AppliesToAll, input.IsActive);
        return MapWithAppointmentTypes(appointmentDocumentType);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Edit)]
    public virtual async Task<AppointmentDocumentTypeDto> UpdateAsync(Guid id, AppointmentDocumentTypeUpdateDto input)
    {
        var appointmentDocumentType = await _appointmentDocumentTypeManager.UpdateAsync(
            id, input.Name, input.AppointmentTypeIds, input.AppliesToAll, input.IsActive);
        return MapWithAppointmentTypes(appointmentDocumentType);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> appointmentDocumentTypeIds)
    {
        // Route through the manager (not a raw DeleteManyAsync) so the
        // system-row guard applies to the bulk path too -- the UI hides
        // reserved rows, but a hand-crafted request must not delete one.
        foreach (var id in appointmentDocumentTypeIds)
        {
            await _appointmentDocumentTypeManager.DeleteAsync(id);
        }
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Delete)]
    public virtual async Task DeleteAllAsync(GetAppointmentDocumentTypesInput input)
    {
        await _appointmentDocumentTypeRepository.DeleteAllAsync(input.FilterText, input.AppointmentTypeId);
    }
}
