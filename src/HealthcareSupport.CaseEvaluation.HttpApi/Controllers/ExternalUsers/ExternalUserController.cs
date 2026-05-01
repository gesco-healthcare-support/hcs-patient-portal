using HealthcareSupport.CaseEvaluation.ExternalSignups;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Controllers.ExternalUsers;

[RemoteService]
[Area("app")]
[ControllerName("ExternalUser")]
[Route("api/app/external-users")]
public class ExternalUserController : AbpController
{
    private readonly IExternalSignupAppService _externalSignupAppService;

    public ExternalUserController(IExternalSignupAppService externalSignupAppService)
    {
        _externalSignupAppService = externalSignupAppService;
    }

    [Authorize]
    [HttpGet]
    [Route("me")]
    public virtual Task<ExternalUserProfileDto> GetMyProfileAsync()
    {
        return _externalSignupAppService.GetMyProfileAsync();
    }

    /// <summary>
    /// D.2 (2026-04-30): admin-side invite endpoint. Authorization is enforced
    /// at the AppService method ([Authorize(Roles = "admin,Staff Supervisor,IT Admin")]),
    /// not here -- the role-based gate is what makes this surface internal-only.
    /// External users would receive 403 even if they discovered the route.
    /// </summary>
    [Authorize]
    [HttpPost]
    [Route("invite")]
    public virtual Task<InviteExternalUserResultDto> InviteExternalUserAsync(
        [FromBody] InviteExternalUserDto input)
    {
        return _externalSignupAppService.InviteExternalUserAsync(input);
    }
}
