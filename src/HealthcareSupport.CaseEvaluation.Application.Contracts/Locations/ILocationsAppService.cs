using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Locations;

public interface ILocationsAppService : IApplicationService
{
    Task<PagedResultDto<LocationWithNavigationPropertiesDto>> GetListAsync(GetLocationsInput input);
    Task<LocationWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<LocationDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<LocationDto> CreateAsync(LocationCreateDto input);
    Task<LocationDto> UpdateAsync(Guid id, LocationUpdateDto input);
    Task DeleteByIdsAsync(List<Guid> locationIds);
    Task DeleteAllAsync(GetLocationsInput input);
}