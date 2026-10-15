using Asp.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using HealthcareSupport.CaseEvaluation.SystemParameters;

namespace HealthcareSupport.CaseEvaluation.Controllers.SystemParametersControllers;

/// <summary>
/// Manual HTTP surface for the per-tenant <c>SystemParameter</c> singleton.
/// Mirrors OLD <c>SystemParametersController</c>'s GET-by-id and PUT
/// endpoints; OLD's POST / PATCH / DELETE / GET-list are NOT ported (Phase 3
/// audit confirmed the Angular UI never used them and the singleton-per-
/// tenant invariant makes Create / Delete unsafe).
///
/// Authorization is enforced at the AppService layer per repo convention
/// (<c>HttpApi/CLAUDE.md</c>); this controller is a pure pass-through.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("SystemParameters")]
[Route("api/app/system-parameters")]
public class SystemParametersController : AbpController, ISystemParametersAppService
{
    protected ISystemParametersAppService SystemParametersAppService { get; }

    public SystemParametersController(ISystemParametersAppService systemParametersAppService)
    {
        SystemParametersAppService = systemParametersAppService;
    }

    [HttpGet]
    public virtual Task<SystemParameterDto> GetAsync()
    {
        return SystemParametersAppService.GetAsync();
    }

    [HttpPut]
    public virtual Task<SystemParameterDto> UpdateAsync(SystemParameterUpdateDto input)
    {
        return SystemParametersAppService.UpdateAsync(input);
    }
}
