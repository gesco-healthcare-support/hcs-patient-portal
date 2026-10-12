using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.MyClaimExaminerProfiles;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.MyClaimExaminerProfiles;

/// <summary>
/// The single HTTP surface for <see cref="IMyClaimExaminerProfileAppService"/>. The service is not
/// auto-exposed; these are the routes the generated Angular client already calls. Authorization lives on
/// the app service.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("MyClaimExaminerProfile")]
[Route("api/app/my-claim-examiner-profile")]
public class MyClaimExaminerProfileController : AbpController, IMyClaimExaminerProfileAppService
{
    private readonly IMyClaimExaminerProfileAppService _service;

    public MyClaimExaminerProfileController(IMyClaimExaminerProfileAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<MyClaimExaminerProfileDto> GetAsync() => _service.GetAsync();

    [HttpPut]
    public virtual Task<MyClaimExaminerProfileDto> UpdateAsync([FromBody] UpdateMyClaimExaminerProfileInput input) =>
        _service.UpdateAsync(input);
}
