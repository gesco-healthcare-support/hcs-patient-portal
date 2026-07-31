using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;

/// <summary>
/// IT Admin / Staff Supervisor manages the M:N Doctor-to-Location
/// preferences that drive the booking-form Location dropdown's per-doctor
/// scoping. Phase 7b (2026-05-03).
/// </summary>
public interface IDoctorPreferredLocationsAppService : IApplicationService
{
    /// <summary>
    /// Active and inactive rows for the supplied Doctor. UI uses this to
    /// render the toggle list -- both On and Off rows are returned so
    /// the supervisor can see what was previously toggled off.
    /// </summary>
    Task<List<DoctorPreferredLocationDto>> GetByDoctorAsync(Guid doctorId);

    /// <summary>
    /// Upserts the (DoctorId, LocationId) row and sets <c>IsActive</c>
    /// per the input. If the row exists, flips <c>IsActive</c>; if not,
    /// inserts a new row. Mirrors OLD's add-or-update upsert.
    /// </summary>
    Task<DoctorPreferredLocationDto> ToggleAsync(ToggleDoctorPreferredLocationInput input);
}
