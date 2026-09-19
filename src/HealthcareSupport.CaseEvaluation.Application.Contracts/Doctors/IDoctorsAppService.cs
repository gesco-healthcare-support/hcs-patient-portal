using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public interface IDoctorsAppService : IApplicationService
{
    Task<PagedResultDto<DoctorWithNavigationPropertiesDto>> GetListAsync(GetDoctorsInput input);
    Task<DoctorWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DoctorDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetTenantLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DoctorDto> CreateAsync(DoctorCreateDto input);
    Task<DoctorDto> UpdateAsync(Guid id, DoctorUpdateDto input);
}