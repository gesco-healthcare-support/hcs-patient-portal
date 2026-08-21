using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- anonymous public surface for opposing-side consent.
/// Mirrors <c>PublicDocumentUploadController</c>: the <c>api/public</c> namespace,
/// class-level <c>[IgnoreAntiforgeryToken]</c>, action-level <c>[AllowAnonymous]</c>,
/// pure passthrough to <see cref="IPublicChangeRequestConsentAppService"/>. The token
/// is the credential; rate limiting falls back to ABP's global IP fixed-window limiter
/// (per-token throttle is a documented follow-up). GET is read-only (safe for email
/// scanner prefetch); the decision is recorded only on POST.
/// </summary>
[IgnoreAntiforgeryToken]
[Route("api/public/change-request-consent")]
public class PublicChangeRequestConsentController : AbpControllerBase
{
    private readonly IPublicChangeRequestConsentAppService _appService;

    public PublicChangeRequestConsentController(IPublicChangeRequestConsentAppService appService)
    {
        _appService = appService;
    }

    [HttpGet("{token}")]
    [AllowAnonymous]
    public Task<ChangeRequestConsentInfoDto> GetAsync(string token)
    {
        return _appService.GetConsentInfoAsync(token);
    }

    [HttpPost("{token}")]
    [AllowAnonymous]
    public Task<ChangeRequestConsentInfoDto> SubmitAsync(string token, [FromBody] SubmitChangeRequestConsentDto input)
    {
        return _appService.SubmitDecisionAsync(token, input);
    }
}
