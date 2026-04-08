using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public interface IAppointmentApplicantAttorneysAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentApplicantAttorneyWithNavigationPropertiesDto>> GetListAsync(GetAppointmentApplicantAttorneysInput input);
    Task<AppointmentApplicantAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<AppointmentApplicantAttorneyDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetApplicantAttorneyLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<AppointmentApplicantAttorneyDto> CreateAsync(AppointmentApplicantAttorneyCreateDto input);
    Task<AppointmentApplicantAttorneyDto> UpdateAsync(Guid id, AppointmentApplicantAttorneyUpdateDto input);
}