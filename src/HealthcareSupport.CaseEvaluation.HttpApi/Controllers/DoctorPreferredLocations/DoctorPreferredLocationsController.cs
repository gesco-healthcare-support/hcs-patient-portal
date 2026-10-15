using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.DoctorPreferredLocations;

/// <summary>
/// Manual HTTP surface for the Doctor-Location preference toggle.
/// Phase 7b (2026-05-03). Authorization is enforced at the AppService
/// layer per repo convention.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("DoctorPreferredLocations")]
[Route("api/app/doctor-preferred-locations")]
public class DoctorPreferredLocationsController : AbpController, IDoctorPreferredLocationsAppService
{
    protected IDoctorPreferredLocationsAppService _appService;

    public DoctorPreferredLocationsController(IDoctorPreferredLocationsAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    [Route("by-doctor/{doctorId:guid}")]
    public virtual Task<List<DoctorPreferredLocationDto>> GetByDoctorAsync(Guid doctorId)
    {
        return _appService.GetByDoctorAsync(doctorId);
    }

    [HttpPost]
    [Route("toggle")]
    public virtual Task<DoctorPreferredLocationDto> ToggleAsync(ToggleDoctorPreferredLocationInput input)
    {
        return _appService.ToggleAsync(input);
    }
}
