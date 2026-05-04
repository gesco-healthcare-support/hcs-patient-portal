using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- manual HTTP surface for the external-user
/// cancel/reschedule submit + supervisor approve/reject endpoints.
/// Phase 16 will add reschedule submit; Phase 17 (Session B) will add
/// the supervisor-side approve/reject routes (likely as a separate
/// partial controller file per the 2-session-split file ownership
/// rule).
/// </summary>
[RemoteService]
[Area("app")]
[Route("api/app/appointment-change-requests")]
public class AppointmentChangeRequestController : AbpController, IAppointmentChangeRequestsAppService
{
    private readonly IAppointmentChangeRequestsAppService _appointmentChangeRequestsAppService;

    public AppointmentChangeRequestController(IAppointmentChangeRequestsAppService appointmentChangeRequestsAppService)
    {
        _appointmentChangeRequestsAppService = appointmentChangeRequestsAppService;
    }

    /// <summary>
    /// External user submits a cancellation request on an Approved
    /// appointment. Body carries the cancellation reason.
    /// </summary>
    [HttpPost]
    [Route("cancel/{appointmentId}")]
    public virtual Task<AppointmentChangeRequestDto> RequestCancellationAsync(
        Guid appointmentId,
        [FromBody] RequestCancellationDto input)
    {
        return _appointmentChangeRequestsAppService.RequestCancellationAsync(appointmentId, input);
    }
}
