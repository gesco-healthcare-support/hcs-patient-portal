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
/// 2026-11-06 (R3) -- manual controller forwarding to <see cref="IConfirmationNumberLookupsAppService"/>
/// (the app service carries <c>[RemoteService(IsEnabled = false)]</c>, so this is its only route). A POST:
/// the patient's name, date of birth and claim number travel in the body, never in a URL or an access
/// log. The class-level <c>[Authorize]</c> repeats the app service's permission on purpose.
/// </summary>
[Authorize(CaseEvaluationPermissions.Appointments.Create)]
[RemoteService]
[Area("app")]
[ControllerName("ConfirmationNumberLookup")]
[Route("api/app/confirmation-number-lookups")]
public class ConfirmationNumberLookupsController : AbpController, IConfirmationNumberLookupsAppService
{
    private readonly IConfirmationNumberLookupsAppService _service;

    public ConfirmationNumberLookupsController(IConfirmationNumberLookupsAppService service)
    {
        _service = service;
    }

    [HttpPost]
    public virtual Task RequestAsync([FromBody] ConfirmationNumberLookupDto input)
    {
        return _service.RequestAsync(input);
    }
}
