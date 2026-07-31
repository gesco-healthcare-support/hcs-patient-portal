using HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentInfoRequests;

/// <summary>
/// Send Back / Request-more-information (2026-06-14). Manual HTTP surface:
/// staff send-back, external resubmit, and the read-open endpoint the external
/// fix-it page calls. Thin passthrough; permissions are enforced in the
/// AppService.
/// </summary>
[RemoteService]
[Area("app")]
[Route("api/app/appointment-info-requests")]
public class AppointmentInfoRequestController : AbpController, IAppointmentInfoRequestsAppService
{
    private readonly IAppointmentInfoRequestsAppService _service;

    public AppointmentInfoRequestController(IAppointmentInfoRequestsAppService service)
    {
        _service = service;
    }

    /// <summary>Staff: flag fields + note, move the appointment to InfoRequested.</summary>
    [HttpPost]
    [Route("send-back/{appointmentId}")]
    public virtual Task<AppointmentInfoRequestDto> SendBackAsync(
        Guid appointmentId,
        [FromBody] SendBackAppointmentInput input)
        => _service.SendBackAsync(appointmentId, input);

    /// <summary>External party: apply the requester's corrections to the flagged fields.</summary>
    [HttpPost]
    [Route("corrections/{appointmentId}")]
    public virtual Task SaveCorrectionsAsync(
        Guid appointmentId,
        [FromBody] SaveInfoRequestCorrectionsInput input)
        => _service.SaveCorrectionsAsync(appointmentId, input);

    /// <summary>External party: resolve the open request, move back to Pending.</summary>
    [HttpPost]
    [Route("resubmit/{appointmentId}")]
    public virtual Task ResubmitAsync(Guid appointmentId)
        => _service.ResubmitAsync(appointmentId);

    /// <summary>External party: current Claim Information rows to prefill the fix-it editor.</summary>
    [HttpGet]
    [Route("injury-details/{appointmentId}")]
    public virtual Task<List<InjuryDetailCorrectionDto>> GetInjuryDetailsForCorrectionAsync(Guid appointmentId)
        => _service.GetInjuryDetailsForCorrectionAsync(appointmentId);

    /// <summary>The open info request (note + flagged fields) for the fix-it page.</summary>
    [HttpGet]
    [Route("open/{appointmentId}")]
    public virtual Task<AppointmentInfoRequestDto?> GetOpenAsync(Guid appointmentId)
        => _service.GetOpenAsync(appointmentId);

    /// <summary>Staff: the full Send Back history (rounds + per-field diff) for review.</summary>
    [HttpGet]
    [Route("history/{appointmentId}")]
    public virtual Task<List<AppointmentInfoRequestRoundDto>> GetHistoryAsync(Guid appointmentId)
        => _service.GetHistoryAsync(appointmentId);
}
