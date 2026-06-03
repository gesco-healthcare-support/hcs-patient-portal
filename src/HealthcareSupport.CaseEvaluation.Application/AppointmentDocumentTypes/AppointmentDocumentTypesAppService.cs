using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.Permissions;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Default)]
public class AppointmentDocumentTypesAppService : CaseEvaluationAppService, IAppointmentDocumentTypesAppService
{
    protected IAppointmentDocumentTypeRepository _appointmentDocumentTypeRepository;
    protected AppointmentDocumentTypeManager _appointmentDocumentTypeManager;

    public AppointmentDocumentTypesAppService(
        IAppointmentDocumentTypeRepository appointmentDocumentTypeRepository,
        AppointmentDocumentTypeManager appointmentDocumentTypeManager)
    {
        _appointmentDocumentTypeRepository = appointmentDocumentTypeRepository;
        _appointmentDocumentTypeManager = appointmentDocumentTypeManager;
    }

    public virtual async Task<PagedResultDto<AppointmentDocumentTypeDto>> GetListAsync(GetAppointmentDocumentTypesInput input)
    {
        var totalCount = await _appointmentDocumentTypeRepository.GetCountAsync(input.FilterText, input.AppointmentTypeId);
        var items = await _appointmentDocumentTypeRepository.GetListAsync(input.FilterText, input.AppointmentTypeId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentDocumentTypeDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentDocumentType>, List<AppointmentDocumentTypeDto>>(items)
        };
    }

    public virtual async Task<AppointmentDocumentTypeDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentDocumentType, AppointmentDocumentTypeDto>(await _appointmentDocumentTypeRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _appointmentDocumentTypeManager.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Create)]
    public virtual async Task<AppointmentDocumentTypeDto> CreateAsync(AppointmentDocumentTypeCreateDto input)
    {
        var appointmentDocumentType = await _appointmentDocumentTypeManager.CreateAsync(input.Name, input.AppointmentTypeId, input.IsActive);
        return ObjectMapper.Map<AppointmentDocumentType, AppointmentDocumentTypeDto>(appointmentDocumentType);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocumentTypes.Edit)]
    public virtual async Task<AppointmentDocumentTypeDto> UpdateAsync(Guid id, AppointmentDocumentTypeUpdateDto input)
    {
        var appointmentDocumentType = await _appointmentDocumentTypeManager.UpdateAsync(id, input.Name, input.AppointmentTypeId, input.IsActive);
        return ObjectMapper.Map<AppointmentDocumentType, AppointmentDocumentTypeDto>(appointmentDocumentType);
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
