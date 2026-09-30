using Asp.Versioning;
using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// The changes feed the Case Tracker pulls (#927), one office per request, once a minute. It replaces the push
/// once an office is cut over, because an internet-facing portal cannot reach the Case Tracker on the office
/// network.
///
/// <para>Lives in HttpApi.HOST beside <see cref="CaseTrackerReconcileController"/>, for the same reasons: it
/// needs Domain types, and an application service would be auto-exposed at a second route with no token check.
/// <see cref="AllowAnonymousAttribute"/> is explicit because the feed token is the only thing protecting the
/// payloads.</para>
///
/// <para>Order matters and is deliberate: the token first, before any office or database is touched; then the
/// office's hourly allowance, so only the real consumer can spend it; then the feed.</para>
///
/// <para>Statuses (decided 2026-09-24, for the consumer's client): 403 for a bad token, a spent allowance, and
/// an office not on the feed or unknown -- never 401, which latches that client. 409 for a cursor or skip the
/// feed cannot accept (changed from 400 on 2026-09-24): their DEPLOYED client acts on the status alone, halts
/// the office where an operator sees it on a 409, and retries anything else it does not know -- 400 included --
/// every minute forever, silently. None of the four can be fixed by retrying. Every body is the Gesco envelope,
/// and every 409 carries one, so a bare 409 did not come from here.</para>
///
/// <para><b>Why not 429, corrected 2026-09-25.</b> This comment used to say 429 "pauses both of its features".
/// That was my claim and it was wrong: it came from section 9 of the feed contract, which is about reconcile
/// and attendance sharing a rate-limit window, and I assumed the feed shared it. Read from their source, it does
/// not -- their feed client holds its own non-static backoff field, while the shared window and the 401 latch
/// both live on their reconcile client. A 429 here could not stall attendance, which is the only path that can
/// set NoShow or NotSeen.</para>
///
/// <para>429 stays unused anyway, for a reason that survives the correction: their feed backoff is per PROCESS
/// rather than per office, so one office's 429 would pause polling for all of them -- a far wider blast radius
/// than the breach. At one poll a minute against
/// <see cref="CaseTrackerFeedConsts.RequestsPerHourPerOffice"/> the allowance should never be reached at all,
/// so this is a choice about the failure shape rather than a hot path.</para>
/// </summary>
[AllowAnonymous]
[ControllerName("CaseTrackerFeed")]
[Route("api/integration")]
public class CaseTrackerFeedController : AbpController
{
    private readonly FeedTokenValidator _tokenValidator;
    private readonly CaseTrackerFeedAllowance _allowance;
    private readonly CaseTrackerFeedService _feedService;
    private readonly IClock _clock;

    public CaseTrackerFeedController(
        FeedTokenValidator tokenValidator,
        CaseTrackerFeedAllowance allowance,
        CaseTrackerFeedService feedService,
        IClock clock)
    {
        _tokenValidator = tokenValidator;
        _allowance = allowance;
        _feedService = feedService;
        _clock = clock;
    }

    /// <summary>
    /// Rows after <paramref name="cursor"/> (or after the office's last acknowledged position when it is
    /// absent), at most 200, in commit order. The cursor sent is recorded as the consumer's acknowledgement.
    /// Each <paramref name="skipped"/> value names a row the consumer deliberately abandoned.
    /// </summary>
    /// <response code="200">A page, possibly empty. An empty page while a write is in flight is normal.</response>
    /// <response code="409">A malformed cursor or skip, a cursor below the floor, or one never issued.</response>
    /// <response code="403">A bad token, a spent allowance, or an office not on the feed.</response>
    [HttpGet]
    [Route("offices/{tenantId}/feed")]
    [Produces("application/json")]
    public virtual async Task<IActionResult> GetFeedAsync(
        Guid tenantId,
        [FromQuery] string? cursor,
        [FromQuery] string[]? skipped,
        CancellationToken cancellationToken)
    {
        string? presentedToken = Request.Headers[CaseTrackerFeedConsts.FeedTokenHeaderName];
        if (!_tokenValidator.IsValid(presentedToken))
        {
            // Generic on purpose: nothing here helps a caller without the token.
            return Error(StatusCodes.Status403Forbidden, "forbidden", "The request is not authorised.", null);
        }

        if (!_allowance.TryAcquire(tenantId))
        {
            return Error(
                StatusCodes.Status403Forbidden,
                "allowance_exceeded",
                "This office's hourly feed allowance is used up. It refills within the hour.",
                null);
        }

        var result = await _feedService.ReadAsync(tenantId, cursor, skipped, cancellationToken);
        return result.Outcome switch
        {
            CaseTrackerFeedOutcome.Page => JsonContent(
                StatusCodes.Status200OK,
                CaseTrackerFeedResponseWriter.WritePage(result, Guid.NewGuid(), _clock.Now)),
            CaseTrackerFeedOutcome.FeedNotEnabled => Error(
                StatusCodes.Status403Forbidden, "feed_not_enabled", "The feed is not enabled for this office.", null),
            CaseTrackerFeedOutcome.CursorBelowFloor => Error(
                StatusCodes.Status409Conflict, "cursor_below_floor", "The cursor is before this office's feed began.", "cursor"),
            CaseTrackerFeedOutcome.CursorAhead => Error(
                StatusCodes.Status409Conflict, "cursor_ahead", "The cursor is beyond anything this feed has issued.", "cursor"),
            CaseTrackerFeedOutcome.SkipInvalid => Error(
                StatusCodes.Status409Conflict, "skip_invalid", "A skipped cursor does not name a row this request acknowledges.", "skipped"),
            CaseTrackerFeedOutcome.CursorInvalid => Error(
                StatusCodes.Status409Conflict, "cursor_invalid", "The cursor is not one this feed issued.", "cursor"),
            _ => throw new InvalidOperationException($"Unmapped feed outcome {result.Outcome}."),
        };
    }

    private ContentResult Error(int status, string code, string message, string? field) =>
        JsonContent(status, CaseTrackerFeedResponseWriter.WriteError(code, message, field, Guid.NewGuid(), _clock.Now));

    private static ContentResult JsonContent(int status, string body) =>
        new() { Content = body, ContentType = "application/json", StatusCode = status };
}
