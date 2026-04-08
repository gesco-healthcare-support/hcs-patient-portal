using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public interface IAppointmentLanguagesAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentLanguageDto>> GetListAsync(GetAppointmentLanguagesInput input);
    Task<AppointmentLanguageDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<AppointmentLanguageDto> CreateAsync(AppointmentLanguageCreateDto input);
    Task<AppointmentLanguageDto> UpdateAsync(Guid id, AppointmentLanguageUpdateDto input);
}