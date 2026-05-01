using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public interface IAppointmentInjuryDetailsAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentInjuryDetailWithNavigationPropertiesDto>> GetListAsync(GetAppointmentInjuryDetailsInput input);
    Task<AppointmentInjuryDetailWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<List<AppointmentInjuryDetailWithNavigationPropertiesDto>> GetByAppointmentIdAsync(Guid appointmentId);
    Task<AppointmentInjuryDetailDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetWcabOfficeLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<AppointmentInjuryDetailDto> CreateAsync(AppointmentInjuryDetailCreateDto input);
    Task<AppointmentInjuryDetailDto> UpdateAsync(Guid id, AppointmentInjuryDetailUpdateDto input);
}
