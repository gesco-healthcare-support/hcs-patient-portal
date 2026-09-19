using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// HTTP surface for the admin dead-letter screen. Sits under <c>api/app/case-tracker</c> alongside the
/// manual push so the integration's endpoints stay grouped.
///
/// <para>An EXPLICIT route rather than relying on ABP's conventional controller generation, because the
/// Angular side calls these paths as literal strings through <c>RestService</c> (the pattern used
/// throughout this app). Choosing the route here means the front end and back end agree by construction
/// instead of depending on what the route convention happens to derive from the service name.</para>
///
/// <para>Authorization lives on the app service, which gates the list on
/// <c>ViewIntegrationDeadLetters</c> and the retry on <c>PushToCaseTracker</c> -- reading the failure
/// list is a narrower capability than re-sending PHI.</para>
/// </summary>
[Area("app")]
[ControllerName("CaseTrackerDeadLetter")]
[Route("api/app/case-tracker")]
public class CaseTrackerDeadLetterController : AbpController
{
    private readonly ICaseTrackerDeadLetterAppService _deadLetterAppService;

    public CaseTrackerDeadLetterController(ICaseTrackerDeadLetterAppService deadLetterAppService)
    {
        _deadLetterAppService = deadLetterAppService;
    }

    /// <summary>Outstanding dead letters across every office, newest first.</summary>
    [HttpGet]
    [Route("dead-letters")]
    public virtual Task<List<CaseTrackerDeadLetterDto>> GetDeadLettersAsync()
    {
        return _deadLetterAppService.GetListAsync();
    }

    /// <summary>
    /// Re-sends the appointment from current data and marks this dead letter resolved. The office is in
    /// the path because the row lives in that office's database and this call arrives on the host surface
    /// with no office context of its own.
    /// </summary>
    [HttpPost]
    [Route("offices/{officeId}/dead-letters/{id}/retry")]
    public virtual Task<CaseTrackerDeadLetterRetryResultDto> RetryDeadLetterAsync(Guid officeId, Guid id)
    {
        return _deadLetterAppService.RetryAsync(officeId, id);
    }
}
