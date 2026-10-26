using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// Per-office connection to the Case Tracker. Shares the <c>api/app/case-tracker</c> prefix
/// with <see cref="CaseTrackerDeadLetterController"/> so the integration's admin endpoints stay
/// grouped, and is a separate controller because the two manage different things -- one inspects
/// failures, this one decides whether the portal sends at all.
///
/// <para>Explicit routes for the same reason as its sibling: the Angular side calls these as literal
/// strings through <c>RestService</c>, so pinning the route here makes both sides agree by
/// construction. Authorization lives on the app service.</para>
/// </summary>
[Area("app")]
[ControllerName("CaseTrackerOffices")]
[Route("api/app/case-tracker")]
public class CaseTrackerOfficesController : AbpController
{
    private readonly ICaseTrackerPushSettingsAppService _pushSettingsAppService;

    public CaseTrackerOfficesController(ICaseTrackerPushSettingsAppService pushSettingsAppService)
    {
        _pushSettingsAppService = pushSettingsAppService;
    }

    /// <summary>Every office with its push state and pending backlog, by name.</summary>
    [HttpGet]
    [Route("offices")]
    public virtual Task<List<CaseTrackerOfficePushStateDto>> GetOfficesAsync()
    {
        return _pushSettingsAppService.GetOfficesAsync();
    }

    /// <summary>
    /// Connects one office to the Case Tracker: starts its feed if it is not on, and turns its switch on.
    /// POST, but connecting a connected office changes nothing, so a retried request cannot compound.
    /// </summary>
    [HttpPost]
    [Route("offices/{officeId}/connect")]
    public virtual Task<CaseTrackerOfficePushStateDto> ConnectAsync(Guid officeId)
    {
        return _pushSettingsAppService.ConnectAsync(officeId);
    }

    /// <summary>Disconnects one office: its switch goes off and its changes wait.</summary>
    [HttpPost]
    [Route("offices/{officeId}/disconnect")]
    public virtual Task<CaseTrackerOfficePushStateDto> DisconnectAsync(Guid officeId)
    {
        return _pushSettingsAppService.DisconnectAsync(officeId);
    }
}
