using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Reports;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Reports;

/// <summary>
/// G-08-04 (2026-06-07) -- manual controller for the per-appointment Patient
/// Demographics PDF. Streams the file from
/// <see cref="IAppointmentDemographicsAppService"/> (which is
/// <c>[RemoteService(IsEnabled = false)]</c>, so this controller owns the route).
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("AppointmentDemographics")]
[Route("api/app/appointment-demographics")]
public class AppointmentDemographicsController : AbpController
{
    private readonly IAppointmentDemographicsAppService _demographicsAppService;

    public AppointmentDemographicsController(IAppointmentDemographicsAppService demographicsAppService)
    {
        _demographicsAppService = demographicsAppService;
    }

    [HttpGet("{appointmentId}")]
    public virtual async Task<IActionResult> GetPdfAsync(Guid appointmentId)
    {
        var result = await _demographicsAppService.GetPdfAsync(appointmentId);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
