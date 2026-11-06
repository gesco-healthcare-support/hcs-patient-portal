using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Appointments;

/// <summary>
/// 2026-11-06 -- manual controller forwarding to <see cref="IReEvaluationSourcesAppService"/> (the app
/// service carries <c>[RemoteService(IsEnabled = false)]</c>, so this is its only route).
///
/// <para>A POST, although it reads: the confirmation number and the patient's date of birth travel in
/// the body, so neither ends up in a URL, a browser history or an access log.</para>
///
/// <para>The class-level <c>[Authorize]</c> repeats the app service's permission on purpose: the
/// answer can carry a whole earlier case, so it must never be reachable without the booking
/// permission, even if the service attribute is changed.</para>
/// </summary>
[Authorize(CaseEvaluationPermissions.Appointments.Create)]
[RemoteService]
[Area("app")]
[ControllerName("ReEvaluationSource")]
[Route("api/app/re-evaluation-sources")]
public class ReEvaluationSourcesController : AbpController, IReEvaluationSourcesAppService
{
    private readonly IReEvaluationSourcesAppService _service;

    public ReEvaluationSourcesController(IReEvaluationSourcesAppService service)
    {
        _service = service;
    }

    [HttpPost("lookup")]
    public virtual Task<ReEvaluationSourceDto?> LookupAsync([FromBody] ReEvaluationSourceLookupDto input)
    {
        return _service.LookupAsync(input);
    }
}
