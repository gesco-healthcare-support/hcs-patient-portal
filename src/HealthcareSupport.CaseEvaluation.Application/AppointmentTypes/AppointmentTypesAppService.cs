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
using HealthcareSupport.CaseEvaluation.AppointmentTypes;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentTypesAppService : CaseEvaluationAppService, IAppointmentTypesAppService
{
    protected IAppointmentTypeRepository _appointmentTypeRepository;
    protected AppointmentTypeManager _appointmentTypeManager;

    public AppointmentTypesAppService(IAppointmentTypeRepository appointmentTypeRepository, AppointmentTypeManager appointmentTypeManager)
    {
        _appointmentTypeRepository = appointmentTypeRepository;
        _appointmentTypeManager = appointmentTypeManager;
    }

    public virtual async Task<PagedResultDto<AppointmentTypeDto>> GetListAsync(GetAppointmentTypesInput input)
    {
        var totalCount = await _appointmentTypeRepository.GetCountAsync(input.FilterText, input.Name);
        var items = await _appointmentTypeRepository.GetListAsync(input.FilterText, input.Name, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentTypeDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentType>, List<AppointmentTypeDto>>(items)
        };
    }

    public virtual async Task<AppointmentTypeDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentType, AppointmentTypeDto>(await _appointmentTypeRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentTypes.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _appointmentTypeRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentTypes.Create)]
    public virtual async Task<AppointmentTypeDto> CreateAsync(AppointmentTypeCreateDto input)
    {
        var appointmentType = await _appointmentTypeManager.CreateAsync(input.Name, input.Description);
        return ObjectMapper.Map<AppointmentType, AppointmentTypeDto>(appointmentType);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentTypes.Edit)]
    public virtual async Task<AppointmentTypeDto> UpdateAsync(Guid id, AppointmentTypeUpdateDto input)
    {
        var appointmentType = await _appointmentTypeManager.UpdateAsync(id, input.Name, input.Description);
        return ObjectMapper.Map<AppointmentType, AppointmentTypeDto>(appointmentType);
    }
}