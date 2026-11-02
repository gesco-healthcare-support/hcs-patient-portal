using Asp.Versioning;
using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Appointments;

/// <summary>
/// Phase 12 (2026-05-04) -- HTTP surface for the richer Approve / Reject
/// AppService. Sits at <c>api/app/appointment-approvals</c> -- distinct
/// from the existing <c>api/app/appointments</c> controller (Session A
/// territory) so Phase 12's surface lands without touching the main
/// <c>AppointmentController</c>. Sync 3 cleanup PR will converge the two
/// controllers when Session A's manager rewrite settles.
/// </summary>
[IgnoreAntiforgeryToken]
[Area("app")]
[ControllerName("AppointmentApprovals")]
[Route("api/app/appointment-approvals")]
public class AppointmentApprovalController : AbpController
{
    private readonly IAppointmentApprovalAppService _appointmentApprovalAppService;

    public AppointmentApprovalController(IAppointmentApprovalAppService appointmentApprovalAppService)
    {
        _appointmentApprovalAppService = appointmentApprovalAppService;
    }

    [HttpPost]
    [Route("{id}/approve")]
    public virtual Task<AppointmentDto> ApproveAppointmentAsync(Guid id, [FromBody] ApproveAppointmentInput input)
    {
        return _appointmentApprovalAppService.ApproveAppointmentAsync(id, input);
    }

    [HttpPost]
    [Route("{id}/reject")]
    public virtual Task<AppointmentDto> RejectAppointmentAsync(Guid id, [FromBody] RejectAppointmentInput input)
    {
        return _appointmentApprovalAppService.RejectAppointmentAsync(id, input);
    }
}
