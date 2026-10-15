using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.MyAttorneyProfiles;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.MyAttorneyProfiles;

/// <summary>
/// The single HTTP surface for <see cref="IMyAttorneyProfileAppService"/>. The service is not auto-exposed;
/// these are the routes the generated Angular client already calls. Authorization lives on the app service.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("MyAttorneyProfile")]
[Route("api/app/my-attorney-profile")]
public class MyAttorneyProfileController : AbpController, IMyAttorneyProfileAppService
{
    private readonly IMyAttorneyProfileAppService _service;

    public MyAttorneyProfileController(IMyAttorneyProfileAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<MyAttorneyProfileDto> GetAsync() => _service.GetAsync();

    [HttpPut]
    public virtual Task<MyAttorneyProfileDto> UpdateAsync([FromBody] UpdateMyAttorneyProfileInput input) =>
        _service.UpdateAsync(input);
}
