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
    /// at the AppService method via
    /// <c>[Authorize(CaseEvaluationPermissions.UserManagement.InviteExternalUser)]</c>
    /// (permission-based, not role-based -- the permission is granted to
    /// IT Admin / Staff Supervisor / Intake Staff via
    /// <c>CaseEvaluationPermissionDefinitionProvider</c>). External users
    /// would receive 403 even if they discovered the route.
    /// </summary>
    [Authorize]
    [HttpPost]
    [Route("invite")]
    public virtual Task<InviteExternalUserResultDto> InviteExternalUserAsync(
        [FromBody] InviteExternalUserDto input)
    {
        return _externalSignupAppService.InviteExternalUserAsync(input);
    }

    /// <summary>
    /// 2026-06-16 (Prompt 16, A-B1): paged invite-management list for the
    /// internal "Pending Invites" surface. Authorization is enforced at the
    /// AppService method (InviteExternalUser permission).
    /// </summary>
    [Authorize]
    [HttpGet]
    [Route("invites")]
    public virtual Task<Volo.Abp.Application.Dtos.PagedResultDto<InvitationDto>> GetInvitesAsync(
        [FromQuery] GetInvitesInput input)
    {
        return _externalSignupAppService.GetInvitesAsync(input);
    }

    /// <summary>2026-06-16 (A-B1): resend (re-issue) a pending invitation.</summary>
    [Authorize]
    [HttpPost]
    [Route("invites/{id}/resend")]
    public virtual Task<InviteExternalUserResultDto> ResendInviteAsync(System.Guid id)
    {
        return _externalSignupAppService.ResendInviteAsync(id);
    }

    /// <summary>2026-06-16 (A-B1): revoke (soft-delete) a pending invitation.</summary>
    [Authorize]
    [HttpPost]
    [Route("invites/{id}/revoke")]
    public virtual Task RevokeInviteAsync(System.Guid id)
    {
        return _externalSignupAppService.RevokeInviteAsync(id);
    }
}
