using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Serves one office's changes feed to the Case Tracker (#927): rows after the consumer's cursor, in commit
/// order, and records that cursor as its acknowledgement.
///
/// <para>A DOMAIN service, not an application service, for the reason <see cref="CaseTrackerReconcileService"/>
/// gives: every application service is auto-exposed at a second route that would skip the feed-token check.
/// The token-gated controller is the only way in.</para>
///
/// <para>The cursor IS the acknowledgement: "everything up to here is committed on your side". So it is
/// validated before anything moves -- a malformed cursor, one below the floor set at cutover, or one beyond
/// anything this feed ever issued is refused, because accepting it would count rows as delivered that never
/// arrived. No cursor means "from where you last acknowledged" (decided 2026-09-24), which starts at the
/// floor, so a consumer that lost its state cannot pull history.</para>
///
/// <para>Every request that reaches a feed-mode office is recorded, refused or not: the consumer is alive, and
/// the silence alert is about a dead one. Payloads are never logged; alerts carry ids and cursors only.</para>
///
/// <para>The office's <c>CaseTrackerPushEnabled</c> switch still gates the feed. It is documented as the ePHI
/// gate -- nothing leaves the portal for an office until it is deliberately switched on -- and it keeps that
/// meaning under the feed: an office switched off is answered exactly like one not on the feed.</para>
/// </summary>
public class CaseTrackerFeedService : ITransientDependency
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICaseTrackerFeedStateRepository _feedStateRepository;
    private readonly ICaseTrackerFeedStore _feedStore;
    private readonly CaseTrackerFeedAlertPublisher _alerts;
    private readonly CaseTrackerDeliveryModeReader _deliveryMode;
    private readonly IClock _clock;
    private readonly ILogger<CaseTrackerFeedService> _logger;

    public CaseTrackerFeedService(
        ICurrentTenant currentTenant,
        ICaseTrackerFeedStateRepository feedStateRepository,
        ICaseTrackerFeedStore feedStore,
        CaseTrackerFeedAlertPublisher alerts,
        CaseTrackerDeliveryModeReader deliveryMode,
        IClock clock,
        ILogger<CaseTrackerFeedService> logger)
    {
        _currentTenant = currentTenant;
        _feedStateRepository = feedStateRepository;
        _feedStore = feedStore;
        _alerts = alerts;
        _deliveryMode = deliveryMode;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// One feed request for <paramref name="officeId"/>. Never throws for a bad request or an unknown office:
    /// every failure collapses to <see cref="CaseTrackerFeedOutcome.FeedNotEnabled"/>, the same answer as an
    /// office not on the feed, so the response cannot be used to discover which offices exist.
    /// </summary>
    public virtual async Task<CaseTrackerFeedResult> ReadAsync(
        Guid officeId,
        string? cursor,
        IReadOnlyList<string>? skipped,
        CancellationToken cancellationToken = default)
    {
        using (_currentTenant.Change(officeId))
        {
            try
            {
                return await ReadInOfficeAsync(officeId, cursor, skipped ?? [], cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ids only in the log -- never a payload.
                _logger.LogWarning(
                    ex,
                    "CaseTrackerFeedService: could not serve the feed for office {OfficeId}; answering feed-not-enabled.",
                    officeId);
                return CaseTrackerFeedResult.Refused(CaseTrackerFeedOutcome.FeedNotEnabled);
            }
        }
    }

    /// <summary>
    /// Where a request's cursor puts it, or why it is refused. Pure, so every rule is tested without a database.
    /// A cursor AT the floor is valid: it is where a new feed starts.
    /// </summary>
    public static (CaseTrackerFeedOutcome? Refusal, long Position) CheckCursor(CaseTrackerFeedState state, string? cursor)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrEmpty(cursor))
        {
            return (null, state.AcknowledgedPosition);
        }

        if (!CaseTrackerFeedCursor.TryDecode(cursor, out var position))
        {
            return (CaseTrackerFeedOutcome.CursorInvalid, 0);
        }

        if (position < state.FloorPosition)
        {
            return (CaseTrackerFeedOutcome.CursorBelowFloor, 0);
        }

        return position > state.HighestIssuedPosition
            ? (CaseTrackerFeedOutcome.CursorAhead, 0)
            : (null, position);
    }

    private async Task<CaseTrackerFeedResult> ReadInOfficeAsync(
        Guid officeId,
        string? cursor,
        IReadOnlyList<string> skipped,
        CancellationToken cancellationToken)
    {
        var state = await _feedStateRepository.FindCurrentAsync(cancellationToken);
        if (state is not { IsActive: true } || !await _deliveryMode.IsPushSwitchOnAsync())
        {
            return CaseTrackerFeedResult.Refused(CaseTrackerFeedOutcome.FeedNotEnabled);
        }

        var now = _clock.Now;
        var (refusal, position) = CheckCursor(state, cursor);
        refusal ??= await ReportSkipsAsync(officeId, state, position, skipped, now, cancellationToken);

        if (refusal.HasValue)
        {
            await RecordRefusalAsync(officeId, state, refusal.Value, cursor, now, cancellationToken);
            return CaseTrackerFeedResult.Refused(refusal.Value);
        }

        return await ServeAsync(officeId, state, position, now, cancellationToken);
    }

    /// <summary>
    /// Rows after <paramref name="position"/>, and the acknowledgement recorded. One extra row is read so
    /// <c>hasMore</c> is known without a count. With no rows the next cursor is the cursor sent, so an empty page
    /// -- normal while a write is in flight -- never moves the consumer.
    /// </summary>
    private async Task<CaseTrackerFeedResult> ServeAsync(
        Guid officeId,
        CaseTrackerFeedState state,
        long position,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rows = await _feedStore.ReadPageAsync(officeId, position, CaseTrackerFeedConsts.PageSize + 1, cancellationToken);
        var hasMore = rows.Count > CaseTrackerFeedConsts.PageSize;
        var page = hasMore ? rows.Take(CaseTrackerFeedConsts.PageSize).ToList() : rows;
        var next = page.Count > 0 ? page[^1].Position : position;

        await _feedStateRepository.RecordRequestAsync(state.Id, now, position, next, cancellationToken);
        if (state.CursorAheadAlertedAt.HasValue)
        {
            // A good request ends the cursor-ahead incident and re-arms its email.
            await _feedStateRepository.SetCursorAheadAlertedAsync(state.Id, null, cancellationToken);
        }

        return CaseTrackerFeedResult.Served(page, next, hasMore);
    }

    /// <summary>
    /// Records a refused request as a sign of life without moving any position, and on a cursor-ahead refusal
    /// emails once per incident (decided 2026-09-24): a stuck consumer retries every minute.
    /// </summary>
    private async Task RecordRefusalAsync(
        Guid officeId,
        CaseTrackerFeedState state,
        CaseTrackerFeedOutcome refusal,
        string? cursor,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _feedStateRepository.RecordRequestAsync(
            state.Id, now, state.AcknowledgedPosition, state.HighestIssuedPosition, cancellationToken);

        _logger.LogWarning(
            "CaseTrackerFeedService: refused a feed request from office {OfficeId}: {Refusal}.",
            officeId, refusal);

        if (refusal != CaseTrackerFeedOutcome.CursorAhead || state.CursorAheadAlertedAt.HasValue)
        {
            return;
        }

        await _alerts.PublishAsync(officeId, now, CaseTrackerFeedAlertKind.CursorAhead, eto => eto.Cursor = cursor);
        await _feedStateRepository.SetCursorAheadAlertedAsync(state.Id, now, cancellationToken);
    }

    /// <summary>
    /// Validates and reports the rows the consumer says it deliberately abandoned, or returns the refusal.
    /// All-or-nothing: one bad skip refuses the request before any is reported.
    ///
    /// <para>A skip inside (acknowledged, cursor] is new and is alerted. A skip at or below the acknowledged
    /// position is a REPEAT -- the same request retried after its response was lost -- so it is logged and not
    /// emailed again (decided 2026-09-24). Either way it must name a real Pending row above the floor.</para>
    /// </summary>
    private async Task<CaseTrackerFeedOutcome?> ReportSkipsAsync(
        Guid officeId,
        CaseTrackerFeedState state,
        long position,
        IReadOnlyList<string> skipped,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var reports = new List<(string Cursor, CaseTrackerFeedRow Row)>();
        foreach (var raw in skipped.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var row = await FindSkippedRowAsync(officeId, state, position, raw, cancellationToken);
            if (row == null)
            {
                return CaseTrackerFeedOutcome.SkipInvalid;
            }

            reports.Add((raw, row));
        }

        foreach (var (raw, row) in reports)
        {
            var isRepeat = row.Position <= state.AcknowledgedPosition;
            _logger.LogWarning(
                "CaseTrackerFeedService: office {OfficeId} reported abandoning the {MessageType} row at {Cursor} for appointment {AppointmentId}{Repeat}.",
                officeId, row.MessageType, raw, row.AppointmentId, isRepeat ? " (a repeat; already recorded)" : string.Empty);

            if (!isRepeat)
            {
                await _alerts.PublishAsync(officeId, now, CaseTrackerFeedAlertKind.SkipReported, eto =>
                {
                    eto.Cursor = raw;
                    eto.AppointmentId = row.AppointmentId;
                    eto.MessageType = row.MessageType.ToString();
                });
            }
        }

        return null;
    }

    /// <summary>The Pending row a skip names, or null when it is malformed, out of range, or names no such row.</summary>
    private async Task<CaseTrackerFeedRow?> FindSkippedRowAsync(
        Guid officeId,
        CaseTrackerFeedState state,
        long position,
        string raw,
        CancellationToken cancellationToken)
    {
        if (!CaseTrackerFeedCursor.TryDecode(raw, out var skip) || skip <= state.FloorPosition || skip > position)
        {
            return null;
        }

        return await _feedStore.FindRowAsync(officeId, skip, cancellationToken);
    }
}
