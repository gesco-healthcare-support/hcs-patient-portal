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
    Task<DoctorAvailabilityBulkDeleteResultDto> DeleteByDateAsync(DoctorAvailabilityDeleteByDateInputDto input);
    Task<DoctorAvailabilityDto> CreateAsync(DoctorAvailabilityCreateDto input);
    Task<DoctorAvailabilityDto> UpdateAsync(Guid id, DoctorAvailabilityUpdateDto input);
    Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(List<DoctorAvailabilityGenerateInputDto> input);

    /// <summary>
    /// Phase 7 (2026-05-03) -- booking-form slot picker. Returns slots in
    /// <c>BookingStatus.Available</c> for the supplied Location, optionally
    /// scoped by AppointmentType (matching or null = any-type). Filters past
    /// dates by default and applies the per-tenant
    /// <c>SystemParameter.AppointmentLeadTime</c> minimum-day-out gate.
    /// Open to any authenticated user -- the booking form needs this read
    /// path; admin endpoints stay gated on <c>.Default</c>.
    /// </summary>
    Task<List<DoctorAvailabilityDto>> GetDoctorAvailabilityLookupAsync(GetDoctorAvailabilityLookupInput input);
}