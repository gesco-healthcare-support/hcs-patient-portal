using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using HealthcareSupport.CaseEvaluation.UserQueries;

namespace HealthcareSupport.CaseEvaluation.Controllers.UserQueries;

[RemoteService]
[Area("app")]
[ControllerName("UserQuery")]
[Route("api/app/user-queries")]
public class UserQueryController : AbpController, IUserQueryAppService
{
    protected IUserQueryAppService _userQueryAppService;

    public UserQueryController(IUserQueryAppService userQueryAppService)
    {
        _userQueryAppService = userQueryAppService;
    }

    [HttpPost]
    public virtual Task CreateAsync(UserQueryCreateDto input)
    {
        return _userQueryAppService.CreateAsync(input);
    }
}
