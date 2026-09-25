using Asp.Versioning;
using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// The reconcile read the Case Tracker calls (contract section F). Sits under
/// <c>api/integration</c> rather than <c>api/app</c> because it is machine-to-machine: no portal user
/// is involved, and it is authenticated by a shared token rather than by a signed-in identity.
///
/// <para><see cref="AllowAnonymousAttribute"/> is explicit rather than implied. There is no signed-in
/// user on this path, so the ONLY thing protecting a full appointment payload is
/// <see cref="IntegrationTokenValidator"/> -- stating that in the attribute keeps the security posture
/// visible to the next reader instead of resting on the absence of an <c>[Authorize]</c>.</para>
///
/// <para>No <c>[IgnoreAntiforgeryToken]</c>: a GET does not need it, and Sonar flags the attribute as
/// a CRITICAL finding (S4502) on new code.</para>
///
/// <para>Lives in HttpApi.HOST rather than alongside the other controllers in HttpApi, which
/// references only Application.Contracts. This endpoint depends on Domain types --
/// <see cref="CaseTrackerReconcileService"/>, <see cref="IntakeEnvelope"/> and
/// <see cref="IntakePayloadSerializer"/> -- and Host can see Domain through Application. The
/// alternatives were worse: an application service would be auto-exposed by
/// <c>ConventionalControllers.Create</c> at a SECOND route with no token check, and hoisting the
/// envelope plus serializer into Domain.Shared would refactor already-merged Part 1 code across three
/// layers to satisfy a layering technicality. <c>HomeController</c> sets the precedent for a
/// controller here.</para>
/// </summary>
[AllowAnonymous]
[ControllerName("CaseTrackerReconcile")]
[Route("api/integration")]
public class CaseTrackerReconcileController : AbpController
{
    private readonly IntegrationTokenValidator _tokenValidator;
    private readonly CaseTrackerReconcileService _reconcileService;

    public CaseTrackerReconcileController(
        IntegrationTokenValidator tokenValidator,
        CaseTrackerReconcileService reconcileService)
    {
        _tokenValidator = tokenValidator;
        _reconcileService = reconcileService;
    }

    /// <summary>
    /// Returns the appointment's complete intake payload.
    ///
    /// <para><paramref name="tenantId"/> is in the path because the portal is database-per-office and
    /// this request carries no other tenant signal. It is the same <c>tenant.tenantId</c> the Case
    /// Tracker receives in every push, so the caller already holds it.</para>
    ///
    /// <para>The body is written by <see cref="IntakePayloadSerializer"/> -- the same serializer the
    /// outbound push uses -- rather than returned as an object for MVC to serialize. The contract
    /// promises a byte-identical shape for push and pull so the receiver can run ONE deserializer, and
    /// going through the shared serializer is the only way to guarantee that; returning an object
    /// would silently inherit whatever global JSON options happen to be configured.</para>
    /// </summary>
    /// <response code="200">The payload, identical in shape to a push body.</response>
    /// <response code="401">Missing or incorrect <c>X-Integration-Token</c>.</response>
    /// <response code="404">Unknown appointment, or an office with the integration switched off.</response>
    [HttpGet]
    [Route("offices/{tenantId}/appointments/{appointmentId}")]
    [Produces("application/json")]
    public virtual async Task<IActionResult> GetAppointmentAsync(
        Guid tenantId,
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        string? presentedToken = Request.Headers[CaseTrackerIntegrationConsts.IntegrationTokenHeaderName];
        if (!_tokenValidator.IsValid(presentedToken))
        {
            // Rejected before ANY database work, so an unauthenticated caller cannot make us open an
            // office connection or probe which appointment ids exist.
            return Unauthorized();
        }

        var envelope = await _reconcileService.GetAsync(tenantId, appointmentId, cancellationToken);
        if (envelope == null)
        {
            // Deliberately indistinguishable from "unknown appointment" -- see the service.
            return NotFound();
        }

        return Content(IntakePayloadSerializer.Serialize(envelope), "application/json");
    }
}
