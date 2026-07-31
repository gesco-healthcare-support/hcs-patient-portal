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
    /// <summary>
    /// 2026-05-15 (slot rework plan 4) -- preview projection for the
    /// multi-axis generation input. Pure function over the input shape:
    /// expands the (date range x selected weekdays x time ranges) cartesian
    /// product into per-slot rows, then flags conflicts against existing
    /// slots at the same location.
    /// </summary>
    Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(DoctorAvailabilityGenerateInputDto input);

    /// <summary>
    /// 2026-05-15 (slot rework plan 4) -- persist every non-conflicted slot
    /// from the preview projection of the supplied input. Transactional --
    /// all-or-nothing for the inserts; conflicted slots are silently skipped
    /// (counts and conflict rows are returned so the SPA can show
    /// "N inserted, K skipped" feedback). Capped at 5,000 slots per call
    /// (locked decision Q2).
    /// </summary>
    Task<DoctorAvailabilityCreateRangeResultDto> CreateRangeAsync(DoctorAvailabilityGenerateInputDto input);

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

    /// <summary>
    /// #2 (2026-06-19) -- the booked/reserved patient names per slot, for the
    /// internal week-view chips. Bulk (one call for the visible week's slots) to
    /// avoid N+1. Returns only slots that have at least one non-terminal
    /// appointment. Internal-only (gated on DoctorAvailabilities.Default).
    /// </summary>
    Task<List<SlotPatientNamesDto>> GetSlotPatientNamesAsync(List<Guid> slotIds);
}