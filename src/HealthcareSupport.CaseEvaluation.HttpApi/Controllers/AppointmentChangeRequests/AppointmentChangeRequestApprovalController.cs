using Asp.Versioning;
using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- HTTP surface for the supervisor approve /
/// reject flows + pending list. Routed under
/// <c>/api/app/appointment-change-request-approvals</c> (sibling to
/// the existing <c>/api/app/appointment-change-requests</c>
/// controller from Phase 15+16). Sync 4 cleanup PR can converge if
/// desired.
/// </summary>
[IgnoreAntiforgeryToken]
[Area("app")]
[ControllerName("AppointmentChangeRequestApprovals")]
[Route("api/app/appointment-change-request-approvals")]
public class AppointmentChangeRequestApprovalController : AbpController
{
    private readonly IAppointmentChangeRequestsApprovalAppService _appService;

    public AppointmentChangeRequestApprovalController(
        IAppointmentChangeRequestsApprovalAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    [Route("pending")]
    public virtual Task<PagedResultDto<AppointmentChangeRequestDto>> GetPendingAsync(
        [FromQuery] GetChangeRequestsInput input)
    {
        return _appService.GetPendingChangeRequestsAsync(input);
    }

    [HttpPost]
    [Route("{id}/approve-cancellation")]
    public virtual Task<AppointmentChangeRequestDto> ApproveCancellationAsync(
        Guid id,
        [FromBody] ApproveCancellationInput input)
    {
        return _appService.ApproveCancellationAsync(id, input);
    }

    [HttpPost]
    [Route("{id}/reject-cancellation")]
    public virtual Task<AppointmentChangeRequestDto> RejectCancellationAsync(
        Guid id,
        [FromBody] RejectChangeRequestInput input)
    {
        return _appService.RejectCancellationAsync(id, input);
    }

    [HttpPost]
    [Route("{id}/approve-reschedule")]
    public virtual Task<AppointmentChangeRequestDto> ApproveRescheduleAsync(
        Guid id,
        [FromBody] ApproveRescheduleInput input)
    {
        return _appService.ApproveRescheduleAsync(id, input);
    }

    [HttpPost]
    [Route("{id}/reject-reschedule")]
    public virtual Task<AppointmentChangeRequestDto> RejectRescheduleAsync(
        Guid id,
        [FromBody] RejectChangeRequestInput input)
    {
        return _appService.RejectRescheduleAsync(id, input);
    }
}
