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
/// is the credential. GET is read-only (safe for email scanner prefetch); the decision
/// is recorded only on POST.
///
/// <para><b>Rate limiting, corrected 2026-09-08 (#702).</b> This previously said limiting
/// "falls back to ABP's global IP fixed-window limiter". <b>There is no such fallback.</b>
/// The chained limiter in <c>CaseEvaluationHttpApiHostModule</c> names four path prefixes
/// and returns <c>GetNoLimiter</c> for everything else, so this endpoint was entirely
/// unthrottled. It is now matched by <c>IsChangeRequestConsentPath</c> and capped at
/// <c>ConsentRequestsPerHour</c> per source IP, on both verbs.</para>
///
/// <para>That limiter bounds unauthenticated database load -- both verbs perform a
/// token-hash lookup across two stores -- and is NOT a guessing control. The token is 32
/// cryptographic random bytes (~256 bits, SHA-256 at rest), so a per-token throttle would
/// add nothing against exhaustion.</para>
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
