using Asp.Versioning;
using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// What the Case Tracker needs to set up an iPad intake: the list of offices, and a blank copy of the patient and
/// doctor packets per office. Beside <see cref="CaseTrackerFeedController"/>, with the same
/// <c>X-Feed-Token</c> check and the same answers, for the same reasons: <see cref="AllowAnonymousAttribute"/> is
/// explicit because the token is the only protection, and it lives in HttpApi.Host because it needs Domain types
/// and an application service would be auto-exposed at a second route with no token check.
///
/// <para>Order, as on the feed: the token first, before any office or database is touched; then the allowance,
/// counted only for a caller that holds the token and kept apart from the feed's 240 an hour; then the work.</para>
///
/// <para>403 for a bad token, a spent allowance, and an office that is unknown or not switched on, never 401,
/// which latches the Case Tracker's client (see the feed controller).</para>
/// </summary>
[AllowAnonymous]
[ControllerName("CaseTrackerForms")]
[Route("api/integration")]
public class CaseTrackerFormsController : AbpController
{
    private readonly FeedTokenValidator _tokenValidator;
    private readonly CaseTrackerFormsAllowance _allowance;
    private readonly CaseTrackerBlankFormService _formService;
    private readonly CaseTrackerOfficeListService _officeListService;
    private readonly IClock _clock;

    public CaseTrackerFormsController(
        FeedTokenValidator tokenValidator,
        CaseTrackerFormsAllowance allowance,
        CaseTrackerBlankFormService formService,
        CaseTrackerOfficeListService officeListService,
        IClock clock)
    {
        _tokenValidator = tokenValidator;
        _allowance = allowance;
        _formService = formService;
        _officeListService = officeListService;
        _clock = clock;
    }

    /// <summary>Every office's id, practice name and doctor name.</summary>
    /// <response code="200">The list, in the Gesco envelope.</response>
    /// <response code="403">A bad token or a spent allowance.</response>
    [HttpGet]
    [Route("offices")]
    [Produces("application/json")]
    public virtual async Task<IActionResult> GetOfficesAsync(CancellationToken cancellationToken)
    {
        if (!HasValidToken())
        {
            return Forbidden();
        }

        if (!_allowance.TryAcquireOfficeList())
        {
            return AllowanceExceeded("The office list's hourly allowance is used up. It refills within the hour.");
        }

        var offices = await _officeListService.ListAsync(cancellationToken);
        return JsonContent(
            StatusCodes.Status200OK,
            CaseTrackerFeedResponseWriter.WriteOfficeList(offices, Guid.NewGuid(), _clock.Now));
    }

    /// <summary>The blank patient packet for one office.</summary>
    /// <response code="200">The PDF, with an ETag.</response>
    /// <response code="304">The caller's If-None-Match names the current version. No body.</response>
    /// <response code="403">A bad token, a spent allowance, or an office that is unknown or not switched on.</response>
    [HttpGet]
    [Route("offices/{officeId}/forms/patient-packet")]
    public virtual Task<IActionResult> GetPatientPacketAsync(Guid officeId, CancellationToken cancellationToken) =>
        GetFormAsync(officeId, BlankPacketKind.PatientPacket, "patient-packet-blank.pdf", cancellationToken);

    /// <summary>The blank doctor packet for one office.</summary>
    /// <response code="200">The PDF, with an ETag.</response>
    /// <response code="304">The caller's If-None-Match names the current version. No body.</response>
    /// <response code="403">A bad token, a spent allowance, or an office that is unknown or not switched on.</response>
    [HttpGet]
    [Route("offices/{officeId}/forms/doctor-packet")]
    public virtual Task<IActionResult> GetDoctorPacketAsync(Guid officeId, CancellationToken cancellationToken) =>
        GetFormAsync(officeId, BlankPacketKind.DoctorPacket, "doctor-packet-blank.pdf", cancellationToken);

    private async Task<IActionResult> GetFormAsync(
        Guid officeId,
        BlankPacketKind kind,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (!HasValidToken())
        {
            return Forbidden();
        }

        if (!_allowance.TryAcquireForm(officeId))
        {
            return AllowanceExceeded("This office's hourly form allowance is used up. It refills within the hour.");
        }

        var result = await _formService.GetAsync(
            officeId, kind, Request.Headers[HeaderNames.IfNoneMatch].ToString(), cancellationToken);

        // The office's own copy: never shared by a cache between callers.
        Response.Headers[HeaderNames.CacheControl] = "private, no-cache";
        switch (result.Outcome)
        {
            case BlankPacketFormOutcome.NotEnabled:
                return Error(StatusCodes.Status403Forbidden, "feed_not_enabled", "The feed is not enabled for this office.");
            case BlankPacketFormOutcome.NotModified:
                Response.Headers[HeaderNames.ETag] = result.ETag;
                return StatusCode(StatusCodes.Status304NotModified);
            case BlankPacketFormOutcome.Pdf:
                Response.Headers[HeaderNames.ETag] = result.ETag;
                return File(result.Pdf!, "application/pdf", fileName);
            default:
                throw new InvalidOperationException($"Unmapped blank form outcome {result.Outcome}.");
        }
    }

    private bool HasValidToken() =>
        _tokenValidator.IsValid(Request.Headers[CaseTrackerFeedConsts.FeedTokenHeaderName]);

    // Generic on purpose: nothing here helps a caller without the token.
    private ContentResult Forbidden() => Error(StatusCodes.Status403Forbidden, "forbidden", "The request is not authorised.");

    private ContentResult AllowanceExceeded(string message) =>
        Error(StatusCodes.Status403Forbidden, "allowance_exceeded", message);

    private ContentResult Error(int status, string code, string message) =>
        JsonContent(status, CaseTrackerFeedResponseWriter.WriteError(code, message, null, Guid.NewGuid(), _clock.Now));

    private static ContentResult JsonContent(int status, string body) =>
        new() { Content = body, ContentType = "application/json", StatusCode = status };
}
