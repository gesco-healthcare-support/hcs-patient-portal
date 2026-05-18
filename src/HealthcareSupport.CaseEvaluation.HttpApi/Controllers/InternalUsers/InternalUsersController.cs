using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.InternalUsers;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.InternalUsers;

/// <summary>
/// Manual HTTP-API surface for the IT-Admin internal-user-creation flow.
/// Routes the create + tenant-options operations from
/// <c>/api/app/internal-users/*</c> to
/// <see cref="IInternalUsersAppService"/>. Per project convention the
/// AppService carries <c>[RemoteService(IsEnabled = false)]</c> so this
/// controller is the only public route; the class-level
/// <c>[Authorize]</c> on the AppService is what enforces the
/// permission, not anything here -- the controller stays a thin
/// pass-through.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("InternalUsers")]
[Route("api/app/internal-users")]
public class InternalUsersController : AbpController
{
    private readonly IInternalUsersAppService _internalUsersAppService;

    public InternalUsersController(IInternalUsersAppService internalUsersAppService)
    {
        _internalUsersAppService = internalUsersAppService;
    }

    /// <summary>
    /// Creates a new internal user (Clinic Staff or Staff Supervisor).
    /// Returns 200 with <see cref="InternalUserCreatedDto"/> on success,
    /// 400 on validation failure (six known error codes; see
    /// <c>CaseEvaluationDomainErrorCodes.InternalUser*</c>), 403 when
    /// the caller lacks the
    /// <c>CaseEvaluation.InternalUsers.Create</c> permission.
    /// </summary>
    [Authorize]
    [HttpPost]
    public virtual Task<InternalUserCreatedDto> CreateAsync(
        [FromBody] CreateInternalUserDto input)
    {
        return _internalUsersAppService.CreateAsync(input);
    }

    /// <summary>
    /// Returns the active tenants for the form's tenant-picker
    /// dropdown. <c>AllowAnonymous</c> on the AppService method is
    /// intentional: the SPA route guard + the class-level
    /// <c>[Authorize]</c> on the AppService already require an
    /// authenticated IT Admin; this endpoint only ships tenant names
    /// (no PHI / no internal data) so widening the gate keeps the
    /// dropdown populating reliably even before the OAuth dance
    /// completes.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [Route("tenants")]
    public virtual Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(
        [FromQuery] string? filter = null)
    {
        return _internalUsersAppService.GetTenantOptionsAsync(filter);
    }
}
