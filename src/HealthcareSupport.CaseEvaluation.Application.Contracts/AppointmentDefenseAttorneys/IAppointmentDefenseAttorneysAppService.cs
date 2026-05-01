using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public interface IAppointmentDefenseAttorneysAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentDefenseAttorneyWithNavigationPropertiesDto>> GetListAsync(GetAppointmentDefenseAttorneysInput input);
    Task<AppointmentDefenseAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<AppointmentDefenseAttorneyDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetDefenseAttorneyLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<AppointmentDefenseAttorneyDto> CreateAsync(AppointmentDefenseAttorneyCreateDto input);
    Task<AppointmentDefenseAttorneyDto> UpdateAsync(Guid id, AppointmentDefenseAttorneyUpdateDto input);
}
