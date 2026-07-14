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
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.Patients;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentLanguages.Default)]
public class AppointmentLanguagesAppService : CaseEvaluationAppService, IAppointmentLanguagesAppService
{
    protected IAppointmentLanguageRepository _appointmentLanguageRepository;
    protected AppointmentLanguageManager _appointmentLanguageManager;
    protected IRepository<Patient, Guid> _patientRepository;

    public AppointmentLanguagesAppService(IAppointmentLanguageRepository appointmentLanguageRepository, AppointmentLanguageManager appointmentLanguageManager, IRepository<Patient, Guid> patientRepository)
    {
        _appointmentLanguageRepository = appointmentLanguageRepository;
        _appointmentLanguageManager = appointmentLanguageManager;
        _patientRepository = patientRepository;
    }

    public virtual async Task<PagedResultDto<AppointmentLanguageDto>> GetListAsync(GetAppointmentLanguagesInput input)
    {
        var totalCount = await _appointmentLanguageRepository.GetCountAsync(input.FilterText);
        var items = await _appointmentLanguageRepository.GetListAsync(input.FilterText, input.Sorting, input.MaxResultCount, input.SkipCount);
        var dtoItems = ObjectMapper.Map<List<AppointmentLanguage>, List<AppointmentLanguageDto>>(items);
        // Prompt 15 / item 32: per-row UsageCount = referencing Patient rows.
        foreach (var dto in dtoItems)
        {
            dto.UsageCount = (int)await _patientRepository.CountAsync(p => p.AppointmentLanguageId == dto.Id);
        }
        return new PagedResultDto<AppointmentLanguageDto>
        {
            TotalCount = totalCount,
            Items = dtoItems
        };
    }

    public virtual async Task<AppointmentLanguageDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentLanguage, AppointmentLanguageDto>(await _appointmentLanguageRepository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentLanguages.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Route through the manager so the system-row + in-use guards apply.
        await _appointmentLanguageManager.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentLanguages.Create)]
    public virtual async Task<AppointmentLanguageDto> CreateAsync(AppointmentLanguageCreateDto input)
    {
        var appointmentLanguage = await _appointmentLanguageManager.CreateAsync(input.Name);
        return ObjectMapper.Map<AppointmentLanguage, AppointmentLanguageDto>(appointmentLanguage);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentLanguages.Edit)]
    public virtual async Task<AppointmentLanguageDto> UpdateAsync(Guid id, AppointmentLanguageUpdateDto input)
    {
        var appointmentLanguage = await _appointmentLanguageManager.UpdateAsync(id, input.Name);
        return ObjectMapper.Map<AppointmentLanguage, AppointmentLanguageDto>(appointmentLanguage);
    }
}