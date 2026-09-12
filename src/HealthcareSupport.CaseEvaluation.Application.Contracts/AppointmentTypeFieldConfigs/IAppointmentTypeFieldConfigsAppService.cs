using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

public interface IAppointmentTypeFieldConfigsAppService : IApplicationService
{
    /// <summary>Reader endpoint used by the booker form to apply config on AppointmentType selection.</summary>
    Task<List<AppointmentTypeFieldConfigDto>> GetByAppointmentTypeIdAsync(Guid appointmentTypeId);

    /// <summary>Admin endpoint: list all configs (optionally filtered by AppointmentType).</summary>
    Task<List<AppointmentTypeFieldConfigDto>> GetListAsync(Guid? appointmentTypeId);

    Task<AppointmentTypeFieldConfigDto> GetAsync(Guid id);

    Task<AppointmentTypeFieldConfigDto> CreateAsync(AppointmentTypeFieldConfigCreateDto input);

    Task<AppointmentTypeFieldConfigDto> UpdateAsync(Guid id, AppointmentTypeFieldConfigUpdateDto input);

    Task DeleteAsync(Guid id);

    /// <summary>
    /// Prompt 15: replace-set batch save of an AppointmentType's field config.
    /// Rows absent from <paramref name="items"/> are deleted, new field names are
    /// created, and changed rows are updated -- so the admin saves the whole
    /// Field Configuration panel in one call. Returns the resulting set.
    /// </summary>
    Task<List<AppointmentTypeFieldConfigDto>> SaveForAppointmentTypeAsync(
        Guid appointmentTypeId,
        List<AppointmentTypeFieldConfigBatchItemDto> items);
}
