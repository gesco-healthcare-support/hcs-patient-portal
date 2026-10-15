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
using HealthcareSupport.CaseEvaluation.AppointmentStatuses;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentStatuses.Default)]
public class AppointmentStatusesAppService : CaseEvaluationAppService, IAppointmentStatusesAppService
{
    protected IAppointmentStatusRepository _appointmentStatusRepository;
    protected AppointmentStatusManager _appointmentStatusManager;

    public AppointmentStatusesAppService(IAppointmentStatusRepository appointmentStatusRepository, AppointmentStatusManager appointmentStatusManager)
    {
        _appointmentStatusRepository = appointmentStatusRepository;
        _appointmentStatusManager = appointmentStatusManager;
    }

    public virtual async Task<PagedResultDto<AppointmentStatusDto>> GetListAsync(GetAppointmentStatusesInput input)
    {
        var totalCount = await _appointmentStatusRepository.GetCountAsync(input.FilterText);
        var items = await _appointmentStatusRepository.GetListAsync(input.FilterText, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentStatusDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentStatus>, List<AppointmentStatusDto>>(items)
        };
    }

    public virtual async Task<AppointmentStatusDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentStatus, AppointmentStatusDto>(await _appointmentStatusRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentStatuses.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Route through the manager so the system-row guard applies. There is
        // no in-use guard (the status lookup is not FK-referenced; appointments
        // use the AppointmentStatusType enum), so UsageCount stays null.
        await _appointmentStatusManager.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentStatuses.Create)]
    public virtual async Task<AppointmentStatusDto> CreateAsync(AppointmentStatusCreateDto input)
    {
        var appointmentStatus = await _appointmentStatusManager.CreateAsync(input.Name);
        return ObjectMapper.Map<AppointmentStatus, AppointmentStatusDto>(appointmentStatus);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentStatuses.Edit)]
    public virtual async Task<AppointmentStatusDto> UpdateAsync(Guid id, AppointmentStatusUpdateDto input)
    {
        var appointmentStatus = await _appointmentStatusManager.UpdateAsync(id, input.Name);
        return ObjectMapper.Map<AppointmentStatus, AppointmentStatusDto>(appointmentStatus);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentStatuses.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> appointmentstatusIds)
    {
        // Route through the manager (not raw DeleteManyAsync) so the system-row
        // guard applies to the bulk path too.
        foreach (var id in appointmentstatusIds)
        {
            await _appointmentStatusManager.DeleteAsync(id);
        }
    }

    [Authorize(CaseEvaluationPermissions.AppointmentStatuses.Delete)]
    public virtual async Task DeleteAllAsync(GetAppointmentStatusesInput input)
    {
        await _appointmentStatusRepository.DeleteAllAsync(input.FilterText);
    }
}