using Asp.Versioning;
using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// HTTP surface for the manual Case Tracker push. Sits under
/// <c>api/app/case-tracker</c> so the integration's own endpoints stay grouped and separate from the
/// appointment CRUD controllers.
/// </summary>
[IgnoreAntiforgeryToken]
[Area("app")]
[ControllerName("CaseTrackerPush")]
[Route("api/app/case-tracker")]
public class CaseTrackerPushController : AbpController
{
    private readonly ICaseTrackerPushAppService _caseTrackerPushAppService;

    public CaseTrackerPushController(ICaseTrackerPushAppService caseTrackerPushAppService)
    {
        _caseTrackerPushAppService = caseTrackerPushAppService;
    }

    [HttpPost]
    [Route("appointments/{id}/push")]
    public virtual Task<CaseTrackerPushQueuedDto> PushAppointmentAsync(Guid id)
    {
        return _caseTrackerPushAppService.PushAppointmentAsync(id);
    }
}
