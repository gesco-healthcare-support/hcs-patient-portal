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
}
