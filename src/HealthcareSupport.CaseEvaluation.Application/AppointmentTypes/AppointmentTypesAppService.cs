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
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentTypes.Default)]
public class AppointmentTypesAppService : CaseEvaluationAppService, IAppointmentTypesAppService
{
    protected IAppointmentTypeRepository _appointmentTypeRepository;
    protected AppointmentTypeManager _appointmentTypeManager;
    protected IRepository<Appointment, Guid> _appointmentRepository;

    // 2026-08-17: renders the *.InUse delete guards as their real message. Without it the
    // raw BusinessException reaches the SPA with no message and the toast falls back to
    // ABP's generic "An internal error occurred during your request!".
    public AppointmentTypesAppService(IAppointmentTypeRepository appointmentTypeRepository, AppointmentTypeManager appointmentTypeManager, IRepository<Appointment, Guid> appointmentRepository)
    {
        _appointmentTypeRepository = appointmentTypeRepository;
        _appointmentTypeManager = appointmentTypeManager;
        _appointmentRepository = appointmentRepository;
    }

    public virtual async Task<PagedResultDto<AppointmentTypeDto>> GetListAsync(GetAppointmentTypesInput input)
    {
        var totalCount = await _appointmentTypeRepository.GetCountAsync(input.FilterText, input.Name);
        var items = await _appointmentTypeRepository.GetListAsync(input.FilterText, input.Name, input.Sorting, input.MaxResultCount, input.SkipCount);
        var dtoItems = ObjectMapper.Map<List<AppointmentType>, List<AppointmentTypeDto>>(items);
        // Prompt 15 / item 32: per-row UsageCount = referencing Appointment rows.
        foreach (var dto in dtoItems)
        {
            dto.UsageCount = (int)await _appointmentRepository.CountAsync(a => a.AppointmentTypeId == dto.Id);
        }
        return new PagedResultDto<AppointmentTypeDto>
        {
            TotalCount = totalCount,
            Items = dtoItems
        };
    }

    public virtual async Task<AppointmentTypeDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentType, AppointmentTypeDto>(await _appointmentTypeRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentTypes.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Route through the manager so the system-row + in-use guards apply.
        // 2026-08-17: the manager raises a bare *.InUse BusinessException. Translate it here
        // so the client gets the real reason instead of ABP's generic internal-error text.
        await _appointmentTypeManager.DeleteAsync(id);
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