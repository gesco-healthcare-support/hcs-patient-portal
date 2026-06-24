using HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;

/// <summary>
/// IT Admin / Staff Supervisor surface for the Doctor-Location preference
/// table (a per-doctor stored preference). Phase 7b (2026-05-03). Mirrors OLD's
/// <c>DoctorPreferredLocationDomain</c> upsert pattern at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\DoctorPreferredLocationDomain.cs</c>:45-108.
///
/// NOTE (IP3, 2026-06-05): storage-only. The booking flow does NOT consume this table --
/// <c>GetLocationLookupAsync</c> queries <c>Location</c> directly, so no Location dropdown is
/// scoped by it, and there is no Angular component bound to it. Retained dormant alongside
/// the dormant Doctor entity; wire a consumer before claiming it filters anything.
///
/// Authorization:
///   - Class-level <c>[Authorize]</c> so authenticated callers can read the preference list.
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
