using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public interface IAppointmentEmployerDetailsAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentEmployerDetailWithNavigationPropertiesDto>> GetListAsync(GetAppointmentEmployerDetailsInput input);
    Task<AppointmentEmployerDetailWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<AppointmentEmployerDetailDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<AppointmentEmployerDetailDto> CreateAsync(AppointmentEmployerDetailCreateDto input);
    Task<AppointmentEmployerDetailDto> UpdateAsync(Guid id, AppointmentEmployerDetailUpdateDto input);
}