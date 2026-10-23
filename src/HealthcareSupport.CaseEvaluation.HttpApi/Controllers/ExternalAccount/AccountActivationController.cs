using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.ExternalAccount;

/// <summary>
/// The only HTTP surface of <see cref="IAccountActivationAppService"/> (the service carries
/// <c>[RemoteService(IsEnabled = false)]</c>). A thin pass-through: the permission checks, the
/// privileged-account rule and the one-minute cooldown all live in the service.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("AccountActivation")]
[Route("api/app/account-activation")]
[Authorize]
public class AccountActivationController : AbpController, IAccountActivationAppService
{
    private readonly IAccountActivationAppService _service;

    public AccountActivationController(IAccountActivationAppService service)
    {
        _service = service;
    }

    /// <summary>Resends the activation email to an account on the caller's identity Users screen.</summary>
    [HttpPost]
    [Route("users/{userId}/resend")]
    public virtual Task ResendToUserAsync(Guid userId) => _service.ResendToUserAsync(userId);

    /// <summary>Host only: resends the activation email to a practice's office administrator.</summary>
    [HttpPost]
    [Route("offices/{officeId}/resend-administrator")]
    public virtual Task ResendToOfficeAdministratorAsync(Guid officeId) =>
        _service.ResendToOfficeAdministratorAsync(officeId);
}
