using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Sends due outbox rows for the current office. Claims a batch via the manager's lease, POSTs each
/// row, and records the outcome per the agreed status matrix: Sent on a confirmed 2xx, rescheduled
/// on a retryable failure, dead-lettered immediately on a fatal one.
///
/// <para>A row is therefore never lost (a crash before the mark leaves it Pending for the next
/// drain) and never double-sent (the idempotency key collapses a logical push to one row, and
/// MarkSent is idempotent).</para>
/// </summary>
public class IntegrationOutboxDrainService : ITransientDependency
{
    private readonly IntegrationOutboxManager _outboxManager;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly ICaseTrackerClient _client;
    private readonly IClock _clock;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<IntegrationOutboxDrainService> _logger;

    public IntegrationOutboxDrainService(
        IntegrationOutboxManager outboxManager,
        IIntegrationOutboxRepository outboxRepository,
        ICaseTrackerClient client,
        IClock clock,
        ISettingProvider settingProvider,
        ILogger<IntegrationOutboxDrainService> logger)
    {
        _outboxManager = outboxManager;
        _outboxRepository = outboxRepository;
        _client = client;
        _clock = clock;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    /// <summary>
    /// Drains up to <paramref name="batchSize"/> due rows in the current office scope. Returns
    /// (sent, failed) counts for logging.
    /// </summary>
    public virtual async Task<IntegrationDrainResult> DrainDueAsync(int? batchSize = null)
    {
        // Master switch, read per drain in the current office scope so a per-office override beats
        // the host default and a toggle takes effect on the next pass. When disabled we claim
        // NOTHING: due rows stay Pending and resume automatically once enabled, with no
        // failed-attempt cost burned against the fail-fast cap.
        if (!await _settingProvider.IsTrueAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled))
        {
            _logger.LogInformation(
                "IntegrationOutboxDrainService: Case Tracker push is disabled; holding due rows Pending.");
            return new IntegrationDrainResult(0, 0);
        }

        // Volume guard. DrainBatchSize caps ONE invocation, but every enqueue schedules its own drain,
        // so N queued rows become N drains on parallel workers -- without this there is no ceiling at
        // all. Three current paths can produce a burst: releasing an office's accumulated backlog when
        // its switch is first turned on ("all sent on enable"), a patient edit fanning out across all
        // that patient's published appointments, and any future deliberate backfill.
        //
        // It matters more than a typical rate limit because each intake becomes a CASE their staff must
        // handle. Holding rows Pending is cheap and reversible; delivering hundreds of wrong ones is
        // not, because someone has to unpick them by hand.
        //
        // Deliberately NOT a trip flag: the count is measured over a rolling window from SentAt, so it
        // clears itself as the window slides. There is nothing to reset, and therefore no way to leave
        // an office switched off by accident -- which was the main risk of a breaker with its own state.
        var windowStart = _clock.Now.AddMinutes(-IntegrationOutboxConsts.VolumeWindowMinutes);
        var sentInWindow = await _outboxRepository.CountSentSinceAsync(windowStart);
        if (sentInWindow >= IntegrationOutboxConsts.VolumeThresholdPerWindow)
        {
            // Warning, not Error: hitting the cap is a legitimate outcome for a large backlog, which
            // simply drains over the following windows. Logged with the numbers so a genuine runaway is
            // distinguishable from a slow release.
            _logger.LogWarning(
                "IntegrationOutboxDrainService: volume guard held delivery -- {SentInWindow} sent since {WindowStart:o} reaches the {Threshold} per {WindowMinutes} min cap. Rows stay Pending and resume as the window slides.",
                sentInWindow,
                windowStart,
                IntegrationOutboxConsts.VolumeThresholdPerWindow,
                IntegrationOutboxConsts.VolumeWindowMinutes);
            return new IntegrationDrainResult(0, 0);
        }

        var lease = TimeSpan.FromSeconds(IntegrationOutboxConsts.LeaseDurationSeconds);
        var backoff = TimeSpan.FromSeconds(IntegrationOutboxConsts.RetryBackoffSeconds);
        var size = batchSize ?? IntegrationOutboxConsts.DrainBatchSize;

        var claimed = await _outboxManager.ClaimDueBatchAsync(_clock.Now, lease, size);

        var sent = 0;
        var failed = 0;
        foreach (var row in claimed)
        {
            try
            {
                var result = await _client.PostAsync(row.TargetPath, row.Payload);
                if (result.IsSuccess)
                {
                    row.MarkSent(_clock.Now);
                    sent++;
                }
                else if (result.Outcome == CaseTrackerPushOutcome.Fatal)
                {
                    // Cannot succeed on retry (bad token, malformed request). Dead-letter now so a
                    // human sees it in minutes instead of after the cap.
                    row.MarkFatal(_clock.Now, result.Error);
                    failed++;
                    _logger.LogError(
                        "IntegrationOutboxDrainService: row {RowId} for appointment {AppointmentId} FATALLY failed ({Error}); dead-lettered.",
                        row.Id, row.AppointmentId, result.Error);
                }
                else
                {
                    row.MarkFailed(_clock.Now, result.Error, backoff);
                    failed++;
                    _logger.LogWarning(
                        "IntegrationOutboxDrainService: row {RowId} for appointment {AppointmentId} failed ({Error}); attempt {Attempt}/{Max}.",
                        row.Id, row.AppointmentId, result.Error, row.AttemptCount, row.MaxAttempts);
                }
            }
            catch (Exception ex)
            {
                // Do NOT rethrow: one bad row must not abort the batch or leave the rest claimed.
                // The client already swallows transport faults, so reaching here means something
                // unexpected -- treat it as retryable rather than losing the row.
                row.MarkFailed(_clock.Now, ex.Message, backoff);
                failed++;
                _logger.LogError(
                    ex,
                    "IntegrationOutboxDrainService: row {RowId} for appointment {AppointmentId} threw; attempt {Attempt}/{Max}.",
                    row.Id, row.AppointmentId, row.AttemptCount, row.MaxAttempts);
            }

            await _outboxManager.SaveAsync(row);
        }

        return new IntegrationDrainResult(sent, failed);
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
