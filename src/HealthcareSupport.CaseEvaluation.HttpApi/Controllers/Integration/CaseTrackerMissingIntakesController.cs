using Asp.Versioning;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// The missing-intake report (#944), under the same <c>api/app/case-tracker</c> prefix as the other Case Tracker
/// admin endpoints. An explicit route for the same reason as <see cref="CaseTrackerOfficesController"/>: the
/// Angular side calls it as a literal string. Authorization lives on the app service.
/// </summary>
[Area("app")]
[ControllerName("CaseTrackerMissingIntakes")]
[Route("api/app/case-tracker")]
public class CaseTrackerMissingIntakesController : AbpController
{
    private readonly ICaseTrackerMissingIntakeAppService _missingIntakeAppService;

    public CaseTrackerMissingIntakesController(ICaseTrackerMissingIntakeAppService missingIntakeAppService)
    {
        _missingIntakeAppService = missingIntakeAppService;
    }

    /// <summary>Every office's published appointments with no intake row. Read only.</summary>
    [HttpGet]
    [Route("missing-intakes")]
    public virtual Task<CaseTrackerMissingIntakeReportDto> GetReportAsync() =>
        _missingIntakeAppService.GetReportAsync();
}
