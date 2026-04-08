using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public interface IDoctorAvailabilitiesAppService : IApplicationService
{
    Task<PagedResultDto<DoctorAvailabilityWithNavigationPropertiesDto>> GetListAsync(GetDoctorAvailabilitiesInput input);
    Task<DoctorAvailabilityWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DoctorAvailabilityDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task DeleteBySlotAsync(DoctorAvailabilityDeleteBySlotInputDto input);
    Task DeleteByDateAsync(DoctorAvailabilityDeleteByDateInputDto input);
    Task<DoctorAvailabilityDto> CreateAsync(DoctorAvailabilityCreateDto input);
    Task<DoctorAvailabilityDto> UpdateAsync(Guid id, DoctorAvailabilityUpdateDto input);
    Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(List<DoctorAvailabilityGenerateInputDto> input);
}