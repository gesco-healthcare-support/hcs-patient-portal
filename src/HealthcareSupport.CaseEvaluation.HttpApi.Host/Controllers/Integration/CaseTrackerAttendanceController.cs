using Asp.Versioning;
using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>Body of an inbound attendance report. One field, one of two exact values.</summary>
public class CaseTrackerAttendanceRequest
{
    /// <summary><c>NO_SHOW</c> or <c>NOT_SEEN</c>. See <see cref="AttendanceOutcomeWire"/>.</summary>
    public string? Outcome { get; set; }
}

/// <summary>
/// Body of a <c>409</c>, so the Case Tracker can tell a conflict worth retrying from a final one
/// (agreed with them 2026-08-18). Returned ONLY on a conflict; a <c>404</c> stays bodyless because
/// its ambiguity is what stops a token holder enumerating offices and appointments.
/// </summary>
public class CaseTrackerAttendanceConflictResponse
{
    /// <summary>
    /// The appointment's current <c>AppointmentStatusType</c> name, e.g. <c>RescheduleRequested</c>.
    /// For their logs and for a human reading a stuck case -- NOT for deciding retryability, which
    /// is what <see cref="Retryable"/> exists to stop them re-deriving.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether the same call can succeed later. The single field they branch on: the rule lives in
    /// <see cref="AttendanceConflictPolicy"/> beside the lifecycle it describes, so a change here
    /// updates their behaviour without renegotiating the contract.
    /// </summary>
    public bool Retryable { get; set; }
}

/// <summary>
/// The attendance report the Case Tracker POSTs when an appointment produced no evaluation (phase 5,
/// 2026-08-07). Sits under <c>api/integration</c> beside the reconcile GET, for the same reason: it
/// is machine-to-machine, with no portal user involved and a shared token rather than a signed-in
/// identity.
///
/// <para>The outcome travels in the BODY rather than as two routes, because the two statuses are one
/// event -- "the appointment produced no evaluation, here is why". A third cause later is then a new
/// enum value rather than a third endpoint.</para>
///
/// <para><b>No <c>[IgnoreAntiforgeryToken]</c>, deliberately, pending the live gate.</b> The two
/// anonymous public POST controllers carry it, but Sonar flags it CRITICAL (S4502) on new code and
/// the prior decision not to add it (<c>docs/plans/2026-07-28-case-tracker-reconcile-get.md:68</c>)
/// was scoped to a GET, so neither settles this. The live gate POSTs with only the token and no
/// antiforgery cookie: a 200 proves the attribute is unnecessary, a 400 proves it is required and
/// gives the security hotspot a demonstrated justification instead of an inherited one.</para>
///
/// <para>The 300/hour rate limit is inherited, not added: the limiter is prefix-scoped on
/// <c>/api/integration</c> so any endpoint under it is capped. Note the budget is SHARED with the
/// reconcile GET and partitioned by client IP, so a Case Tracker repair sweep and these reports draw
/// on one allowance (<c>CaseEvaluationHttpApiHostModule.IntegrationRequestsPerHour</c>).</para>
///
/// <para>Lives in HttpApi.HOST rather than alongside the other controllers in HttpApi, which
/// references only Application.Contracts: this endpoint depends on Domain types, and Host can see
/// Domain through Application. <see cref="CaseTrackerReconcileController"/> sets the precedent.</para>
/// </summary>
[AllowAnonymous]
[ControllerName("CaseTrackerAttendance")]
[Route("api/integration")]
public class CaseTrackerAttendanceController : AbpController
{
    private readonly IntegrationTokenValidator _tokenValidator;
    private readonly CaseTrackerAttendanceService _attendanceService;

    public CaseTrackerAttendanceController(
        IntegrationTokenValidator tokenValidator,
        CaseTrackerAttendanceService attendanceService)
    {
        _tokenValidator = tokenValidator;
        _attendanceService = attendanceService;
    }

    /// <summary>
    /// Records that the appointment produced no evaluation.
    ///
    /// <para><paramref name="tenantId"/> is in the path because the portal is database-per-office and
    /// this request carries no other tenant signal. It is the same <c>tenant.tenantId</c> the Case
    /// Tracker receives in every push, so the caller already holds it.</para>
    /// </summary>
    /// <response code="200">Applied, or already applied -- a retry is a no-op.</response>
    /// <response code="400">Missing or unrecognised <c>outcome</c>.</response>
    /// <response code="401">Missing or incorrect <c>X-Integration-Token</c>.</response>
    /// <response code="404">Unknown office or appointment, or an office with the integration off.</response>
    /// <response code="409">Cannot take this outcome now; body carries the status and a retryable flag.</response>
    [HttpPost]
    [Route("offices/{tenantId}/appointments/{appointmentId}/attendance")]
    public virtual async Task<IActionResult> RecordAttendanceAsync(
        Guid tenantId,
        Guid appointmentId,
        [FromBody] CaseTrackerAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        string? presentedToken = Request.Headers[CaseTrackerIntegrationConsts.IntegrationTokenHeaderName];
        if (!_tokenValidator.IsValid(presentedToken))
        {
            // Rejected before ANY database work, so an unauthenticated caller cannot make us open an
            // office connection or probe which appointment ids exist.
            return Unauthorized();
        }

        // Parsed before the office is opened, for the same reason: a malformed body must not cost a
        // tenant switch. Also the only thing stopping this endpoint becoming a general-purpose
        // status setter -- see AttendanceOutcomeWire.
        if (!AttendanceOutcomeWire.TryParse(request?.Outcome, out var outcome))
        {
            return BadRequest();
        }

        var result = await _attendanceService.ApplyAsync(
            tenantId, appointmentId, outcome, cancellationToken);

        return result.Result switch
        {
            CaseTrackerAttendanceResult.Applied => Ok(),
            // The status and the retry verdict travel together so the caller never has to re-derive
            // one from the other. CurrentStatus is non-null on every conflict (see the outcome type).
            CaseTrackerAttendanceResult.Conflict => Conflict(new CaseTrackerAttendanceConflictResponse
            {
                Status = result.CurrentStatus!.Value.ToString(),
                Retryable = result.IsRetryable,
            }),
            // Deliberately indistinguishable from "unknown appointment", and deliberately BODYLESS
            // -- see the service.
            _ => NotFound(),
        };
    }
}
