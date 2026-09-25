using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Sends due outbox rows for the current office. Claims ONE row at a time via the manager's lease,
/// POSTs it, and records the outcome per the agreed status matrix: Sent on a confirmed 2xx,
/// rescheduled on a retryable failure (#917's 24-hour window), dead-lettered on a fatal one.
///
/// <para>ONE SHORT TRANSACTION PER ROW, and none across the HTTP call (#917, decided 2026-09-23).
/// Each row is claimed in its own committed transaction, sent with no transaction open, and its result
/// recorded in a second short transaction. The pass used to be a single transaction held across every
/// call: in a slow outage (30-second timeouts on up to 50 rows) that held row locks for about 25
/// minutes -- blocking anything that read those rows, including the enqueue's per-appointment lookup
/// behind staff actions -- held back the feed position (#927), and a crash mid-pass rolled back every
/// Sent mark so the whole batch was sent again.</para>
///
/// <para>A row is still never lost (a crash between send and record leaves it leased Pending, and the
/// lease expiry hands it to a later pass) and at most re-sent once in that crash window; the receiver
/// upserts, and <see cref="IntegrationOutboxItem.MarkSent"/> is idempotent.</para>
///
/// <para>This service opens its own units of work, so its caller must NOT hold one around it: an outer
/// transaction would swallow the per-row commits and bring back exactly the lock hold above.</para>
///
/// <para>FEED MODE (#927). An office switched to the changes feed is delivered by the Case Tracker pulling, so
/// the drain sends nothing for it. That is checked per pass AND again before every row is claimed: a feed
/// started while a pass is running then stops the pass at the next row, so at most the one row already in
/// flight is pushed after the switch -- delivered once by push, or served once by the feed, never lost.</para>
/// </summary>
public class IntegrationOutboxDrainService : ITransientDependency
{
    private readonly IntegrationOutboxManager _outboxManager;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly ICaseTrackerClient _client;
    private readonly IClock _clock;
    private readonly CaseTrackerDeliveryModeReader _deliveryMode;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<IntegrationOutboxDrainService> _logger;

