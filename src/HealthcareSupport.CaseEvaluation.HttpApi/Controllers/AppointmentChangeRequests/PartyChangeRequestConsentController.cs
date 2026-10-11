using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentChangeRequests;

/// <summary>
/// Manual controller forwarding to <see cref="IPartyChangeRequestConsentAppService"/> (the app
/// service carries <c>[RemoteService(IsEnabled = false)]</c>, so this is its only route).
/// Authenticated, unlike the token-based <c>PublicChangeRequestConsentController</c>; who may
/// answer is decided in the app service.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("PartyChangeRequestConsent")]
[Route("api/app/change-request-consents")]
public class PartyChangeRequestConsentController : AbpController, IPartyChangeRequestConsentAppService
{
    private readonly IPartyChangeRequestConsentAppService _service;

    public PartyChangeRequestConsentController(IPartyChangeRequestConsentAppService service)
    {
        _service = service;
    }

    [HttpGet("mine")]
    public virtual Task<ListResultDto<PartyConsentRequestDto>> GetMyPendingAsync([FromQuery] Guid? appointmentId)
    {
        return _service.GetMyPendingAsync(appointmentId);
    }

    [HttpPost("{changeRequestId}/decision")]
    public virtual Task<PartyConsentRequestDto> SubmitMyDecisionAsync(
        Guid changeRequestId,
        [FromBody] SubmitChangeRequestConsentDto input)
    {
        return _service.SubmitMyDecisionAsync(changeRequestId, input);
    }
}
