using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// Per-office control of the Case Tracker push switch. Shares the <c>api/app/case-tracker</c> prefix
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
    /// Turns the push on or off for one office. PUT rather than POST: the same call with the same body
    /// leaves the same state, so a retried request cannot compound.
    /// </summary>
    [HttpPut]
    [Route("offices/{officeId}/push")]
    public virtual Task<CaseTrackerOfficePushStateDto> SetPushEnabledAsync(
        Guid officeId,
        [FromBody] CaseTrackerPushToggleInput input)
    {
        return _pushSettingsAppService.SetPushEnabledAsync(officeId, input.Enabled);
    }
}

/// <summary>Body for the toggle. A named type rather than a bare bool so the JSON stays readable.</summary>
public class CaseTrackerPushToggleInput
{
    public bool Enabled { get; set; }
}
