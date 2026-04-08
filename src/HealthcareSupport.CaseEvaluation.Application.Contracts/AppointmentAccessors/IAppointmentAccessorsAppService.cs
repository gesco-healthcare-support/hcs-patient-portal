using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public interface IAppointmentAccessorsAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentAccessorWithNavigationPropertiesDto>> GetListAsync(GetAppointmentAccessorsInput input);
    Task<AppointmentAccessorWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<AppointmentAccessorDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<AppointmentAccessorDto> CreateAsync(AppointmentAccessorCreateDto input);
    Task<AppointmentAccessorDto> UpdateAsync(Guid id, AppointmentAccessorUpdateDto input);
}