using Asp.Versioning;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.ExternalAccount;

/// <summary>
/// Phase 10 (2026-05-03) -- manual HTTP surface for the OLD-parity
/// password-reset flow. Both endpoints are anonymous (the user has not
/// yet logged in) and rate-limited via the
/// <c>password-reset-by-email</c> partition wired in
/// <c>CaseEvaluationHttpApiHostModule</c>: 5 requests / hour /
/// (email | sub | client-ip).
///
/// <para>Routes are versioned under <c>api/public/external-account/</c>
/// to match the convention established by
/// <c>ExternalSignupController</c> for OLD-parity public surfaces. The
/// <c>[IgnoreAntiforgeryToken]</c> attribute mirrors the signup
/// controller -- requests originate from the AuthServer Razor page
/// and the Angular SPA reset page, neither of which round-trips an
/// ABP antiforgery cookie.</para>
/// </summary>
[IgnoreAntiforgeryToken]
[Area("app")]
[ControllerName("ExternalAccount")]
[Route("api/public/external-account")]
public class ExternalAccountController : AbpController
{
    private readonly IExternalAccountAppService _externalAccountAppService;

    public ExternalAccountController(IExternalAccountAppService externalAccountAppService)
    {
        _externalAccountAppService = externalAccountAppService;
    }

    [AllowAnonymous]
    [HttpPost]
    [Route("send-password-reset-code")]
    public virtual Task SendPasswordResetCodeAsync([FromBody] SendPasswordResetCodeInput input)
    {
        return _externalAccountAppService.SendPasswordResetCodeAsync(input);
    }

    [AllowAnonymous]
    [HttpPost]
    [Route("reset-password")]
    public virtual Task ResetPasswordAsync([FromBody] ResetPasswordInput input)
    {
        return _externalAccountAppService.ResetPasswordAsync(input);
    }

    /// <summary>
    /// Path prefix for the rate-limited routes. The
    /// <c>CaseEvaluationHttpApiHostModule</c> wires the global rate
    /// limiter against this prefix rather than via an
    /// <c>[EnableRateLimiting]</c> attribute -- the HttpApi project is
    /// <c>Microsoft.NET.Sdk</c> (not Web SDK) and adding a framework
    /// reference here just for one attribute would pull the entire
    /// <c>Microsoft.AspNetCore.App</c> shared framework into a
    /// pure-class-library project. Keeping rate-limit wiring in
    /// HttpApi.Host (which already has the Web SDK) is the
    /// less-invasive split.
    /// </summary>
    public const string RateLimitedPathPrefix = "/api/public/external-account";
}
