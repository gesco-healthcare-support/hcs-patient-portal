using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.ExternalSignups;

/// <summary>
/// The signed-in routes of <see cref="IExternalSignupAppService"/> that the generated Angular client
/// (<c>angular/src/app/proxy/external-signups/external-signup.service.ts</c>) calls. Each route,
/// verb and parameter binding matches that client exactly, so it keeps working without being
/// regenerated: a near miss here would surface only at runtime.
/// </summary>
/// <remarks>
/// The service is not auto-exposed (<c>[RemoteService(IsEnabled = false)]</c>), so these actions and
/// the public <see cref="ExternalSignupController"/> are its whole HTTP surface. Each service method
/// still carries its own permission attribute; <c>[Authorize]</c> here only refuses an anonymous
/// caller before the service is reached.
/// </remarks>
[RemoteService]
[Area("app")]
[ControllerName("ExternalSignupStaff")]
[Route("api/app/external-signup")]
[Authorize]
public class ExternalSignupStaffController : AbpController
{
    private readonly IExternalSignupAppService _externalSignupAppService;

    public ExternalSignupStaffController(IExternalSignupAppService externalSignupAppService)
    {
        _externalSignupAppService = externalSignupAppService;
    }

    [HttpGet]
    [Route("active-invited-emails")]
    public virtual Task<List<string>> GetActiveInvitedEmailsAsync([FromQuery] List<string> emails)
    {
        return _externalSignupAppService.GetActiveInvitedEmailsAsync(emails);
    }

    [HttpGet]
    [Route("tenant-options")]
    public virtual Task<ListResultDto<LookupDto<System.Guid>>> GetTenantOptionsAsync([FromQuery] string? filter = null)
    {
        return _externalSignupAppService.GetTenantOptionsAsync(filter);
    }

    [HttpPost]
    [Route("send-portal-link")]
    public virtual Task SendPortalLinkAsync([FromBody] SendPortalLinkInput input)
    {
        return _externalSignupAppService.SendPortalLinkAsync(input);
    }
}
