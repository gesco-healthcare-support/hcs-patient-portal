using HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;

/// <summary>
/// IT Admin / Staff Supervisor surface for the Doctor-Location preference
/// table that scopes the booking-form Location dropdown per doctor.
/// Phase 7b (2026-05-03). Mirrors OLD's
/// <c>DoctorPreferredLocationDomain</c> upsert pattern at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\DoctorPreferredLocationDomain.cs</c>:45-108.
///
/// Authorization:
///   - Class-level <c>[Authorize]</c> so authenticated booking flows can
///     read the preference list (Phase 11 will consume it).
///   - <c>ToggleAsync</c> overrides with
///     <c>DoctorPreferredLocations.Toggle</c> for the admin write path.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class DoctorPreferredLocationsAppService : CaseEvaluationAppService, IDoctorPreferredLocationsAppService
{
    private readonly IRepository<DoctorPreferredLocation> _repository;

    public DoctorPreferredLocationsAppService(IRepository<DoctorPreferredLocation> repository)
    {
        _repository = repository;
    }

    [Authorize(CaseEvaluationPermissions.DoctorPreferredLocations.Default)]
    public virtual async Task<List<DoctorPreferredLocationDto>> GetByDoctorAsync(Guid doctorId)
    {
        var rows = await _repository.GetListAsync(x => x.DoctorId == doctorId);
        return rows
            .OrderBy(x => x.LocationId)
            .Select(ObjectMapper.Map<DoctorPreferredLocation, DoctorPreferredLocationDto>)
            .ToList();
    }

    [Authorize(CaseEvaluationPermissions.DoctorPreferredLocations.Toggle)]
    public virtual async Task<DoctorPreferredLocationDto> ToggleAsync(ToggleDoctorPreferredLocationInput input)
    {
        Check.NotNull(input, nameof(input));
        if (input.DoctorId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Doctor"]]);
        }
        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }

        var existing = await _repository.FindAsync(x =>
            x.DoctorId == input.DoctorId && x.LocationId == input.LocationId);

        DoctorPreferredLocation entity;
        if (existing == null)
        {
            entity = new DoctorPreferredLocation(
                input.DoctorId, input.LocationId, CurrentTenant.Id, input.IsActive);
            await _repository.InsertAsync(entity, autoSave: true);
        }
        else
        {
            existing.IsActive = input.IsActive;
            entity = await _repository.UpdateAsync(existing, autoSave: true);
        }

        return ObjectMapper.Map<DoctorPreferredLocation, DoctorPreferredLocationDto>(entity);
    }
}