    public IntegrationOutboxDrainService(
        IntegrationOutboxManager outboxManager,
        IIntegrationOutboxRepository outboxRepository,
        ICaseTrackerClient client,
        IClock clock,
        CaseTrackerDeliveryModeReader deliveryMode,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<IntegrationOutboxDrainService> logger)
    {
        _outboxManager = outboxManager;
        _outboxRepository = outboxRepository;
        _client = client;
        _clock = clock;
        _deliveryMode = deliveryMode;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    /// <summary>
    /// Drains up to <paramref name="batchSize"/> due rows in the current office scope, one at a time.
    /// Returns (sent, failed) counts for logging.
    /// </summary>
    public virtual async Task<IntegrationDrainResult> DrainDueAsync(int? batchSize = null)
    {
        if (!await IsDeliveryOpenAsync())
        {
            return new IntegrationDrainResult(0, 0);
        }

        var lease = TimeSpan.FromSeconds(IntegrationOutboxConsts.LeaseDurationSeconds);
        var size = batchSize ?? IntegrationOutboxConsts.DrainBatchSize;
        var sent = 0;
        var failed = 0;

        for (var i = 0; i < size; i++)
        {
            var row = await ClaimNextAsync(lease);
            if (row == null)
            {
                break;
            }

            var result = await SendAsync(row);
            await RecordAsync(row.Id, result);

            if (result.IsSuccess)
            {
                sent++;
            }
            else
            {
                failed++;
            }
        }

        return new IntegrationDrainResult(sent, failed);
    }

    /// <summary>
    /// The master switch and the volume guard, read in their own short unit of work. When either holds,
    /// NOTHING is claimed: due rows stay Pending with no attempt spent, and resume on a later pass.
    /// </summary>
    private async Task<bool> IsDeliveryOpenAsync()
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        // Master switch, read per drain in the current office scope so a per-office override beats the
        // host default and a toggle takes effect on the next pass.
        if (!await _deliveryMode.IsPushSwitchOnAsync())
        {
            _logger.LogInformation(
                "IntegrationOutboxDrainService: Case Tracker push is disabled; holding due rows Pending.");
            await uow.CompleteAsync();
            return false;
        }

        // #927: an office on the changes feed is delivered by the Case Tracker pulling; nothing is pushed.
        if (await _deliveryMode.IsFeedActiveAsync())
        {
            _logger.LogInformation(
                "IntegrationOutboxDrainService: office is on the Case Tracker feed; the drain sends nothing.");
            await uow.CompleteAsync();
            return false;
        }

        // Volume guard. DrainBatchSize caps ONE invocation, but every enqueue schedules its own drain, so
        // without this there is no ceiling at all. It matters more than a typical rate limit because each
        // intake becomes a CASE their staff must handle. Deliberately NOT a trip flag: the count is
        // measured over a rolling window from SentAt, so it clears itself as the window slides.
        var windowStart = _clock.Now.AddMinutes(-IntegrationOutboxConsts.VolumeWindowMinutes);
        var sentInWindow = await _outboxRepository.CountSentSinceAsync(windowStart);
        await uow.CompleteAsync();

        if (sentInWindow >= IntegrationOutboxConsts.VolumeThresholdPerWindow)
        {
            // Warning, not Error: hitting the cap is a legitimate outcome for a large backlog.
            _logger.LogWarning(
                "IntegrationOutboxDrainService: volume guard held delivery -- {SentInWindow} sent since {WindowStart:o} reaches the {Threshold} per {WindowMinutes} min cap. Rows stay Pending and resume as the window slides.",
                sentInWindow,
                windowStart,
                IntegrationOutboxConsts.VolumeThresholdPerWindow,
                IntegrationOutboxConsts.VolumeWindowMinutes);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Claims and leases the next due row, and COMMITS the lease before any HTTP happens. Claims nothing once
    /// the office has been switched to the feed (#927), so a feed started mid-pass ends the pass here.
    /// </summary>
    private async Task<IntegrationOutboxItem?> ClaimNextAsync(TimeSpan lease)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        if (await _deliveryMode.IsFeedActiveAsync())
        {
            await uow.CompleteAsync();
            return null;
        }

        var row = await _outboxManager.ClaimNextDueAsync(_clock.Now, lease);
        await uow.CompleteAsync();
        return row;
    }

    /// <summary>
    /// Sends one row with NO transaction open. Never throws: the client already reports transport
    /// faults as retryable results, and anything unexpected is treated as retryable too, so one bad row
    /// cannot abort the pass or be lost.
    /// </summary>
    private async Task<CaseTrackerPushResult> SendAsync(IntegrationOutboxItem row)
    {
        try
        {
            return await _client.PostAsync(row.TargetPath, row.Payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "IntegrationOutboxDrainService: row {RowId} for appointment {AppointmentId} threw while sending; treating it as retryable.",
                row.Id, row.AppointmentId);
            return CaseTrackerPushResult.FromTransportFailure(ex.Message);
        }
    }

    /// <summary>
    /// Records one row's result in its own short transaction, against a FRESH copy of the row: the copy
    /// the claim loaded belongs to a unit of work that has already completed.
    /// </summary>
    private async Task RecordAsync(Guid rowId, CaseTrackerPushResult result)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var row = await _outboxRepository.GetAsync(rowId);
        var now = _clock.Now;

        if (result.IsSuccess)
        {
            row.MarkSent(now);
        }
        else if (result.Outcome == CaseTrackerPushOutcome.Fatal)
        {
            // Cannot succeed on retry (bad token, malformed request). Dead-letter now so a human sees it
            // in minutes instead of after the retry window.
            row.MarkFatal(now, result.Error);
            _logger.LogError(
                "IntegrationOutboxDrainService: row {RowId} for appointment {AppointmentId} FATALLY failed ({Error}); dead-lettered.",
                row.Id, row.AppointmentId, result.Error);
        }
        else
        {
            row.MarkFailed(now, result.Error);
            _logger.LogWarning(
                "IntegrationOutboxDrainService: row {RowId} for appointment {AppointmentId} failed ({Error}); attempt {Attempt}, {Status}.",
                row.Id, row.AppointmentId, result.Error, row.AttemptCount, row.Status);
        }

        await _outboxManager.SaveAsync(row);
        await uow.CompleteAsync();
    }
}

/// <summary>Outcome of a single drain pass.</summary>
public class IntegrationDrainResult
{
    public IntegrationDrainResult(int sent, int failed)
    {
        Sent = sent;
        Failed = failed;
    }

    public int Sent { get; }

    public int Failed { get; }
}
