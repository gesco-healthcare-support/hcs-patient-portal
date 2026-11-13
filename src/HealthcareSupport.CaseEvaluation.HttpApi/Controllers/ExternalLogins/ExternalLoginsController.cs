using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.ExternalLogins;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.ExternalLogins;

/// <summary>
/// 2026-11-13 -- manual controller forwarding to <see cref="IExternalLoginsAppService"/> (the app service carries
/// <c>[RemoteService(IsEnabled = false)]</c>, so these are its only routes). The class-level
/// <c>[Authorize]</c> repeats the app service's permission on purpose, so this surface is never reachable
/// without it and CodeQL's function-level access-control check can see it here.
/// </summary>
[Authorize(CaseEvaluationPermissions.UserManagement.SwitchOffExternalUser)]
[RemoteService]
[Area("app")]
[ControllerName("ExternalLogin")]
[Route("api/app/external-logins")]
public class ExternalLoginsController : AbpController, IExternalLoginsAppService
{
    private readonly IExternalLoginsAppService _service;

    public ExternalLoginsController(IExternalLoginsAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ExternalLoginDto>> GetListAsync([FromQuery] GetExternalLoginsInput input)
    {
        return _service.GetListAsync(input);
    }

    [HttpPut("{id}/active")]
    public virtual Task<ExternalLoginDto> SetActiveAsync(Guid id, [FromBody] ExternalLoginSetActiveDto input)
    {
        return _service.SetActiveAsync(id, input);
    }
}
