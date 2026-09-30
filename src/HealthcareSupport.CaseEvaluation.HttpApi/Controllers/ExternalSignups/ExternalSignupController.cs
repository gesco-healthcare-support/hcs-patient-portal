using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.ExternalSignups;

[IgnoreAntiforgeryToken]
[Area("app")]
[ControllerName("ExternalSignup")]
[Route("api/public/external-signup")]
public class ExternalSignupController : AbpController
{
    private readonly IExternalSignupAppService _externalSignupAppService;

    public ExternalSignupController(IExternalSignupAppService externalSignupAppService)
    {
        _externalSignupAppService = externalSignupAppService;
    }

    // Signed-in only; the app service method enforces the invite permission. See its summary for why
    // this is no longer anonymous.
    [Authorize]
    [HttpGet]
    [Route("tenant-options")]
    public virtual Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(string? filter = null)
    {
        return _externalSignupAppService.GetTenantOptionsAsync(filter);
    }

    [HttpGet]
    [Route("external-user-lookup")]
    public virtual Task<ListResultDto<ExternalUserLookupDto>> GetExternalUserLookupAsync(string? filter = null)
    {
        return _externalSignupAppService.GetExternalUserLookupAsync(filter);
    }

    [AllowAnonymous]
    [HttpPost]
    [Route("register")]
    public virtual Task RegisterAsync([FromBody] ExternalUserSignUpDto input)
    {
        return _externalSignupAppService.RegisterAsync(input);
    }

    /// <summary>
    /// 1.6 (2026-04-30): anonymous tenant-name resolver used by the AuthServer
    /// JS overlay to translate invite-link `?__tenant=&lt;Name&gt;` query strings
    /// into the GUID needed for the registration POST. Always runs in host
    /// context regardless of caller's tenant cookie. Returns 404 on miss.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [Route("resolve-tenant")]
    public virtual async Task<IActionResult> ResolveTenantByNameAsync([FromQuery] string name)
    {
        var result = await _externalSignupAppService.ResolveTenantByNameAsync(name);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>
    /// 2026-05-15 -- anonymous validator for the AuthServer JS overlay on
    /// <c>/Account/Register?inviteToken=...</c>. Hashes the raw token,
    /// looks up the Invitation row, returns the resolved email + role +
    /// tenant for prefill. On 4xx, the overlay renders one of three
    /// friendly banners based on the BusinessException error code
    /// (InviteInvalid / InviteExpired / InviteAlreadyAccepted).
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [Route("validate-invite")]
    public virtual Task<InvitationValidationDto> ValidateInviteAsync([FromQuery] string token)
    {
        return _externalSignupAppService.ValidateInviteAsync(token);
    }
}
